#region

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Server.Design;

/// <summary>
/// Lets the `dotnet ef` CLI construct a <see cref="ZHostApp{TDb}" />'s context. Derive from it in
/// the STARTUP PROJECT — EF only scans that assembly for the factory — and implement
/// <see cref="CreateApp" />:
///
/// <code>
/// public class TuneDesignTimeDbContextFactory : ZDesignTimeDbContextFactory&lt;TuneHostApp, TuneServerDb&gt; {
///   protected override TuneHostApp CreateApp(WebApplicationBuilder builder) => new(builder);
/// }
/// </code>
///
/// <para><b>The problem it solves.</b> A <c>ZDbContext</c> takes an <see cref="IZ.Core.Contexts.IZContext" />,
/// which resolves through <c>IZRootContext</c> — registered SCOPED, because on a server it lives for
/// one HTTP request (<c>DependencyInjection.AddZApp</c>). With no factory, EF builds the app's host
/// and asks the ROOT provider for the context, which cannot supply a scoped dependency:
/// <c>Cannot resolve 'IZ.Core.Contexts.IZContext' from root provider because it requires scoped
/// service 'IZ.Core.Contexts.IZRootContext'</c>. Every `dotnet ef` command dies there — including
/// <c>migrations has-pending-model-changes</c>, which is what
/// <c>ci/hooks/PendingMigrations.cs</c> runs, so the commit-time check is unavailable to any repo in
/// this state.</para>
///
/// <para><b>Why this and not the ZTRANSIENT escape hatch.</b> Setting <c>ZTRANSIENT</c> re-registers
/// <c>IZRootContext</c> as transient so the root provider can serve it. That works, but it changes
/// the app's real context lifetime through an environment variable: every caller silently gets its
/// OWN root context instead of sharing the request's, and nothing stops the variable being set in a
/// shell that later runs the server. This factory changes no registration at all — it opens the
/// scope EF did not, which is the same thing the running app does per request.</para>
///
/// <para>The provider and scope are deliberately kept alive for the life of the process: EF disposes
/// the context it is handed, and disposing the scope with it would take out the context's own
/// dependencies mid-command. Design-time commands are short-lived processes, and both are released
/// on exit.</para>
/// </summary>
public abstract class ZDesignTimeDbContextFactory<TApp, TDb> : IDesignTimeDbContextFactory<TDb>
  where TApp : ZHostApp<TDb> where TDb : DbContext {

  private static readonly List<IDisposable> Retained = new();
  private static bool _exitHooked;

  /// <summary>Constructs the host app around the builder — normally <c>new MyHostApp(builder)</c>.</summary>
  protected abstract TApp CreateApp(WebApplicationBuilder builder);

  public TDb CreateDbContext(string[] args) {
    // EF's own argv must not reach the host builder: the command-line configuration provider throws
    // on the bare words EF passes through.
    var app = CreateApp(WebApplication.CreateBuilder(Array.Empty<string>()));
    var services = app.BuildServicesAsync().GetAwaiter().GetResult();
    var scope = services.CreateScope();
    Retain(services, scope);
    return scope.ServiceProvider.GetRequiredService<TDb>();
  }

  private static void Retain(params IDisposable[] disposables) {
    lock (Retained) {
      Retained.AddRange(disposables);
      if (_exitHooked) return;
      _exitHooked = true;
      AppDomain.CurrentDomain.ProcessExit += (_, _) => {
        lock (Retained) {
          // Reverse order: the scope must go before the provider that owns it.
          for (int i = Retained.Count - 1; i >= 0; i--) {
            try { Retained[i].Dispose(); } catch { /* a design-time process is exiting anyway */ }
          }
          Retained.Clear();
        }
      };
    }
  }
}
