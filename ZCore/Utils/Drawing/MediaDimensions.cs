using System;
using System.IO;
using System.Text;


namespace IZ.Core.Utils.Drawing;

public static class MediaDimensions
{

  /// <summary>
  /// Returns the dimensions of an image or video as a ZSize (Width, Height).
  /// Supported: PNG, JPEG, MP4/MOV, WebM/MKV.
  /// </summary>
  public static ZSize? GetDimensions(string path) {
    using var s = File.OpenRead(path);
    Span<byte> head = stackalloc byte[12];
    int n = s.Read(head);
    if (n < 12) return null;

    // PNG
    if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
      return ReadPngIHDR(s);

    // JPEG
    if (head[0] == 0xFF && head[1] == 0xD8)
      return ReadJpegSOF(s);

    // WebM/MKV (EBML header)
    if (head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3)
      return ReadMatroska(s, alreadyRead: 12, prefix: head);

    // MP4/MOV (ISO BMFF)
    if ((path.EndsWith(".mp4") || path.EndsWith(".mov")) && LooksLikeIsoBmff(head))
      return ReadIsoBmff(path);

    return null;
  }

    // ---------- PNG ----------
    private static ZSize ReadPngIHDR(Stream s)
    {
        s.Position = 8; // after PNG signature
        Span<byte> buf = stackalloc byte[25];
        if (s.Read(buf) < 25) throw new InvalidDataException("Truncated PNG.");
        if (!(buf[4] == 0x49 && buf[5] == 0x48 && buf[6] == 0x44 && buf[7] == 0x52))
            throw new InvalidDataException("Missing IHDR chunk.");
        int w = ReadInt32BE(buf.Slice(8, 4));
        int h = ReadInt32BE(buf.Slice(12, 4));
        return new ZSize(w, h);
    }

    // ---------- JPEG ----------
    private static ZSize ReadJpegSOF(Stream s)
    {
        int b1 = s.ReadByte(), b2 = s.ReadByte();
        while (b1 != -1 && b2 != -1)
        {
            if (b1 == 0xFF && b2 >= 0xC0 && b2 <= 0xC3)
            {
                Span<byte> seg = stackalloc byte[7];
                if (s.Read(seg) < 7) break;
                int h = (seg[3] << 8) | seg[4];
                int w = (seg[5] << 8) | seg[6];
                return new ZSize(w, h);
            }
            if (b1 == 0xFF && b2 != 0xFF && b2 != 0xD8 && b2 != 0xD9)
            {
                Span<byte> lenBuf = stackalloc byte[2];
                if (s.Read(lenBuf) < 2) break;
                int len = (lenBuf[0] << 8) | lenBuf[1];
                if (len < 2) break;
                s.Position += len - 2;
            }
            b1 = s.ReadByte(); b2 = s.ReadByte();
        }
        throw new InvalidDataException("JPEG SOF not found.");
    }

    // ---------- ISO BMFF (MP4/MOV) ----------
    private static bool LooksLikeIsoBmff(ReadOnlySpan<byte> head)
    {
        uint size = ReadUInt32BE(head[..4]);
        return size >= 8 && IsAsciiFourCC(head.Slice(4, 4));
    }

    private static ZSize ReadIsoBmff(string path)
    {
        using var s = File.OpenRead(path);
        long fileSize = s.Length;
        foreach (var (boxType, start, size) in EnumerateTopLevelBoxes(s))
        {
            if (boxType == "moov")
            {
                long moovEnd = start + size;
                s.Position = start + 8;
                while (s.Position + 8 <= moovEnd)
                {
                    var (tType, tStart, tSize) = ReadBoxHeader(s, moovEnd);
                    if (tType == "trak" && TryGetVideoTrackSize(s, tStart, tSize, out var z))
                        return z;
                    s.Position = tStart + tSize;
                }
            }
        }
        throw new InvalidDataException("No video track found.");
    }

