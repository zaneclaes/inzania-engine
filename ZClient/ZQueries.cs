#region

using System;
using System.Collections.Generic;
using IZ.Client.Networking.WebSockets;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data;
using StrawberryShake;
using SocketBuilder = System.Func<
  IZ.Core.Contexts.IZContext, System.Uri, string, System.Collections.Generic.Dictionary<string, string>?, IZ.Client.Networking.WebSockets.IWebSocket>;

#endregion

namespace IZ.Client;

public static class ZQueries {
  private static SocketBuilder _webSocketBuilder = (context, url, protocol, headers) => new SystemWebSocket();

  public static void SetSocketBuilder(SocketBuilder b) {
    _webSocketBuilder = b;
  }

  public static IWebSocket CreateWebSocket(IZContext context, Uri url, string subprotocols, Dictionary<string, string>? headers = null) {
    context.Log.Information("[WS] creating web socket...");
    return _webSocketBuilder.Invoke(context, url, subprotocols, GetHeaders(context, headers));
  }

  public static OperationKind ToOperationKind(this ApiExecutionType executionType) {
    if (executionType == ApiExecutionType.Query) return OperationKind.Query;
    if (executionType == ApiExecutionType.Mutation) return OperationKind.Mutation;
    if (executionType == ApiExecutionType.Subscription) return OperationKind.Subscription;
    throw new ArgumentException(executionType.ToString());
  }

  public static Dictionary<string, string> GetHeaders(IZContext context, Dictionary<string, string>? extra = null) {
    Dictionary<string, string> ret = new Dictionary<string, string> {
      ["GraphQL-preflight"] = "1",
      [ZHeaders.ClientId] = (context.App as ZClientApp)!.ClientId!,
      [ZHeaders.ApplicationVersion] = (context.App as ZClientApp)!.Version?.ToString() ?? "0.0.0",
      [ZHeaders.RequestId] = ModelId.GenerateId(),
      [ZHeaders.Env] = context.App.Env.ToString()
    };

    var at = context.GetService<IIdentityStore>()?.StoredSession;
    if (at?.AccessToken != null) ret[ZHeaders.Authorization] = "bearer " + at.AccessToken;

    if (extra != null) {
      foreach (string key in extra.Keys) {
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
