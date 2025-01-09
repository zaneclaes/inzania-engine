#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
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
    // .AddSingleton<ITypeConverter, TuneTypeConverter>()
    // .AddSingleton(new InputFormatter(new TuneTypeConverter()))
    // .AddSingleton(new InputParser(new TuneTypeConverter()))
    .AddSingleton<ITypeInspector, ZDataTypeInspector>()
    .AddSingleton<DataLoaderRegistry>()
    .AddSingleton<ZQueryAccessor>()
  ;

  public static IRequestExecutorBuilder AddSchemaQuery(this IRequestExecutorBuilder services, ZApp app) {

    return services
        .ModifyOptions(options => {
          options.EnableFlagEnums = true;
          options.DefaultBindingBehavior = BindingBehavior.Explicit;
        })
        .AddDiagnosticEventListener<ApiExecutionEventListener>()
        .AddTuneTypes()
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
  }


  public static IObjectFieldDescriptor AddApiAuthorization(this IObjectFieldDescriptor field, ApiAuthorizeAttribute auth) {
    // ZEnv.Log.Information("[AUTH] {name} with {@auth}", field.ToString(), auth);
    if (auth.IsDefault) field = field.Authorize();
    if (auth.Roles.Any()) field = field.Authorize(auth.Roles.Select(r => r.ToString()).ToArray());
    if (auth.Policy.HasValue) field = field.Authorize(auth.Policy.Value.ToString());
    return field;
  }

  public static Type GetTuneSchemaType(Type t, Type? generic, bool isOptional = false) =>
    GetTuneSchemaType(ZTypeDescriptor.FromType(t, isOptional), generic);

  public static Type GetTuneSchemaType(ZTypeDescriptor descriptor, Type? generic) {
    // TuneTypeDescriptor descriptor = TuneApi.GetTuneApiType(t, isOptional);
    ZEnv.Log.Verbose("[TYPE] finalize {t}", descriptor);
    if (descriptor.ObjectDescriptor.ObjectType != typeof(string) && descriptor.ObjectDescriptor.ObjectType.IsEnum) {
      generic = typeof(ZEnumType<>);
    } else if (descriptor.ObjectDescriptor.IsScalar) return descriptor.OrigType;
    var schemaType = descriptor.ObjectDescriptor.ObjectType;
    if (descriptor.ObjectDescriptor.IsFile) {
      if (generic != typeof(ZInputType<>)) throw new ArgumentException($"IFileUpload found on {generic} (not an input type)");
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

  public static IRequestExecutorBuilder AddTuneTypes(
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
      .AddType<ZModelIdType>();

    List<ZObjectDescriptor> types = ZObjectDescriptor.ObjectTypes.Values.ToList();
    foreach (var t in types) {
      descriptor = descriptor.AddType(GetTuneSchemaType(t.ObjectType, typeof(ZObjectType<>)));
    }
    return descriptor;
  }

  public static void AddTuneRequestProperty(
    this IObjectTypeDescriptor descriptor, ZPropertyDescriptor prop
  ) {
    var field = descriptor.Field(prop.FieldName)
      .Type(GetTuneSchemaType(prop.FieldTypeDescriptor, typeof(ZObjectType<>)));
    if (prop.Auth != null) field = field.AddApiAuthorization(prop.Auth);
    ZEnv.Log.Verbose("[FIELD] {prop} <{type} />", prop, prop.FieldType);
    field.Resolve((c, ct) => prop.GetValue(c.Parent<object>()));
  }

  public static void AddTuneRequestMethod(
    this IObjectTypeDescriptor descriptor, Func<IResolverContext, ZMethodDescriptor, object?[]?, Task<object?>> resolve, ZMethodDescriptor mi
  ) {
    string fieldName = mi.FieldName;
    var field = descriptor.Field(fieldName);

    foreach (var param in mi.Parameters) {
      var doArg = GetTuneSchemaType(param.ParameterType, typeof(ZInputType<>), param.IsOptional);
      ZEnv.Log.Verbose("[FUNC] {name}: {arg} ({type} {t2}) = {argType}",
        fieldName, param.FieldName, param.ParameterType, param.IsOptional, doArg.Name);
      field = field.Argument(param.FieldName, m => m.Type(doArg));
    }
    if (mi.Auth != null) field = field.AddApiAuthorization(mi.Auth);
    var doReturn = GetTuneSchemaType(mi.FieldType, typeof(ZObjectType<>));
    ZEnv.Log.Debug("[FUNC] {name}({@fields}): {t2} / {ret}",
      fieldName, mi.Parameters.Select(p => p.ParameterType), mi.FieldType, doReturn);
    field.Resolve(async resolver => await resolve(resolver, mi, resolver.ResolveInputVariables(mi.Parameters)), doReturn);
  }

  public static void AddTuneRequestDescriptors<TRequest>(this IObjectTypeDescriptor descriptor, ApiExecutionType et) where TRequest : ZRequestBase {
    Dictionary<Type, Dictionary<string, ZMethodDescriptor>>? apiMethods = ZApi.GetMethodImplementor(et);

    foreach (var t in apiMethods.Keys) {
      List<ZMethodDescriptor> methods = apiMethods[t].Values.ToList();
      foreach (var mi in methods)
        descriptor.AddTuneRequestMethod(async (resolver, method, args) => {
          var context = resolver.Services.GetCurrentContext();
          object queryObj = Activator.CreateInstance(t, context)!; // .BeginRequest()
          return await context.ExecuteRequiredTask(async () => {
            var result = (method.Invoke(queryObj, args) as IZResult)!;
            return await result.ExecuteObject();
          });
        }, mi);
    }
  }
}
