#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.Resolvers;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using HotChocolate.Utilities;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Api.Fragments;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Schema.Conventions;
using IZ.Schema.Errors;
using IZ.Schema.Loaders;
using IZ.Schema.Queries;
using IZ.Schema.Types;
using IZ.Schema.Variables;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Schema;

public static class ZSchema {
  public static IServiceCollection AddSchemaServices(this IServiceCollection services) => services
    .AddTransient<IZResolver, ZSchemaResolver>()
    // .AddTransient<IScoreProcessor, ScoreProcessor>()
    .AddSingleton<INamingConventions, ZNamingConventions>()
    .AddSingleton<IChangeTypeProvider, ZTypeConverter>()
    // .AddSingleton<ITypeConverter, ZTypeConverter>()
    // .AddSingleton(new InputFormatter(new ZTypeConverter()))
    // .AddSingleton(new InputParser(new ZTypeConverter()))
    .AddSingleton<ITypeInspector, ZDataTypeInspector>()
    .AddSingleton<DataLoaderRegistry>()
    .AddSingleton<ZQueryAccessor>()
  ;

  public static IRequestExecutorBuilder AddSchemaQuery(this IRequestExecutorBuilder services, ZApp app) {

    services = services
        .ModifyOptions(options => {
          options.EnableFlagEnums = true;
          options.DefaultBindingBehavior = BindingBehavior.Explicit;
        })
        .AddDiagnosticEventListener<ApiExecutionEventListener>()
        .AddZTypes()
        // .AddProjections()
        .AddFiltering()
        .AddSorting()
        .AddConvention<INamingConventions, ZNamingConventions>()
        .ModifyPagingOptions((opts) => {
          opts.MaxPageSize = 100;
          opts.DefaultPageSize = 10;
          opts.IncludeTotalCount = true;
        })
        .AddErrorFilter<GraphqlErrorFilter>()
        // .SetRequestOptions(_ => new RequestExecutorOptions {
        //   ExecutionTimeout = TimeSpan.FromMinutes(5)
        // })
        .UsePersistedOperationPipeline()
        .ConfigureSchemaServices(s => {
          s
            .AddSingleton<IFragmentProvider>(app.Fragments)
            .AddSingleton<ZQueryAccessor>()
            .AddSingleton<IOperationDocumentStorage, ZQueryAccessor>();
        })
      ;

    return services;
  }


  public static IObjectFieldDescriptor AddApiAuthorization(this IObjectFieldDescriptor field, ApiAuthorizeAttribute auth) {
    // ZEnv.Log.Information("[AUTH] {name} with {@auth}", field.ToString(), auth);
    if (auth.IsDefault) field = field.Authorize();
    if (auth.Roles.Any()) field = field.Authorize(auth.Roles.Select(r => r.ToString()).ToArray());
    if (auth.Policy.HasValue) field = field.Authorize(auth.Policy.Value.ToString());
    return field;
  }

  public static Type GetZSchemaType(Type t, Type? generic, bool isOptional = false) =>
    GetZSchemaType(ZTypeDescriptor.FromType(t, isOptional), generic);

  private static Type GetZSchemaType(ZTypeDescriptor descriptor, Type? generic) {
    ZEnv.Log.Verbose("[TYPE] finalize {t}", descriptor);
    if (descriptor.ObjectDescriptor.ObjectType != typeof(string) && descriptor.ObjectDescriptor.ObjectType.IsEnum) {
      generic = typeof(ZEnumType<>);
    } else if (descriptor.ObjectDescriptor.IsScalar) return descriptor.OrigType;
    var schemaType = descriptor.ObjectDescriptor.ObjectType;
    if (descriptor.ObjectDescriptor.IsFile) {
      if (generic != typeof(ZInputType<>)) throw new ArgumentException($"{descriptor} found on {generic} (not an input type)");
      schemaType = typeof(UploadType);
    } else if (generic != null) {
      schemaType = generic.MakeGenericType(descriptor.ObjectDescriptor.ObjectType);
    }
    if (descriptor.IsList) {
      if (!descriptor.IsNullableInner) schemaType = typeof(NonNullType<>).MakeGenericType(schemaType);
      schemaType = typeof(ListType<>).MakeGenericType(schemaType);
    }
    if (!descriptor.IsNullableOuter) schemaType = typeof(NonNullType<>).MakeGenericType(schemaType);
    // if (task) t = typeof(Task<>).MakeGenericType(t);
    return schemaType;
  }

