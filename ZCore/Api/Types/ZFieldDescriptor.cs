#region

using System;
using System.Collections.Generic;
using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Api.Types;

public abstract class ZFieldDescriptor : IAmInternal {

  private readonly bool _enforceOptional;
  private ZTypeDescriptor? _apiType;

  // private MemberInfo _member;

  protected ZFieldDescriptor(MemberInfo member, Type fieldType, bool enforceOptional = false) {
    // _member = member;
    FieldType = fieldType;
    Formats = member.GetCustomAttribute<ApiFormatAttribute>()?.FormatTags ?? new HashSet<string?>();
    Auth = member.GetCustomAttribute<ApiAuthorizeAttribute>();
    _enforceOptional = enforceOptional;
  }
  public string Name { get; protected set; } = null!;

  public string FieldName { get; protected set; } = null!;

  public Type FieldType { get; }

  public ZTypeDescriptor FieldTypeDescriptor => _apiType ??= ZTypeDescriptor.FromType(FieldType, _enforceOptional);

  public HashSet<string?> Formats { get; }

  public ApiAuthorizeAttribute? Auth { get; }

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

    return ret ?? s_empty;
  }

  private static readonly List<ZTypeDescriptor> s_empty = new();


  public override string ToString() => $"{FieldName}: {FieldTypeDescriptor}";
}