    private static bool TryGetVideoTrackSize(Stream s, long trakStart, uint trakSize, out ZSize z)
    {
        z = new ZSize(0,0);
        long trakEnd = trakStart + trakSize;
        long pos = trakStart + 8;
        bool isVideo = false;
        ZSize? tkhd = null;

        while (pos + 8 <= trakEnd)
        {
            s.Position = pos;
            var (type, start, size) = ReadBoxHeader(s, trakEnd);
            if (size < 8) break;

            if (type == "tkhd")
                tkhd = ReadTkhdWH(s, start, size);
            else if (type == "mdia")
                isVideo = IsVideoHandler(s, start, size);

            pos = start + size;
        }

        if (isVideo && tkhd != null) {
            z = tkhd;
            return true;
        }
        return false;
    }

    private static ZSize ReadTkhdWH(Stream s, long start, uint size)
    {
        s.Position = start + 8;
        int version = s.ReadByte();
        s.Position += 3; // flags
        int timeBytes = version == 1 ? 8 : 4;
        s.Position += timeBytes * 2 + 4 + 4 + timeBytes + 8 + 8;
        s.Position += 2 + 2 + 2 + 2 + 36; // skip to width/height
        Span<byte> wh = stackalloc byte[8];
        if (s.Read(wh) < 8) throw new InvalidDataException("Truncated tkhd.");
        float w = ReadUInt32BE(wh[..4]) / 65536f;
        float h = ReadUInt32BE(wh.Slice(4, 4)) / 65536f;
        return new ZSize(w, h);
    }

    private static bool IsVideoHandler(Stream s, long mdiaStart, uint mdiaSize)
    {
        long mdiaEnd = mdiaStart + mdiaSize;
        long pos = mdiaStart + 8;
        while (pos + 8 <= mdiaEnd)
        {
            s.Position = pos;
            var (type, start, size) = ReadBoxHeader(s, mdiaEnd);
            if (type == "hdlr")
            {
                s.Position = start + 16;
                Span<byte> handler = stackalloc byte[4];
                if (s.Read(handler) < 4) return false;
                return Encoding.ASCII.GetString(handler) == "vide";
            }
            pos = start + size;
        }
        return false;
    }

    // ---------- Matroska / WebM ----------
    private static ZSize ReadMatroska(Stream s, int alreadyRead, ReadOnlySpan<byte> prefix)
    {
        s.Position = 0;
        if (!FindEbmlElement(s, 0x18538067, long.MaxValue, out long segStart, out long segEnd))
            throw new InvalidDataException("Segment not found.");
        if (!FindEbmlElement(s, 0x1654AE6B, segEnd, out long tracksStart, out long tracksEnd))
            throw new InvalidDataException("Tracks not found.");

        long p = tracksStart;
        while (p < tracksEnd)
        {
            if (!ReadEbmlHeader(s, tracksEnd, out ulong id, out ulong size, out long dataStart))
                break;
            long dataEnd = dataStart + (long)size;
            if (id == 0xAE)
            {
                int? type = null; int? w = null; int? h = null;
                long tp = dataStart;
                while (tp < dataEnd)
                {
                    if (!ReadEbmlHeader(s, dataEnd, out ulong cid, out ulong csize, out long cstart))
                        break;
                    long cend = cstart + (long)csize;
                    if (cid == 0x83) type = s.ReadByte();
                    else if (cid == 0xE0)
                    {
                        long vp = cstart;
                        while (vp < cend)
                        {
                            if (!ReadEbmlHeader(s, cend, out ulong vid, out ulong vsize, out long vstart))
                                break;
                            if (vid == 0xB0) w = (int)ReadUnsignedEbmlInteger(s, vsize);
                            else if (vid == 0xBA) h = (int)ReadUnsignedEbmlInteger(s, vsize);
                            vp = vstart + (long)vsize;
                            s.Position = vp;
                        }
                    }
                    tp = cend; s.Position = tp;
                }
                if (type == 1 && w.HasValue && h.HasValue)
                    return new ZSize(w.Value, h.Value);
            }
            p = dataEnd; s.Position = p;
        }
        throw new InvalidDataException("Video track not found.");
    }

