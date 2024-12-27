#region

using System;
using System.Collections.Generic;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Api.Types;

public abstract class TuneFieldDescriptor : IAmInternal {
  public string FieldName { get; protected set; } = default!;

  public abstract Type FieldType { get; }

  public TuneTypeDescriptor FieldTypeDescriptor => _apiType ??= TuneTypeDescriptor.FromType(FieldType, IsOptional);
  private TuneTypeDescriptor? _apiType;

  public bool IsOptional { get; protected set; }

  public HashSet<string?> Formats { get; }

  public ApiAuthorizeAttribute? Auth { get; }

  // private MemberInfo _member;

  public TuneFieldDescriptor(MemberInfo member) {
    // _member = member;
    Formats = member.GetCustomAttribute<ApiFormatAttribute>()?.FormatTags ?? new HashSet<string?>();
    Auth = member.GetCustomAttribute<ApiAuthorizeAttribute>();
  }

  protected virtual List<TuneTypeDescriptor> GetTypeDescriptors() =>
    new List<TuneTypeDescriptor> {
      FieldTypeDescriptor
    };

  public List<TuneTypeDescriptor> ExpandTypes(List<TuneTypeDescriptor> breadcrumbs) {
    IZEnv.Log.Verbose("[EXPAND] {type}", this);
    List<TuneTypeDescriptor> ret = new List<TuneTypeDescriptor>();
    foreach (var desc in GetTypeDescriptors()) {
      if (!breadcrumbs.Contains(desc)) {
        IZEnv.Log.Debug("[ADD] {type} from {t}", desc, desc.ObjectDescriptor);
        ret.Add(desc);
        breadcrumbs.Add(desc);
      } else {
        IZEnv.Log.Verbose("[EXIST] {type}", desc);
      }
    }
    return ret;
  }

  public override string ToString() => $"{FieldName}: {FieldTypeDescriptor}";
}