  private static IRequestExecutorBuilder AddZTypes(
    this IRequestExecutorBuilder descriptor
  ) {
    descriptor = descriptor
      .AddType<UploadType>()
      .AddTypeConverter<ZTypeConverter>()
      .AddType<UnsignedIntType>()
      .BindRuntimeType<uint, UnsignedIntType>()
      .AddType<UnsignedLongType>()
      .BindRuntimeType<ulong, UnsignedLongType>()
      .AddType<UnsignedShortType>()
      .BindRuntimeType<ushort, UnsignedShortType>()
      .AddQueryType<ZQueryType>()
      .AddMutationType<ZMutationType>()
      .AddSubscriptionType<ZSubscriptionType>()
      .AddType<ZModelIdType>();

    List<ZObjectDescriptor> types = ZObjectDescriptor.ObjectTypes.Values.ToList();
    foreach (var t in types) {
      if (t.ObjectType == typeof(IFileUpload)) continue;
      descriptor = descriptor.AddType(GetZSchemaType(t.ObjectType, typeof(ZObjectType<>)));
    }
    return descriptor;
  }

  public static void AddZRequestProperty(
    this IObjectTypeDescriptor descriptor, ZPropertyDescriptor prop
  ) {
    var field = descriptor.Field(prop.FieldName)
      .Type(GetZSchemaType(prop.FieldTypeDescriptor, typeof(ZObjectType<>)));
    if (prop.Auth != null) field = field.AddApiAuthorization(prop.Auth);
    ZEnv.Log.Verbose("[FIELD] {prop} <{type} />", prop, prop.FieldType);
    field.Resolve((c, ct) => prop.GetValue(c.Parent<object>()));
  }

  public static IObjectFieldDescriptor AddZRequestMethod(
    this IObjectTypeDescriptor descriptor, Func<IResolverContext, ZMethodDescriptor, object?[]?, Task<object?>> resolve, ZMethodDescriptor mi
  ) {
    string fieldName = mi.FieldName;
    var field = descriptor.Field(fieldName);

    foreach (var param in mi.Parameters) {
      var doArg = GetZSchemaType(param.ParameterType, typeof(ZInputType<>), param.IsOptional);
      ZEnv.Log.Verbose("[FUNC] {name}: {arg} ({type} {t2}) = {argType}",
        fieldName, param.FieldName, param.ParameterType, param.IsOptional, doArg.Name);
      field = field.Argument(param.FieldName, m => m.Type(doArg));
    }
    if (mi.Auth != null) field = field.AddApiAuthorization(mi.Auth);
    var doReturn = GetZSchemaType(mi.FieldType, typeof(ZObjectType<>));
    ZEnv.Log.Debug("[FUNC] {name}({@fields}): {t2} / {ret}",
      fieldName, mi.Parameters.Select(p => p.ParameterType), mi.FieldType, doReturn);
    field.Resolve(async resolver => await resolve(resolver, mi, resolver.ResolveInputVariables(mi.Parameters)), doReturn);
    return field;
  }

  private static SubscribeResolverDelegate CreateSubscribeResolver<TMessage>(string topicString)
  {
    return async ctx =>
    {
      var ct = ctx.RequestAborted;
      var receiver = ctx.Service<ITopicEventReceiver>();
      return await receiver.SubscribeAsync<TMessage>(
          topicString,
          null,
          null,
          ct)
        .ConfigureAwait(false);
    };
  }

