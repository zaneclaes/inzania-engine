#region

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using HotChocolate.Transport.Http;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using StrawberryShake.Json;
using StrawberryShake.Transport.Http;

#endregion

namespace IZ.Client;
using Microsoft.Extensions.Configuration;

public static class ClientExtensions {
  public static IServiceCollection AddTuneQueries<TSession, TConn>(this IServiceCollection c, Func<IServiceProvider, TConn> connBuilder)
    where TSession : class, IStoredUserSession where TConn : class, IHttpConnection => c
    .AddSingleton<IStoredUserSession, TSession>()
    .AddSingleton<IServerConnection>(sp => new ZGraphServerConnection(sp.GetCurrentContext()))
    .AddSingleton<IHttpConnection, TConn>(connBuilder) //
    // .AddTuneQuery().Services
    .AddSingleton<IEntityStore, EntityStore>()
    .AddSingleton<IOperationStore>(sp => new OperationStore(sp.GetRequiredService<IEntityStore>()))
    // .AddSingleton<StrawberryShake.IOperationResultBuilder<global::System.Text.Json.JsonDocument, GraphResult>, GraphBuilder>()
    .AddSingleton<IResultPatcher<JsonDocument>, JsonResultPatcher>();

  public static ApplicationStorage ToZApplicationDirectories(this IConfigurationSection dirs, string productName) {
    return new ApplicationStorage(
      productName,
      dirs.GetSection("User").Value!,
      dirs.GetSection("Tmp").Value!,
      dirs.GetSection("wwwroot").Value);
  }

  public static GraphQLHttpRequest ToGraphQLHttpRequest(this OperationRequest request) {
    (string? id, string? name, var document, IReadOnlyDictionary<string, object?>? variables, IReadOnlyDictionary<string, object?>? extensions, _, IReadOnlyDictionary<string, Upload?>? files, _) = request;

#if NETSTANDARD2_0
    string? body = Encoding.UTF8.GetString(document.Body.ToArray());
#else
        var body = Encoding.UTF8.GetString(document.Body);
#endif

    bool hasFiles = files is {Count: > 0};

    variables = MapVariables(variables);
    if (hasFiles && variables is not null) {
      variables = MapFilesToVariables(variables, files!);
    }

    var operation =
      new HotChocolate.Transport.OperationRequest(body, id, name, variables, extensions);

    return new GraphQLHttpRequest(operation) {
      EnableFileUploads = hasFiles
    };
  }

  /// <summary>
  /// Converts the variables into a dictionary that can be serialized. This is necessary
  /// because the variables can contain lists of key value pairs which are not supported
  /// by HotChocolate.Transport.Http
  /// </summary>
  /// <remarks>
  /// We only convert the variables if necessary to avoid unnecessary allocations.
  /// </remarks>
  private static IReadOnlyDictionary<string, object?>? MapVariables(
    IReadOnlyDictionary<string, object?> variables) {
    if (variables.Count == 0) {
      return null;
    }

    Dictionary<string, object?>? copy = null;
    foreach (KeyValuePair<string, object?> variable in variables) {
      object? value = variable.Value;
      // the value can be a List<T> of key value pairs and not only a dictionary. We do expect
      // to just have lists here, but in case we have a dictionary this should also just work.
      if (value is IEnumerable<KeyValuePair<string, object?>> items) {
        copy ??= CreateDictionary(variables);

        value = MapVariables(CreateDictionary(items));
      } else if (value is List<object?> list) {
        // the lists are mutable so we can just update the value in the list
        MapVariables(list);
      }

      if (copy is not null) {
        copy[variable.Key] = value;
      }
    }

    return copy ?? variables;
  }

  private static void MapVariables(List<object?> variables) {
    if (variables.Count == 0) {
      return;
    }

    for (int index = 0; index < variables.Count; index++) {
      switch (variables[index]) {
        case IEnumerable<KeyValuePair<string, object?>> items:
          variables[index] = MapVariables(CreateDictionary(items));
          break;

        case List<object?> list:
          MapVariables(list);
          break;
      }
    }
  }

  private static Dictionary<string, object?> CreateDictionary(
    IEnumerable<KeyValuePair<string, object?>> values) {
#if NETSTANDARD2_0
    Dictionary<string, object?> dictionary = new Dictionary<string, object?>();

    foreach (KeyValuePair<string, object?> value in values) {
      dictionary[value.Key] = value.Value;
    }

    return dictionary;
#else
        return new Dictionary<string, object?>(values);
#endif
  }

  private static IReadOnlyDictionary<string, object?> MapFilesToVariables(
    IReadOnlyDictionary<string, object?> variables,
    IReadOnlyDictionary<string, Upload?> files) {
    foreach (KeyValuePair<string, Upload?> file in files) {
      string? path = file.Key;
      Upload? upload = file.Value;

      if (!upload.HasValue) {
        continue;
      }

      string? currentPath = path.Substring("variables.".Length);
      object? currentObject = variables;
      int index;
      while ((index = currentPath.IndexOf('.')) >= 0) {
        string? segment = currentPath.Substring(0, index);
        switch (currentObject) {
          case Dictionary<string, object> dictionary:
            if (!dictionary.TryGetValue(segment, out currentObject)) {
              throw new InvalidOperationException(
                string.Format("File map does not match {0}", path));
            }

            break;

          case List<object> array:
            if (!int.TryParse(segment, out int arrayIndex)) {
              throw new InvalidOperationException(
                string.Format("File map does not match {0}", path));
            }

            if (arrayIndex >= array.Count) {
              throw new InvalidOperationException(
                string.Format("File map does not match {0}", path));
            }

            currentObject = array[arrayIndex];
            break;

          default:
            throw new InvalidOperationException(
              string.Format("File map does not match {0}", path));
        }

        currentPath = currentPath.Substring(index + 1);
      }

      switch (currentObject) {
        case Dictionary<string, object> result:
          result[currentPath] =
            new FileReference(upload.Value.Content, upload.Value.FileName);
          break;

        case List<object> array:
          if (!int.TryParse(currentPath, out int arrayIndex)) {
            throw new InvalidOperationException(
              string.Format("File map does not match {0}", path));
          }

          if (arrayIndex >= array.Count) {
            throw new InvalidOperationException(
              string.Format("File map does not match {0}", path));
          }

          array[arrayIndex] =
            new FileReference(upload.Value.Content, upload.Value.FileName);

          break;

        default:
          throw new InvalidOperationException(
            string.Format("File map does not match {0}", path));
      }
    }

    return variables;
  }
}
