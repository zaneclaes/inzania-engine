#region

using System;
using System.Collections.Generic;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Api.Types;

public abstract class ZFieldDescriptor : IAmInternal {

  public readonly bool EnforceOptional;

  public ZTypeDescriptor FieldTypeDescriptor => _apiType ??= _typeMap.LoadTypeDescriptor(FieldType, EnforceOptional);
  protected readonly IZTypeMap _typeMap;
  private ZTypeDescriptor? _apiType;

  // private MemberInfo _member;

  protected ZFieldDescriptor(IZTypeMap typeMap, Type fieldType, HashSet<string?>? formats = null, IApiAuthorize? auth = null, bool enforceOptional = false) {
    _typeMap = typeMap;
    FieldType = fieldType;
    Formats = formats ?? new HashSet<string?>();
    Auth = auth;
    EnforceOptional = enforceOptional;
  }

  protected ZFieldDescriptor(IZTypeMap typeMap, MemberInfo member, Type fieldType, bool enforceOptional = false) {
    _typeMap = typeMap;
    // _member = member;
    FieldType = fieldType;
    Formats = member.GetCustomAttribute<ApiFormatAttribute>()?.FormatTags ?? new HashSet<string?>();
    Auth = member.GetCustomAttribute<ApiAuthorizeAttribute>();
    EnforceOptional = enforceOptional;
  }
  public string Name { get; protected set; } = null!;

  public string FieldName { get; protected set; } = null!;

  public Type FieldType { get; }

  public HashSet<string?> Formats { get; set; }

  public IApiAuthorize? Auth { get; }

  protected virtual IEnumerable<ZTypeDescriptor> GetTypeDescriptors() {
    yield return FieldTypeDescriptor;
  }

  public List<ZTypeDescriptor> ExpandTypes(ISet<ZTypeDescriptor> breadcrumbs) {
    List<ZTypeDescriptor>? ret = null;

    foreach (var desc in GetTypeDescriptors()) {
      // HashSet-style membership: O(1) average
      if (breadcrumbs.Add(desc)) {
        (ret ??= new List<ZTypeDescriptor>(4)).Add(desc);
      }
    }

    return ret ?? _sEmpty;
  }

  private static readonly List<ZTypeDescriptor> _sEmpty = new();

  public override string ToString() => $"{FieldName}: {FieldTypeDescriptor}";
}
