#region

using System;
using System.Collections.Generic;
using IZ.Client.Networking.WebSockets;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using SocketBuilder = System.Func<
  IZ.Core.Contexts.IZContext, System.Uri, string, System.Collections.Generic.Dictionary<string, string>?, IZ.Client.Networking.WebSockets.IWebSocket>;

#endregion

namespace IZ.Client;

public static class TuneQueries {
  private static SocketBuilder _webSocketBuilder = (context, url, protocol, headers) => new SystemWebSocket();

  public static void SetSocketBuilder(SocketBuilder b) {
    _webSocketBuilder = b;
  }

  public static IWebSocket CreateWebSocket(IZContext context, Uri url, string subprotocols, Dictionary<string, string>? headers = null) =>
    _webSocketBuilder.Invoke(context, url, subprotocols, GetHeaders(context, headers));

  public static OperationKind ToOperationKind(this ApiExecutionType executionType) {
    if (executionType == ApiExecutionType.Query) return OperationKind.Query;
    if (executionType == ApiExecutionType.Mutation) return OperationKind.Mutation;
    if (executionType == ApiExecutionType.Subscription) return OperationKind.Subscription;
    throw new ArgumentException(executionType.ToString());
  }

  public static Dictionary<string, string> GetHeaders(IZContext context, Dictionary<string, string>? extra = null) {
    Dictionary<string, string> ret = new Dictionary<string, string> {
      ["GraphQL-preflight"] = "1",
      [ZHeaders.InstallId] = (context.App as ZClientApp)!.InstallId!,
      [ZHeaders.RequestId] = ModelId.GenerateId()
    };

    var at = context.GetService<IStoredUserSession>();
    if (at?.AccessToken != null) ret[ZHeaders.Authorization] = "bearer " + at.AccessToken;
    else {
      context.Log.Information("No token in {at}", at?.GetType()?.Name);
    }

    if (extra != null) {
      foreach (var key in extra.Keys) {
        ret[key] = extra[key];
      }
    }

    return ret;
  }

  // private static string PrintArgs(params string[] args) => args.Any() ? ("(" + string.Join(", ", args) + ")") : "";
  //
  // private static List<string> PrintObject(
  //   string fieldName, Type mappedType, List<string> args, params Type[] types
  // ) {
  //   List<Type> typeTree = types.ToList();
  //   typeTree.Add(mappedType);
  //   int depth = typeTree.Count;
  //   string spaces = "";
  //   for (int i = 0; i < depth; i++) spaces += "  ";
  //
  //   TuneObjectDescriptor descriptor = TuneApi.GetTuneObjectDescriptor(mappedType);
  //   string braces = descriptor.IsScalar ? "" : " {";
  //   List<string> lines = new List<string>() {
  //     $"{spaces}{fieldName}{PrintArgs(args.ToArray())}{braces}",
  //   };
  //   if (!descriptor.IsScalar && depth < 10) {
  //     foreach (string childField in descriptor.FieldMap.Keys) {
  //       Type childType = descriptor.FieldMap[childField];
  //       if (typeTree.Contains(childType)) continue; // Prevent type recursion
  //       lines.AddRange(PrintObject(childField, descriptor.FieldMap[childField],
  //         new List<string>(), typeTree.ToArray()));
  //     }
  //     lines.Add($"{spaces}}}");
  //   }
  //
  //   return lines;
  // }

}
