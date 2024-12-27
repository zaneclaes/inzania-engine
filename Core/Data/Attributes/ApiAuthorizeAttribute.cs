#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Auth;

#endregion

namespace IZ.Core.Data.Attributes;

[AttributeUsage(validOn: AttributeTargets.Property | AttributeTargets.Method)]
public class ApiAuthorizeAttribute : Attribute {
  public List<TuneUserRole> Roles { get; }

  public TunePolicy? Policy { get; }

  public bool IsDefault => Policy == null && !Roles.Any();

  public ApiAuthorizeAttribute(TunePolicy policy = TunePolicy.PublicUser, params TuneUserRole[] allowedRoles) {
    Policy = policy == TunePolicy.None ? null : policy;
    Roles = allowedRoles.ToList();
  }
}
