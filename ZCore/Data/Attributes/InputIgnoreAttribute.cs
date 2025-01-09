#region

using System;

#endregion

namespace IZ.Core.Data.Attributes;

[AttributeUsage(AttributeTargets.Property
                | AttributeTargets.Method | AttributeTargets.Field)]
public class InputIgnoreAttribute : Attribute { }
