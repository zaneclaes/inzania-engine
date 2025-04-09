using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IZ.Core.Data;
using IZ.P2P.Shared;

namespace IZ.P2P.Data;

public interface IZP2PConnectionOption {
  public const string ContentTypeAudio = "audio";

  public const string ContentTypeVideo = "video";

  public string IpAddress { get; }

  public ushort PortNumber { get; set; }

  public ZP2PAccessibility Accessibility { get; set; }

  public string? ContentType { get; set; }

  public bool IsLocal => Accessibility == ZP2PAccessibility.Local;
  public bool IsPublic => Accessibility == ZP2PAccessibility.Public;

  public bool IsAudio => ContentType == ContentTypeAudio;
  public bool IsVideo => ContentType == ContentTypeVideo;
}
