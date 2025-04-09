using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.P2P.Data;

public interface IZP2PSession : ICreatedAt {
  [MaxLength(8)] public string Key { get; set; }

  public IZP2PMember SessionHost { get; }

  public List<IZP2PMember> SessionGuests { get; }

  public DateTime CreatedAt { get; set; }

  // If non-null, this session is no longer open
  public DateTime? ClosedAt { get; set; }

  public List<IZP2PMember> SessionMembers => new List<IZP2PMember>() { SessionHost }.Union(SessionGuests).ToList();
}
