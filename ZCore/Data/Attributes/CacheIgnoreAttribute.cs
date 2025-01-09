#region

using System;

#endregion

namespace IZ.Core.Data.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class CacheIgnoreAttribute : Attribute { }
