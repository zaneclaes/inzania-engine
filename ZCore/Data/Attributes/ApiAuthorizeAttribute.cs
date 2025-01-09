#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Auth;

#endregion

namespace IZ.Core.Data.Attributes;

[AttributeUsage(validOn: AttributeTargets.Property | AttributeTargets.Method)]
public class ApiAuthorizeAttribute : Attribute {
  public List<ZUserRole> Roles { get; }

  public ZPolicy? Policy { get; }

  public bool IsDefault => Policy == null && !Roles.Any();

  public ApiAuthorizeAttribute(ZPolicy policy = ZPolicy.PublicUser, params ZUserRole[] allowedRoles) {
    Policy = policy == ZPolicy.None ? null : policy;
    Roles = allowedRoles.ToList();
  }
}
