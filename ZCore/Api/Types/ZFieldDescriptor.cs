#region

using System;
using System.Collections.Generic;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Api.Types;

public abstract class ZFieldDescriptor : IAmInternal {
  public string Name { get; protected set; } = null!;

  public string FieldName { get; protected set; } = null!;

  public Type FieldType { get; }

  public ZTypeDescriptor FieldTypeDescriptor => _apiType ??= ZTypeDescriptor.FromType(FieldType, _enforceOptional);
  private ZTypeDescriptor? _apiType;

  private readonly bool _enforceOptional = false;

  public HashSet<string?> Formats { get; }

  public ApiAuthorizeAttribute? Auth { get; }

  // private MemberInfo _member;

  protected ZFieldDescriptor(MemberInfo member, Type fieldType, bool enforceOptional = false) {
    // _member = member;
    FieldType = fieldType;
    Formats = member.GetCustomAttribute<ApiFormatAttribute>()?.FormatTags ?? new HashSet<string?>();
    Auth = member.GetCustomAttribute<ApiAuthorizeAttribute>();
    _enforceOptional = enforceOptional;
  }

  protected virtual List<ZTypeDescriptor> GetTypeDescriptors() =>
    new List<ZTypeDescriptor> {
      FieldTypeDescriptor
    };

  public List<ZTypeDescriptor> ExpandTypes(List<ZTypeDescriptor> breadcrumbs) {
    ZEnv.Log.Debug("[EXPAND] {type}", this);
    List<ZTypeDescriptor> ret = new List<ZTypeDescriptor>();
    foreach (var desc in GetTypeDescriptors()) {
      if (!breadcrumbs.Contains(desc)) {
        ZEnv.Log.Debug("[ADD] {type} from {t}", desc, this);
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
