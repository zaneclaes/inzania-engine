#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

public class ZMethodDescriptor : ZFieldDescriptor {

  protected ZMethodDescriptor(
    string opName, ApiExecutionType executionType, List<ZParameterDescriptor> parameters,
    Type returnType, HashSet<string?>? formats = null, IApiAuthorize? auth = null, bool enforceOptional = false
  ) : base(returnType, formats, auth, enforceOptional) {
    Name = opName;
    ExecutionType = executionType;
    Parameters = parameters.ToList();
    SetFieldName();
  }

  public ZMethodDescriptor(MethodInfo methodInfo) : base(methodInfo, methodInfo.ReturnType, IsMethodReturnNullable(methodInfo)) {
    Method = methodInfo;
    Name = methodInfo.Name;
    Parameters = methodInfo.GetParameters()
      .Select(p => new ZParameterDescriptor(p))
      .ToList();
    // ApiMethod = methodInfo.GetCustomAttribute<ApiMethodAttribute>();

    Name = methodInfo.Name;
    ExecutionType = ApiExecutionType.Query;

    // if (ApiMethod != null) {
    //   ExecutionType = isSet ? ApiExecutionType.Mutation : isGet ? ApiExecutionType.Query : ApiMethod.ExecutionType;
    //   if (ExecutionType != ApiMethod.ExecutionType) {
    //     ZEnv.Log.Warning("[METHOD] {name} was converted from {type} to {exec}", OperationName, ApiMethod.ExecutionType, ExecutionType);
    //   }
    // }
    SetFieldName();
  }

  private void SetFieldName() {
    var name = Name;
    bool isSet = name.StartsWith("Set");
    bool isGet = name.StartsWith("Get");
    if (isSet || isGet) name = name.Substring(3);
    FieldName = name.ToFieldName();
  }

  // public ApiMethodAttribute? ApiMethod { get; }

  public ApiExecutionType ExecutionType { get; }

  public List<ZParameterDescriptor> Parameters { get; }

  private MethodInfo? Method { get; }

  public virtual object? Invoke(IZContext context, object o, params object?[]? args) {
    try {
      return Method!.Invoke(o, args);
    } catch (Exception e) {
      context.Log.Error(e, "Failed to invoke {method} on {type}", Method?.Name, o.GetType());
      throw;
    }
  }

  protected override IEnumerable<ZTypeDescriptor> GetTypeDescriptors() {
    yield return FieldTypeDescriptor;
    foreach (var p in Parameters)
      yield return p.ApiType;
  }

  public static Type StripIgnoredOuterFunctionTypes(Type t) {
    if (t.Name == "Task`1") { // ISAssignableTo(Task<>) seems to not work
      t = t.GenericTypeArguments[0];
    }
    if (t.HasAssignableType(typeof(IZResult))) {
      // ZEnv.Log.Information("T {old} -> {new}", t.Name, t.GenericTypeArguments[0].Name);
      t = t.GenericTypeArguments[0];
    }
    return t;
  }

  private static bool IsMethodReturnNullable(MethodInfo methodInfo) {
    var context = new NullabilityInfoContext();
    var nullability = context.Create(methodInfo.ReturnParameter);
    if (nullability.ReadState == NullabilityState.Nullable) return true;
    var inner = nullability.GenericTypeArguments.FirstOrDefault();
    return inner is {ReadState: NullabilityState.Nullable};
  }

  public string GetSource(string className, Type queryable, string ns) {
    var usings = new HashSet<string>() {queryable.Namespace!};
    var src = GetClassSource(className, queryable.Name, usings);
    return @$"using {string.Join(";\nusing ", usings)};

namespace {ns};

{src}";
  }

  public string GetClassSource(string className, string objectName, HashSet<string> usings) {
    var rt = ZTypeDescriptor.FromType(FieldType.GenericTypeArguments[0]);
    var p = "new List<ZParameterDescriptor>()";
    usings.Add("System");
    usings.Add("System.Collections.Generic");
    usings.Add("IZ.Core.Contexts");
    usings.Add("IZ.Core.Data.Attributes");
    usings.Add("IZ.Core.Api");
    usings.Add("IZ.Core.Api.Types");
    usings.Add("IZ.Core.Auth");
    usings.Add(rt.ObjectDescriptor.ObjectType.Namespace!);
    var args = new List<string>() { };
    if (Parameters.Any()) {
      p += "{\n      " + string.Join(",\n      ", Parameters.Select(p => p.GetSource(usings))) + "}\n    ";
      args = Parameters.Select((p, i) => {
        var pt = ZTypeDescriptor.FromType(p.ParameterType);
        usings.Add(pt.ObjectDescriptor.ObjectType.Namespace!);
        return pt.ToCast($"args![{i}]");
      }).ToList();
    }
    var fm = "new HashSet<string?>()";
    if (Formats.Any()) {
      fm += "{ " + string.Join(", ", Formats.Select(f => f == null ? "null" : $"\"{f}\"")) + " }";
    }
    var auth = Auth == null ? "null" : Auth.GetSource();

    return @$"public class {className} : ZMethodDescriptor {{
  public {className}() : base(
    ""{Name}"", 
    ApiExecutionType.{ExecutionType}, 
    {p}, 
    typeof(IZResult<{rt.ToSystemTypeName()}>),
    {fm},
    {auth},
    {EnforceOptional.ToString().ToLower()}
  ) {{ }}
  
  public override object? Invoke(IZContext context, object o, params object?[]? args) => 
    (o as {objectName} ?? throw new NullReferenceException($""{{o.GetType()}} is not a {objectName}"")).{Name}({string.Join(", ", args)});
}}
";
  }

  public override string ToString() => $"<{Name}: {FieldTypeDescriptor}>";
}
