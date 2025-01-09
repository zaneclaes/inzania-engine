#region

using System.Linq;
using System.Reflection;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Schema.Types;

public class ZDataTypeInspector : DefaultTypeInspector, ITypeInspector {
  public override bool IsMemberIgnored(MemberInfo member) {
    ZEnv.Log.Verbose("[MEMBER] {type} {t2}", member.DeclaringType, member.ReflectedType);
    return base.IsMemberIgnored(member) || member.IsDefined(typeof(ApiIgnoreAttribute)) ||
           (member.DeclaringType?.IsAssignableTo(typeof(IAmInternal)) ?? false);
  }
  //
  // public override IExtendedType GetReturnType(MemberInfo member, bool ignoreAttributes = false) {
  //   IExtendedType et = base.GetReturnType(member, ignoreAttributes);
  //   if (member.IsDefined(typeof(ApiAuthorizeAttribute))) {
  //     ApiAuthorizeAttribute auth = member.GetCustomAttribute<ApiAuthorizeAttribute>()!;
  //     et.Ob
  //     ZEnv.Log.Information("[ET] {name} = {@auth}", member.Name, et);
  //   }
  //   return et;
  // }

  /// <inheritdoc />
  public new void ApplyAttributes(
    IDescriptorContext context,
    IDescriptor descriptor,
    ICustomAttributeProvider attributeProvider) {
    object[]? attributes = attributeProvider.GetCustomAttributes(true);
    if (descriptor is ObjectFieldDescriptor ofd &&
        attributes.FirstOrDefault(a => a is ApiAuthorizeAttribute) is ApiAuthorizeAttribute auth) {
      // ZEnv.Log.Information("[ATTR] {ctxt} {@desc}", descriptor.GetType().Name, attr);
      descriptor = ofd.AddApiAuthorization(auth);
    }
    base.ApplyAttributes(context, descriptor, attributeProvider);
  }

  protected override void Initialize(IConventionContext context) {
    // do not call the base class, so it does not throw an exception.
    if (!IsInitialized) base.Initialize(context);
  }
}
