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

namespace IZ.Server.Graphql;

public static class HttpExtensions {

  public static void ClaimZIdentity(this HttpContext http, IZIdentity identity) {
    http.User = identity.Principal;
  }

  public static string? GetAuthToken(this HttpContext http) {
    string? auth = http.Request.Headers[ZHeaders.Authorization].ToString();
    string? authPre = "bearer ";
    return auth.StartsWith(authPre, true, CultureInfo.InvariantCulture) ? auth.Substring(authPre.Length).Trim() : null;
  }

  public static string? GetInstallId(this HttpContext http) {
    string? auth = http.Request.Headers[ZHeaders.InstallId].ToString();
    return string.IsNullOrWhiteSpace(auth) ? null : auth;
  }


  private static List<ZUserRole> GetZRoles(this ClaimsPrincipal curUser) =>
    ZRoles.AllRoles.Where(r => curUser.IsInRole(r.ToString())).ToList();

  public static ZUserRole GetZRole(this ClaimsPrincipal curUser) {
    var ret = ZUserRole.Visitor;
    foreach (var role in GetZRoles(curUser)) {
      if (role > ret) ret = role;
    }
    return ret;
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
  //   // DateTime oldest = ZEnv.Now - TimeSpan.FromDays(2);
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
