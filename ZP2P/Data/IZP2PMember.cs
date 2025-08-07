using System;
using System.Collections.Generic;
using IZ.Core.Contexts;
using IZ.Core.Data;

namespace IZ.P2P.Data;

public interface IZP2PMember : IHaveContext, IStringKeyData, ICreatedAt {
  public string Name { get; }

  public List<IZP2PConnectionOption> P2PConnectionOptions { get; }

  // public IZP2PSession? GuestOfSession { get; }

  public DateTime? ConnectedAt { get; set; }

  public DateTime? DisconnectedAt { get; set; }
}
