#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Auth;

#endregion

namespace IZ.Core.Data.Attributes;

public interface IApiAuthorize {
  public List<ZUserRole> Roles { get; }

  public ZPolicy? Policy { get; }

  public bool IsDefault => Policy == null && !Roles.Any();

  public string GetSource() {
    var zp = Policy.HasValue ? $"ZPolicy.{Policy}" : "ZPolicy.None";
    var roles = !Roles.Any() ? "" : $", IZ.Core.Auth.ZUserRole." + string.Join(", IZ.Core.Auth.ZUserRole.", Roles);
    return $"new ApiAuthorizeAttribute({zp}{roles})";
  }
}

[AttributeUsage(validOn: AttributeTargets.Property | AttributeTargets.Method)]
public class ApiAuthorizeAttribute : Attribute, IApiAuthorize {

  public ApiAuthorizeAttribute(ZPolicy policy = ZPolicy.VirtualUser, params ZUserRole[] allowedRoles) {
    Policy = policy == ZPolicy.None ? null : policy;
    Roles = allowedRoles.ToList();
  }
  public List<ZUserRole> Roles { get; }

  public ZPolicy? Policy { get; }
}
