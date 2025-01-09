#region

using System;
using System.Collections.Generic;
using IZ.Client.Networking.Sockets;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using SocketBuilder = System.Func<
  string, System.Collections.Generic.Dictionary<string, string>, IZ.Client.Networking.Sockets.ISocket>;

#endregion

namespace IZ.Client;

public static class TuneQueries {
  private static SocketBuilder _socketBuilder = (url, headers) => new SystemSocket();

  public static void SetSocketBuilder(SocketBuilder b) {
    _socketBuilder = b;
  }

  public static ISocket CreateSocket(string url, Dictionary<string, string> headers) => _socketBuilder.Invoke(url, headers);

  public static OperationKind ToOperationKind(this ApiExecutionType executionType) {
    if (executionType == ApiExecutionType.Query) return OperationKind.Query;
    if (executionType == ApiExecutionType.Mutation) return OperationKind.Mutation;
    throw new ArgumentException(executionType.ToString());
  }

  public static Dictionary<string, string> GetHeaders(IZContext context) {
    Dictionary<string, string>? ret = new Dictionary<string, string> {
      ["GraphQL-preflight"] = "1",
      ["InstallId"] = (context.App as ZClientApp)!.InstallId!,
      ["X-Request-ID"] = ModelId.GenerateId()
    };

    var at = context.ServiceProvider.GetService<IStoredUserSession>();
    if (at?.AccessToken != null) ret["Authorization"] = "bearer " + at.AccessToken;
    else {
      context.Log.Information("No token in {at}", at?.GetType()?.Name);
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
