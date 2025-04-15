using System.Threading.Tasks;
using IZ.P2P.Data;
using IZ.P2P.Shared;

namespace IZ.P2P.Guest;

public interface IZGuest<TSession> : IZP2P<TSession>  where TSession : IZP2PSession {
  // public string? IpAddress { get; }

  // public int? Ping { get; }

  public Task<IZP2PSession> Connect(string key, params string[] contentTypes);
}
