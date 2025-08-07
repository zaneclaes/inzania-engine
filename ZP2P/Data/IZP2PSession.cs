using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using IZ.Core.Data;

namespace IZ.P2P.Data;

public interface IZP2PSession<TMember> : ICreatedAt where TMember : IZP2PMember {
  [MaxLength(8)] public string Key { get; set; }

  public TMember Host { get; }

  public List<TMember> Guests { get; }

  // If non-null, this session is no longer open
  public DateTime? ClosedAt { get; set; }

  public List<TMember> Members => new List<TMember> {
    Host
  }.Union(Guests).ToList();
}
