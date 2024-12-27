#region

using System;
using System.Linq;
using System.Reflection;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Schema.Conventions;

public class TuneNamingConventions : DefaultNamingConventions {
  // public override string GetEnumValueName(object value) {
  //   // var enumType = value.GetType();
  //
  //   // if (enumType.IsEnum && enumType.GetCustomAttribute<FlagsAttribute>() != null) {
  //   //   // Encode enums as numbers so that bitmask flags are preserved
  //   //   var str = ((int) value).ToString();
  //   //   string pre = "P";
  //   //   if (str.StartsWith("-")) {
  //   //     pre = "M";
  //   //     str = str.Substring(1);
  //   //   }
  //   //   IZEnv.Log.Information("[VAL] {temp} {str}", pre, str);
  //   //   return pre + str;
  //   // }
  //   return base.GetEnumValueName(value);
  // }

  public override string GetEnumValueName(object value) {
  //   return "E" + ((int) value); // hacky way for GQL to allow integers
  return base.GetEnumValueName(value);
  }

  public override string? GetTypeDescription(Type type, TypeKind kind) {
    if (type is null) {
      throw new ArgumentNullException(nameof(type));
    }

    string? description = type.GetApiDocs();
    return !string.IsNullOrWhiteSpace(description) ? description : base.GetTypeDescription(type, kind);
  }

  public override string? GetMemberDescription(
    MemberInfo member,
    MemberKind kind) {
    // IZEnv.Log.Information("[MEMBER DESC] {name}", member.Name);
    string? description = member.GetApiDocs();
    return !string.IsNullOrWhiteSpace(description) ? description : base.GetMemberDescription(member, kind);
  }

  public override string? GetArgumentDescription(ParameterInfo parameter) {
    // IZEnv.Log.Information("[ARG DESC] {name}", parameter.Name);
    string? description = parameter.GetApiDocs();
    return !string.IsNullOrWhiteSpace(description) ? description : base.GetArgumentDescription(parameter);
  }

  public override string? GetEnumValueDescription(object? value) {
    if (value is null) {
      throw new ArgumentNullException(nameof(value));
    }

    var enumType = value.GetType();
    if (enumType.IsEnum) {
      var enumMember = enumType
        .GetMember(value.ToString()!)
        .FirstOrDefault();

      if (enumMember != null) {
        string? description = enumMember.GetApiDocs();
        return !string.IsNullOrWhiteSpace(description) ? description : base.GetEnumValueDescription(value);
      }
    }
    return null;
  }
}

public static class TuneNamingExtensions {
  public static string? GetApiDocs(
    this ICustomAttributeProvider attributeProvider
  ) {
    object[] attrs = attributeProvider.GetCustomAttributes(false);
    if (attrs.FirstOrDefault(a => a is ApiDocsAttribute) is ApiDocsAttribute apiDocs) {
      return apiDocs.GenerateDescription(attrs);
    }

    return null;
  }
}
