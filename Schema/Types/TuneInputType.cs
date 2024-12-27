#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Utils;
using IZ.Schema.Variables;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Schema.Types;

public class TuneInputType<TData> : InputObjectType<TData> where TData : ApiObject {
  protected override void Configure(IInputObjectTypeDescriptor<TData> descriptor) {
    IZEnv.Log.Verbose("[IN] {type}", typeof(TData));
    var tuneObjectDescriptor = TuneTypeDescriptor.FromType(typeof(TData));
    foreach (string inputName in tuneObjectDescriptor.ObjectDescriptor.Inputs.Keys) {
      var prop = tuneObjectDescriptor.ObjectDescriptor.Inputs[inputName];
      IZEnv.Log.Verbose("[IN] [{type}] {arg} = {type}", typeof(TData), inputName, prop.FieldType);

      var t = TuneSchema.GetTuneSchemaType(prop.FieldType, typeof(TuneInputType<>));
      // if (!TuneApi.GetTuneObjectDescriptor(prop.PropertyType).IsScalar)
      //   t = typeof(InputObjectType<>).MakeGenericType(t);

      descriptor.Field(inputName).Type(t);
    }
    base.Configure(descriptor);
  }
}

public static class TuneInputTypes {

  private static object? ResolveInputVariable(ITuneContext context, TuneTypeDescriptor paramType, ISyntaxNode node) {
    if (node is ListValueNode list) {
      var input = (IList) Activator.CreateInstance(paramType.OrigType)!;
      foreach (var val in list.Items) {
        input.Add(ResolveInputVariable(context, paramType, val));
      }
      return input;
    }
    if (paramType.ObjectDescriptor.ObjectType.HasAssignableType<IFileUpload>()) {
      return new TuneFileUpload(((FileValueNode) node)!.Value);
    }
    if (paramType.ObjectDescriptor.IsScalar) {
      var valueNode = node as IValueNode ?? throw new ArgumentException($"{node.GetType().Name} is not a value node");
      return paramType.ObjectDescriptor.ConvertValue(valueNode.Value?.ToString());
    }
    List<ISyntaxNode> syntaxNodes = node.GetNodes().ToList();
    if (!syntaxNodes.Any()) return null;
    // IZEnv.Log.Information("{count} nodes in {type}", syntaxNodes.Count, paramType.ObjectDescriptor.ObjectType);
    object ret = Activator.CreateInstance(paramType.ObjectDescriptor.ObjectType)!;
    if (ret is ContextualObject co) co.Context = context;

    foreach (var n in syntaxNodes) {
      if (n is ObjectFieldNode of) {
        if (paramType.ObjectDescriptor.Inputs.TryGetValue(of.Name.Value, out var a)) {
          object? paramVal = ResolveInputVariable(context, a.FieldTypeDescriptor, of.Value);
          paramType.ObjectDescriptor.Inputs[of.Name.Value].SetValue(ret, paramVal);
        } else {
          throw new ArgumentException($"{of.Name.Value} {of.Value.Kind} missing on {paramType.ObjectDescriptor.TypeName}");
        }
      } else {
        IZEnv.Log.Error("[PARAM] {type}: {str}", n.Kind, n.GetType().Name);
        throw new ArgumentException($"PARAM {n.Kind} is unknown");
      }
    }
    return ret;
  }

  // public static Dictionary<string, ApiVariableValueOrLiteral>? ResolveInputVariables(
  //   ITuneContext context, Func<string, object?> getValue, List<TuneParameterDescriptor> pars
  // ) {
  //   if (!pars.Any()) return null;
  //
  //   return context.ExecuteOptional(() => {
  //     Dictionary<string, ApiVariableValueOrLiteral> args = new Dictionary<string, ApiVariableValueOrLiteral>();
  //     foreach (var parameterInfo in pars) {
  //       var node = resolver.ArgumentLiteral<IValueNode>(parameterInfo.Name!.ToFieldName());
  //       var obj = getValue(parameterInfo.FieldName);
  //       var apiVar = new ApiVariableValueOrLiteral(new ApiInputType(TypeKind.Object, parameterInfo.ParameterType), obj, node);
  //
  //       context.Log.Debug("[PARAM] {pt} = {@p} = {@param}", parameterInfo.FieldName, node, obj);
  //       args.Add(parameterInfo.FieldName, apiVar);
  //     }
  //     return args;
  //   });
  // }

  public static Dictionary<string, ApiVariableValueOrLiteral>? ResolveInputVariables(
    ITuneContext context, Func<string, IValueNode?> getValue, List<TuneParameterDescriptor> pars
  ) {
    if (!pars.Any()) return null;

    return context.ExecuteOptional(() => {
      Dictionary<string, ApiVariableValueOrLiteral> args = new Dictionary<string, ApiVariableValueOrLiteral>();
      foreach (var parameterInfo in pars) {
        // var node = resolver.ArgumentLiteral<IValueNode>(parameterInfo.Name!.ToFieldName());
        var node = getValue(parameterInfo.FieldName);
        if (node == null) {
          context.Log.Verbose("[PAR] NULL {pt}", parameterInfo.FieldName);
          continue;
        }

        var paramType = TuneTypeDescriptor.FromType(parameterInfo.ParameterType);
        object? obj = node.Kind == SyntaxKind.NullValue ? parameterInfo.DefaultValue : ResolveInputVariable(context, paramType, node);

        var apiVar = new ApiVariableValueOrLiteral(new ApiInputType(TypeKind.Object, parameterInfo.ParameterType), obj, node);

        context.Log.Debug("[PARAM] {pt} = {@p} = {@param}", parameterInfo.FieldName, node, obj);
        args.Add(parameterInfo.FieldName, apiVar);
      }
      return args;
    });
  }

  public static object?[]? ResolveInputVariables(this IResolverContext resolver, List<TuneParameterDescriptor> pars) {
    var context = resolver.Services.GetRootContext();
    return ResolveInputVariables(context, resolver.ArgumentLiteral<IValueNode>, pars)?.Values
      .Select(v => v.Value).ToArray();
  }
}
