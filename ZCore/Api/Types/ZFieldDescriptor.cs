#region

using System;
using System.Collections.Generic;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Api.Types;

public abstract class ZFieldDescriptor : IAmInternal {
  public string FieldName { get; protected set; } = default!;

  public abstract Type FieldType { get; }

  public ZTypeDescriptor FieldTypeDescriptor => _apiType ??= ZTypeDescriptor.FromType(FieldType, IsOptional);
  private ZTypeDescriptor? _apiType;

  public bool IsOptional { get; protected set; }

  public HashSet<string?> Formats { get; }

  public ApiAuthorizeAttribute? Auth { get; }

  // private MemberInfo _member;

  public ZFieldDescriptor(MemberInfo member) {
    // _member = member;
    Formats = member.GetCustomAttribute<ApiFormatAttribute>()?.FormatTags ?? new HashSet<string?>();
    Auth = member.GetCustomAttribute<ApiAuthorizeAttribute>();
  }

  protected virtual List<ZTypeDescriptor> GetTypeDescriptors() =>
    new List<ZTypeDescriptor> {
      FieldTypeDescriptor
    };

  public List<ZTypeDescriptor> ExpandTypes(List<ZTypeDescriptor> breadcrumbs) {
    ZEnv.Log.Verbose("[EXPAND] {type}", this);
    List<ZTypeDescriptor> ret = new List<ZTypeDescriptor>();
    foreach (var desc in GetTypeDescriptors()) {
      if (!breadcrumbs.Contains(desc)) {
        ZEnv.Log.Debug("[ADD] {type} from {t}", desc, desc.ObjectDescriptor);
        ret.Add(desc);
        breadcrumbs.Add(desc);
      } else {
        ZEnv.Log.Verbose("[EXIST] {type}", desc);
      }
    }
    return ret;
  }

  public override string ToString() => $"{FieldName}: {FieldTypeDescriptor}";
}