  private static SubscribeResolverDelegate CreateArgumentSubscribeResolver<TMessage>(
    string topicFormatString)
  {
    return async ctx =>
    {
      var ct = ctx.RequestAborted;
      var arguments = ctx.Selection.Field.Arguments;
      var argumentValues = new object[arguments.Count];

      // first we capture the argument values.
      for (var i = 0; i < arguments.Count; i++)
      {
        argumentValues[i] = ctx.ArgumentValue<object>(arguments[i].Name);
      }

      // next we create from it the topic string.
      var topicString = string.Format(topicFormatString, argumentValues);

      // last we subscribe with the topic string.
      var receiver = ctx.Service<ITopicEventReceiver>();
      return await receiver.SubscribeAsync<TMessage>(
          topicString,
          null,
          null,
          ct)
        .ConfigureAwait(false);
    };
  }

  private static string ResolveTopicString(MethodInfo method) {
    if (method.IsDefined(typeof(TopicAttribute))) {
      return method.GetCustomAttribute<TopicAttribute>()?.Name ?? method.Name;
    }
    return method.Name;
  }

  private static void SubscribeFactory<TMessage>(
    ObjectFieldDefinition fieldDef,
    string topicString)
  {
    var arg = false;

    if (topicString.Contains('{')) {
      for (var i = 0; i < fieldDef.Arguments.Count; i++) {
        var argument = fieldDef.Arguments[i];
        var argumentPlaceholder = $"{{{argument.Name}}}";

        if (topicString.Contains(argumentPlaceholder)) {
          topicString = topicString.Replace(argumentPlaceholder, $"{{{i}}}");
          arg = true;
        }
      }
    }

    if (arg) {
      fieldDef.SubscribeResolver = CreateArgumentSubscribeResolver<TMessage>(topicString);
    } else {
      fieldDef.SubscribeResolver = CreateSubscribeResolver<TMessage>(topicString);
    }
  }

  private static readonly MethodInfo SubscribeFactoryMethod =
    typeof(SubscribeAttribute).GetMethod(nameof(SubscribeFactory), BindingFlags.NonPublic | BindingFlags.Static)!;

  public static void AddZRequestDescriptors<TRequest>(this IObjectTypeDescriptor descriptor, ApiExecutionType et) where TRequest : ZRequestBase {
    Dictionary<Type, Dictionary<string, ZMethodDescriptor>>? apiMethods = ZApi.GetMethodImplementor(et);

    foreach (var t in apiMethods.Keys) {
      List<ZMethodDescriptor> methods = apiMethods[t].Values.ToList();
      foreach (var mi in methods) {
        var field = descriptor.AddZRequestMethod(async (resolver, method, args) => {
          var context = resolver.Services.GetCurrentContext();
          object queryObj = Activator.CreateInstance(t, context)!; // .BeginRequest()
          return await context.ExecuteRequiredTask(async () => {
            var result = (method.Invoke(context, queryObj, args) as IZResult)!;
            return await result.ExecuteObject();
          });
        }, mi);

        if (et == ApiExecutionType.Subscription) {
          field.Extend().OnBeforeNaming((c, fieldDef) => {
            var topicString = fieldDef.Name;// ResolveTopicString();
            var messageType = typeof(string);
            var factory = SubscribeFactoryMethod.MakeGenericMethod(messageType);
            factory.Invoke(null, new object?[] { fieldDef, topicString });
            /*ZEnv.Log.Information("BEFORE NAMING {d}", d.Name);
            var subscribeResolver = member.DeclaringType?.GetMethod(
              With!,
              Public | NonPublic | Instance | Static);

            if (subscribeResolver is null) {
              throw new ArgumentException($"Subscriber resolver not found");
            }

            var map = new Dictionary<ParameterInfo, string>();

            foreach (var argument in d.Arguments) {
              if (argument.Parameter is not null) {
                map[argument.Parameter] = argument.Name;
              }
            }
            c.DescriptorContext.ResolverCompiler.CompileSubscribe(
              subscribeResolver,
              d.SourceType!,
              d.ResolverType,
              map);
            // d.GetParameterExpressionBuilders()*/
          });
        }
      }
    }
  }
}
