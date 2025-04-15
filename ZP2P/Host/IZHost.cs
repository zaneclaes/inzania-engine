using System.Threading.Tasks;
using IZ.P2P.Data;
using IZ.P2P.Shared;

namespace IZ.P2P.Host;

public interface IZHost<TSession> : IZP2P<TSession> where TSession : IZP2PSession {
  public Task<IZP2PSession> StartHosting(params string[] contentTypes);

  public Task StopHosting();
}
