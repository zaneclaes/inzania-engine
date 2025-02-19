#region

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HotChocolate.Types;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Schema.Types;

public class ZObjectType<TData> : ObjectType<TData> where TData : ApiObject {
  private readonly List<MethodInfo> _methodInfos = new List<MethodInfo>();


  protected override void Configure(IObjectTypeDescriptor<TData> descriptor) {
    ZEnv.Log.Verbose("[OUT] {type}", typeof(TData));

    var zDescriptor = ZTypeDescriptor.FromType(typeof(TData));
    AddProperties(zDescriptor, descriptor);
    AddMethods(zDescriptor, descriptor);

    base.Configure(descriptor);
  }

  private void AddProperties(ZTypeDescriptor zDescriptor, IObjectTypeDescriptor<TData> descriptor) {
    ZEnv.Log.Debug("[PROPS] {desc}", zDescriptor);
    foreach (var desc in zDescriptor.ObjectDescriptor.FieldMap.Values) {
      if (!(desc is ZPropertyDescriptor prop)) continue;
      ((IObjectTypeDescriptor) descriptor).AddZRequestProperty(prop);
    }
  }

  private void AddMethods(ZTypeDescriptor zDescriptor, IObjectTypeDescriptor<TData> descriptor) {
    foreach (var mi in zDescriptor.ObjectDescriptor.Methods.Values) {
      ((IObjectTypeDescriptor) descriptor).AddZRequestMethod(async (resolver, method, args) => {
        if (method.ExecutionType != ApiExecutionType.Query && (int) resolver.Operation.Type != (int) method.ExecutionType) {
          throw new ArgumentException($"{zDescriptor}.{method.OperationName}() is a {method.ExecutionType}");
        }
        ZEnv.Log.Debug("[EXEC] {name} on {@obj} w {@args}", method.OperationName, resolver.Operation.Type, args);
        object? ret = resolver.Services.GetCurrentContext()
          .ExecuteOptional(() => method.Invoke(resolver.Parent<object>(), args));
        ZEnv.Log.Verbose("[EXEC] {name} done", method.OperationName);
        if (ret is IZResult res) {
          ret = await res.ExecuteObject();
        }
        if (ret is Task task) {
          ret = await task.ExecuteObjectAsync();
        }

        return ret;
      }, mi);
      // ZEnv.Log.Information("[OUT] [{type}] {arg} = {type}", typeof(TData), fieldName, prop.PropertyType);
    }
  }

}
