using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Microsoft.AspNetCore.Http;
using Tuneality.Core.Auth;
using Tuneality.Core.Users;

namespace IZ.Server.Graphql;

public static class HttpExtensions {

  public static void ClaimTuneIdentity(this HttpContext http, ITuneIdentity identity) {
    http.User = identity.Principal;
  }

  public static string? GetAuthToken(this HttpContext http) {
    string? auth = http.Request.Headers["Authorization"].ToString();
    string? authPre = "bearer ";
    return auth.StartsWith(authPre, true, CultureInfo.InvariantCulture) ? auth.Substring(authPre.Length).Trim() : null;
  }

  public static string? GetInstallId(this HttpContext http) {
    string? auth = http.Request.Headers["InstallId"].ToString();
    return string.IsNullOrWhiteSpace(auth) ? null : auth;
  }


  private static List<TuneUserRole> GetTuneRoles(this ClaimsPrincipal curUser) =>
    TuneRoles.AllRoles.Where(r => curUser.IsInRole(r.ToString())).ToList();

  public static TuneUserRole GetTuneRole(this ClaimsPrincipal curUser) {
    var ret = TuneUserRole.Visitor;
    foreach (var role in GetTuneRoles(curUser)) {
      if (role > ret) ret = role;
    }
    return ret;
  }

  public static async Task<ITuneIdentity> GetUserIdentity(this HttpContext http, ITuneContext context) {
    string? authToken = http.GetAuthToken();
    string installId = http.GetInstallId() ?? "";
    PrincipalIdentity? identity = null;
    if (http.User.Identity == null || !http.User.Identity.IsAuthenticated) {
      if (context.App.Env <= IZEnvironment.Development && authToken != null) {
        // auto-login to develeopment...
        identity = new PrincipalIdentity("zane", "Zane", "zaneclaes@gmail.com", TuneUserRole.Admin, authToken);
        // context.Log.Warning("[LOGIN] auto-login to Zane in development via {token} len token; IDs: {identities}", authToken.Length, identity.Identities);
      } else {
        if (authToken != null) context.Log.Warning("[AUTH] token {token} has no identity", authToken);
        else context.Log.Warning("[AUTH] token {token} has no identity", authToken);
        return new TuneVisitorIdentity(context, installId, authToken);
      }
    } else {
      context.Log.Warning("[LOGIN] PRINCIPAL: {identities}", http.User);
      identity = new PrincipalIdentity(http.User, authToken!);
    }
    // context.Log.Debug("[USER] claims {@claims} from token {token}", curUser.Claims.Select(c => $"{c.Type}: {c.Value}"), authToken);

    string tokenId = identity.AuthToken.ToSha256();
    IQueryable<TuneSession> sessionQuery = context
      .QueryForModelId<TuneSession, string>(tokenId)
      .Fetch(m => m.User);
    var session = await context.UpsertModel(sessionQuery, async (ses, state) => {
      var user = await context.UpsertId<TuneUser>(identity.Id, u => {
        u.Username = identity.Username;
        u.UsernameLower = identity.Username.ToLowerInvariant();
        // context.Data.SetChanged(u);
        ses.Log.Information("[USER] created {@user}", u);
      });

      if (state == DataState.Created) {
        ses.Id = tokenId;
        ses.ExpiresAt = IZEnv.Now + TimeSpan.FromDays(30);

        ses.Log.Debug("[SESSION] created {@session}", ses);
      } else {
        // TODO: check internal expiresAt? Should be caught by identity server...
        ses.Log.Debug("[SESSION] loaded {@session}", ses);
      }
      ses.Token = identity.AuthToken;
      ses.User = user;
      ses.UserId = user.Id;
      ses.ClientId = installId;

      if (identity.Role != ses.User.Role) {
        ses.User.Role = identity.Role;
        // context.Data.SetChanged(ses.User);
      }
      if (ses.User.Email != identity.Email.ToLowerInvariant()) {
        ses.User.Email = identity.Email.ToLowerInvariant();
        // context.Data.SetChanged(ses.User);
      }
    });
    if (session.User.LastActiveAt < IZEnv.Now - TimeSpan.FromMinutes(10))
      session.User.LastActiveAt = IZEnv.Now; // slight sampling here to prevent lots of noisy DB saves
    await context.Data.SaveIfNeededAsync();
    return new TuneUserIdentity(context, session, identity.Identities);
  }

  public static string GetClaim(this ClaimsPrincipal principal, string key) =>
    principal.Claims.FirstOrDefault(c => c.Type.Split("/").Last() == key)?.Value ??
    throw new NullReferenceException(key);

  // public static async Task<ClientIp?> AddClientIpHit(this HttpContext httpContext, string? resource = null, uint duration = 0) {
  //   FurContext context = httpContext.RequestServices.GetRequiredService<FurContext>();
  //   if (httpContext.Items.TryGetValue("ClientIp", out var obj) && obj is ClientIp cip) {
  //     if (resource != null) {
  //       FurPlayer player = await context.RequireMinimalCurrentPlayer();
  //       cip.AddHit(httpContext.TraceIdentifier, CorrelationIdentifier.TraceId.ToString(), resource, player, duration);
  //     }
  //     return cip;
  //   }
  //
  //   string? ipAddr = httpContext.GetClientIpAddress(true);
  //   if (string.IsNullOrWhiteSpace(ipAddr)) {
  //     // context.Log.Warning("[API] failed to get HTTP IP {@req}", httpContext.Request);
  //     return null;
  //   }
  //
  //   // DateTime oldest = IZEnv.Now - TimeSpan.FromDays(2);
  //   ClientIp? clientIp = await context.Data.ClientIps
  //     // .Include(c => c.Hits.Where(h => h.CreatedAt > oldest))
  //     .Include(c => c.Players)
  //     .Where(c => c.Id.Equals(ipAddr))
  //     .LoadDataModelAsync(context);
  //
  //   bool saveRequired = false;
  //   if (clientIp == null) {
  //     clientIp = new ClientIp() {Id = ipAddr};
  //     context.Data.ClientIps.Add(clientIp);
  //     saveRequired = true; // Save new IPs immediately so that we don't have duplicate entries
  //   }
  //
  //   if (resource != null) {
  //     FurPlayer player = await context.RequireMinimalCurrentPlayer();
  //     clientIp.AddHit(httpContext.TraceIdentifier, CorrelationIdentifier.TraceId.ToString(), resource, player, duration);
  //   }
  //   httpContext.Items["ClientIp"] = clientIp;
  //
  //   if (saveRequired) {
  //     await context.Save(nameof(AddClientIpHit));
  //   }
  //
  //   // context.Log.Information("[HTTP] HIT {@client}", clientIp);
  //   return clientIp;
  // }

  // Missing some more options https://stackoverflow.com/questions/527638/how-can-i-get-the-clients-ip-address-from-http-headers?noredirect=1&lq=1
  public static string? GetClientIpAddress(this HttpContext httpContext, bool checkForward = false) {
    string? ip = null;
    if (checkForward) {
      ip = httpContext.Request.Headers["X-Forwarded-For"];
      if (string.IsNullOrWhiteSpace(ip)) ip = httpContext.Request.Headers["HTTP_X_FORWARDED_FOR"];
    }

    if (string.IsNullOrEmpty(ip)) ip = httpContext.Request.Headers["REMOTE_ADDR"];
    else // Using X-Forwarded-For last address
      ip = ip.Split(',')
        .Last()
        .Trim();

    return ip;
  }
}
