namespace IZ.Core.Utils;
using System;
using System.Text;

    /// <summary>
    /// Pure C# MD5 (RFC 1321) without System.Security.Cryptography.
    /// - One-shot: Md5.ComputeHash(bytes) / Md5.ComputeHashString(text)
    /// - Incremental: new Md5().Update(...).FinalizeHash()
    /// </summary>
    public sealed class Md5
    {
        // S specifies the per-round left rotation amounts.
        private static readonly int[] S = new int[64] {
            7,12,17,22,  7,12,17,22,  7,12,17,22,  7,12,17,22,
            5, 9,14,20,  5, 9,14,20,  5, 9,14,20,  5, 9,14,20,
            4,11,16,23,  4,11,16,23,  4,11,16,23,  4,11,16,23,
            6,10,15,21,  6,10,15,21,  6,10,15,21,  6,10,15,21
        };

        // Use binary integer part of sines of integers (Radians) as constants:
        private static readonly uint[] K = new uint[64] {
            0xd76aa478u, 0xe8c7b756u, 0x242070dbu, 0xc1bdceeeu, 0xf57c0fafu, 0x4787c62au, 0xa8304613u, 0xfd469501u,
            0x698098d8u, 0x8b44f7afu, 0xffff5bb1u, 0x895cd7beu, 0x6b901122u, 0xfd987193u, 0xa679438eu, 0x49b40821u,
            0xf61e2562u, 0xc040b340u, 0x265e5a51u, 0xe9b6c7aau, 0xd62f105du, 0x02441453u, 0xd8a1e681u, 0xe7d3fbc8u,
            0x21e1cde6u, 0xc33707d6u, 0xf4d50d87u, 0x455a14edu, 0xa9e3e905u, 0xfcefa3f8u, 0x676f02d9u, 0x8d2a4c8au,
            0xfffa3942u, 0x8771f681u, 0x6d9d6122u, 0xfde5380cu, 0xa4beea44u, 0x4bdecfa9u, 0xf6bb4b60u, 0xbebfbc70u,
            0x289b7ec6u, 0xeaa127fau, 0xd4ef3085u, 0x04881d05u, 0xd9d4d039u, 0xe6db99e5u, 0x1fa27cf8u, 0xc4ac5665u,
            0xf4292244u, 0x432aff97u, 0xab9423a7u, 0xfc93a039u, 0x655b59c3u, 0x8f0ccc92u, 0xffeff47du, 0x85845dd1u,
            0x6fa87e4fu, 0xfe2ce6e0u, 0xa3014314u, 0x4e0811a1u, 0xf7537e82u, 0xbd3af235u, 0x2ad7d2bbu, 0xeb86d391u
        };

        // State (A, B, C, D)
        private uint _a = 0x67452301u;
        private uint _b = 0xefcdab89u;
        private uint _c = 0x98badcfeu;
        private uint _d = 0x10325476u;

        // Buffering
        private readonly byte[] _buffer = new byte[64];
        private int _bufferLen = 0;
        private ulong _totalLen = 0; // total message length in bytes

        /// <summary>One-shot: compute MD5 of a byte array.</summary>
        public static byte[] ComputeHash(byte[] data)
        {
            var md5 = new Md5();
            md5.Update(data, 0, data?.Length ?? 0);
            return md5.FinalizeHash();
        }

        /// <summary>One-shot: compute MD5 of a UTF-8 string, returning LowerHex.</summary>
        public static string ComputeHashString(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            return ToLowerHex(ComputeHash(bytes));
        }

        /// <summary>Feed more data.</summary>
        public Md5 Update(byte[] data, int offset, int count)
        {
            if (data == null || count == 0) return this;

            _totalLen += (ulong)count;

            int i = 0;
            // If there's data in the buffer, try to fill to 64 and process
            if (_bufferLen > 0)
            {
                int toCopy = Math.Min(64 - _bufferLen, count);
                Buffer.BlockCopy(data, offset, _buffer, _bufferLen, toCopy);
                _bufferLen += toCopy;
                i += toCopy;
                offset += toCopy;
                count -= toCopy;

                if (_bufferLen == 64)
                {
                    Transform(_buffer, 0);
                    _bufferLen = 0;
                }
            }

            // Process as many 64-byte blocks as possible directly from input
            while (count >= 64)
            {
                Transform(data, offset);
                offset += 64;
                count  -= 64;
                i      += 64;
            }

            // Buffer any remaining tail
            if (count > 0)
            {
                Buffer.BlockCopy(data, offset, _buffer, 0, count);
                _bufferLen = count;
            }

            return this;
        }

        /// <summary>Convenience Update overload.</summary>
        public Md5 Update(byte[] data) => Update(data, 0, data?.Length ?? 0);

        /// <summary>Finalize and return 16-byte digest.</summary>
        public byte[] FinalizeHash()
        {
            // Pad: append 0x80, then zeros, then original length in bits as 64-bit LE
            var padding = BuildPadding(_totalLen, _bufferLen);

            Update(padding, 0, padding.Length); // this will process the final blocks

            // Output is A, B, C, D in little-endian
            var digest = new byte[16];
            WriteUInt32LE(digest, 0, _a);
            WriteUInt32LE(digest, 4, _b);
            WriteUInt32LE(digest, 8, _c);
            WriteUInt32LE(digest,12, _d);

            // Reset to avoid accidental reuse
            Reset();

            return digest;
        }

        private void Reset()
        {
            _a = 0x67452301u;
            _b = 0xefcdab89u;
            _c = 0x98badcfeu;
            _d = 0x10325476u;
            _bufferLen = 0;
            _totalLen = 0;
        }

        private static byte[] BuildPadding(ulong totalBytesBeforePadding, int buffered)
        {
            // total length after appending 0x80 and zeros must be ≡ 56 (mod 64), then add 8 length bytes
            // Current remainder:
            int rem = (buffered) % 64;
            int padLen = (rem < 56) ? (56 - rem) : (120 - rem);

            var pad = new byte[padLen + 8];
            pad[0] = 0x80;

            // length in bits, little-endian 64-bit
            ulong bitLen = totalBytesBeforePadding * 8UL;
            for (int i = 0; i < 8; i++)
            {
                pad[padLen + i] = (byte)((bitLen >> (8 * i)) & 0xFF);
            }
            return pad;
        }

        private static void WriteUInt32LE(byte[] dest, int offset, uint value)
        {
            dest[offset + 0] = (byte)(value & 0xFF);
            dest[offset + 1] = (byte)((value >> 8) & 0xFF);
            dest[offset + 2] = (byte)((value >> 16) & 0xFF);
            dest[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static uint ReadUInt32LE(byte[] src, int offset)
        {
            return (uint)(src[offset + 0]
                | (src[offset + 1] << 8)
                | (src[offset + 2] << 16)
                | (src[offset + 3] << 24));
        }

        private static uint RotateLeft(uint x, int n) => (x << n) | (x >> (32 - n));

        private void Transform(byte[] block, int offset)
        {
            // Break chunk into sixteen 32-bit little-endian words M[0..15]
            uint[] M = new uint[16];
            for (int i = 0; i < 16; i++)
                M[i] = ReadUInt32LE(block, offset + (i * 4));

            uint A = _a, B = _b, C = _c, D = _d;

            unchecked
            {
                for (int i = 0; i < 64; i++)
                {
                    uint F, g;

                    if (i < 16)
                    {
                        F = (B & C) | (~B & D);
                        g = (uint)i;
                    }
                    else if (i < 32)
                    {
                        F = (D & B) | (~D & C);
                        g = (uint)((5 * i + 1) & 0x0F);
                    }
                    else if (i < 48)
                    {
                        F = B ^ C ^ D;
                        g = (uint)((3 * i + 5) & 0x0F);
                    }
                    else
                    {
                        F = C ^ (B | ~D);
                        g = (uint)((7 * i) & 0x0F);
                    }

                    uint tmp = D;
                    D = C;
                    C = B;
                    uint sum = A + F + K[i] + M[g];
                    B = B + RotateLeft(sum, S[i]);
                    A = tmp;
                }

                _a += A;
                _b += B;
                _c += C;
                _d += D;
            }
        }

        public static string ToLowerHex(byte[] bytes)
        {
            if (bytes == null) return string.Empty;
            var c = new char[bytes.Length * 2];
            int p = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                c[p++] = GetHexNibble((byte)(b >> 4));
                c[p++] = GetHexNibble((byte)(b & 0xF));
            }
            return new string(c);
        }

        private static char GetHexNibble(byte b)
        {
            return (char)(b < 10 ? ('0' + b) : ('a' + (b - 10)));
        }
    }

/* ---------------------------
   Quick self-test / examples:
   ---------------------------
   Console.WriteLine(PureCrypto.Md5.ComputeHashString(""));
   // d41d8cd98f00b204e9800998ecf8427e

   Console.WriteLine(PureCrypto.Md5.ComputeHashString("abc"));
   // 900150983cd24fb0d6963f7d28e17f72

   Console.WriteLine(PureCrypto.Md5.ComputeHashString("The quick brown fox jumps over the lazy dog"));
   // 9e107d9d372bb6826bd81d3542a419d6

   // Incremental:
   var md5 = new PureCrypto.Md5();
   md5.Update(Encoding.UTF8.GetBytes("The quick brown fox "));
   md5.Update(Encoding.UTF8.GetBytes("jumps over the lazy dog"));
   var digest = md5.FinalizeHash();
   Console.WriteLine(PureCrypto.Md5.ToLowerHex(digest));
*/
