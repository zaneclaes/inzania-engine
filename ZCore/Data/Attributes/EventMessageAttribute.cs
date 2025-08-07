// subscription eventsn require the actual HotChocolate abstraction attribute on the server
// But in Unity, where we don't have the abstractions, we need a fake attribute
#if Z_UNITY
using System;

namespace IZ.Core.Data.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class EventMessageAttribute : Attribute { }
#endif
