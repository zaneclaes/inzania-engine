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

public class TuneObjectType<TData> : ObjectType<TData> where TData : ApiObject {
  private readonly List<MethodInfo> _methodInfos = new List<MethodInfo>();


  protected override void Configure(IObjectTypeDescriptor<TData> descriptor) {
    IZEnv.Log.Verbose("[OUT] {type}", typeof(TData));

    var tuneDescriptor = TuneTypeDescriptor.FromType(typeof(TData));
    AddProperties(tuneDescriptor, descriptor);
    AddMethods(tuneDescriptor, descriptor);

    base.Configure(descriptor);
  }

  private void AddProperties(TuneTypeDescriptor tuneDescriptor, IObjectTypeDescriptor<TData> descriptor) {
    IZEnv.Log.Debug("[PROPS] {desc}", tuneDescriptor);
    foreach (var desc in tuneDescriptor.ObjectDescriptor.FieldMap.Values) {
      if (!(desc is TunePropertyDescriptor prop)) continue;
      ((IObjectTypeDescriptor) descriptor).AddTuneRequestProperty(prop);
    }
  }

  private void AddMethods(TuneTypeDescriptor tuneDescriptor, IObjectTypeDescriptor<TData> descriptor) {
    foreach (var mi in tuneDescriptor.ObjectDescriptor.Methods.Values) {
      TuneSchema.AddTuneRequestMethod(((IObjectTypeDescriptor) descriptor), async (resolver, method, args) => {
        if (method.ExecutionType != ApiExecutionType.Query && (int) resolver.Operation.Type != (int) method.ExecutionType) {
          throw new ArgumentException($"{tuneDescriptor}.{method.OperationName}() is a {method.ExecutionType}");
        }
        IZEnv.Log.Debug("[EXEC] {name} on {@obj} w {@args}", method.OperationName, resolver.Operation.Type, args);
        object? ret = resolver.Services.GetCurrentContext()
          .ExecuteOptional(() => method.Invoke(resolver.Parent<object>(), args));
        IZEnv.Log.Verbose("[EXEC] {name} done", method.OperationName);
        if (ret is IZResult res) {
          ret = await res.ExecuteObject();
        }
        if (ret is Task task) {
          ret = await task.ExecuteObjectAsync();
        }

        return ret;
      }, mi);
      // IZEnv.Log.Information("[OUT] [{type}] {arg} = {type}", typeof(TData), fieldName, prop.PropertyType);
    }
  }

}