    // ---------- EBML helpers ----------
    private static bool FindEbmlElement(Stream s, ulong targetId, long limit, out long start, out long end)
    {
        long p = s.Position;
        while (p < limit)
        {
            if (!ReadEbmlHeader(s, limit, out ulong id, out ulong size, out long dataStart))
                break;
            long dataEnd = dataStart + (long)size;
            if (id == targetId) { start = dataStart; end = dataEnd; return true; }
            s.Position = dataEnd; p = dataEnd;
        }
        start = end = 0; return false;
    }

    private static bool ReadEbmlHeader(Stream s, long limit, out ulong id, out ulong size, out long dataStart)
    {
        id = size = 0; dataStart = 0;
        if (!ReadVInt(s, out ulong idVal, out _)) return false;
        if (!ReadVInt(s, out ulong sizeVal, out _)) return false;
        id = idVal; size = sizeVal; dataStart = s.Position;
        return true;
    }

    private static bool ReadVInt(Stream s, out ulong value, out int length)
    {
        value = 0; length = 0;
        int first = s.ReadByte();
        if (first < 0) return false;
        byte b = (byte)first;
        int leading = 0;
        for (int i = 7; i >= 0; i--) if (((b >> i) & 1) == 1) { leading = 8 - i; break; }
        if (leading == 0 || leading > 8) return false;
        length = leading;
        ulong mask = (ulong)(0xFF >> leading);
        value = (ulong)(b & mask);
        for (int i = 1; i < length; i++)
        {
            int nb = s.ReadByte(); if (nb < 0) return false;
            value = (value << 8) | (byte)nb;
        }
        return true;
    }

    private static ulong ReadUnsignedEbmlInteger(Stream s, ulong size)
    {
        ulong val = 0;
        for (ulong i = 0; i < size; i++)
        {
            int b = s.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            val = (val << 8) | (byte)b;
        }
        return val;
    }

    // ---------- ISO BMFF box utilities ----------
    private static (string type, long start, uint size) ReadBoxHeader(Stream s, long limit)
    {
        Span<byte> hdr = stackalloc byte[8];
        if (s.Read(hdr) < 8) return ("", s.Position, 0);
        uint size = ReadUInt32BE(hdr[..4]);
        string type = Encoding.ASCII.GetString(hdr.Slice(4, 4));
        long start = s.Position - 8;
        if (size == 1)
        {
            Span<byte> ext = stackalloc byte[8];
            if (s.Read(ext) < 8) return ("", start, 0);
            ulong big = ReadUInt64BE(ext);
            size = (uint)Math.Min((ulong)uint.MaxValue, big);
        }
        else if (size == 0)
            size = (uint)Math.Max(8, limit - start);
        return (type, start, size);
    }

    private static System.Collections.Generic.IEnumerable<(string type, long start, uint size)> EnumerateTopLevelBoxes(Stream s)
    {
        s.Position = 0;
        long len = s.Length;
        while (s.Position + 8 <= len)
        {
            long pos = s.Position;
            var (type, start, size) = ReadBoxHeader(s, len);
            if (size < 8) yield break;
            yield return (type, start, size);
            s.Position = pos + size;
        }
    }

    // ---------- Common helpers ----------
    private static uint ReadUInt32BE(ReadOnlySpan<byte> b) =>
        ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];

    private static ulong ReadUInt64BE(ReadOnlySpan<byte> b) =>
        ((ulong)b[0] << 56) | ((ulong)b[1] << 48) | ((ulong)b[2] << 40) | ((ulong)b[3] << 32) |
        ((ulong)b[4] << 24) | ((ulong)b[5] << 16) | ((ulong)b[6] << 8) | b[7];

    private static int ReadInt32BE(ReadOnlySpan<byte> b) => (int)ReadUInt32BE(b);

    private static bool IsAsciiFourCC(ReadOnlySpan<byte> b)
    {
        foreach (byte c in b)
            if (c < 0x20 || c > 0x7E) return false;
        return true;
    }
}
