#region

using System;

#endregion

namespace IZ.Core.Data.Attributes;

/// <summary>
///   Properties with this flag are not available via the Furballs API.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ApiIgnoreAttribute : Attribute { }
