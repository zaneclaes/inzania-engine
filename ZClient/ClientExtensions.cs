#region

using System;
using System.Text.Json;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using StrawberryShake.Json;
using StrawberryShake.Transport.Http;

#endregion

namespace IZ.Client;

public static class ClientExtensions {
  public static IServiceCollection AddTuneQueries<TSession, TConn>(this IServiceCollection c, Func<IServiceProvider, TConn> connBuilder)
    where TSession : class, IStoredUserSession where TConn : class, IHttpConnection => c
    .AddSingleton<IStoredUserSession, TSession>()
    .AddSingleton<IServerConnection>(sp => new TuneGraphServerConnection(sp.GetCurrentContext()))
    .AddSingleton<IHttpConnection, TConn>(connBuilder) //
    // .AddTuneQuery().Services
    .AddSingleton<IEntityStore, EntityStore>()
    .AddSingleton<IOperationStore>(sp => new OperationStore(sp.GetRequiredService<IEntityStore>()))
    // .AddSingleton<StrawberryShake.IOperationResultBuilder<global::System.Text.Json.JsonDocument, GraphResult>, GraphBuilder>()
    .AddSingleton<IResultPatcher<JsonDocument>, JsonResultPatcher>();
}
