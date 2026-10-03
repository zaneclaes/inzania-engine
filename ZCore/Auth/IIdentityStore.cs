using System;
using System.Threading.Tasks;
using System.Threading;

namespace IZ.Core.Auth;

// Retrieves the access token for the current user
public interface IIdentityStore {
  public event EventHandler<IZIdentity?> OnUserIdentityChanged;

  public IZIdentity? CurrentZIdentity { get; }
  public IZUser? CurrentZUser { get; }

  public StoredSession? StoredSession { get; }
  public LastLoginInfo? LastLogin { get; }

  public IZIdentity? UpdateUserSession(IZSession? session);
  public IZSession? LoadStoredSession();

  // public Task<IZIdentity> RestoreUserSession(Installation install, StoredSession? session = null);
}

public interface ISessionRenewalStore {
  public int SessionRenewalContract { get; }
  public void ConfigureSessionRenewal(int contract, Func<string?, CancellationToken, Task>? renewal);
  public Task EnsureFreshSessionAsync(string? operation = null, CancellationToken cancellationToken = default);
}

public static class SessionRenewalStoreExtensions {
  public static int GetSessionRenewalContract(this IIdentityStore store) =>
    (store as ISessionRenewalStore)?.SessionRenewalContract ?? 0;

  public static void ConfigureSessionRenewal(this IIdentityStore store, int contract, Func<string?, CancellationToken, Task>? renewal) =>
    (store as ISessionRenewalStore)?.ConfigureSessionRenewal(contract, renewal);

  public static Task EnsureFreshSessionAsync(this IIdentityStore store, string? operation = null, CancellationToken cancellationToken = default) =>
    (store as ISessionRenewalStore)?.EnsureFreshSessionAsync(operation, cancellationToken) ?? Task.CompletedTask;
}
