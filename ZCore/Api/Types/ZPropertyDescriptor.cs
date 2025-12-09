#region

using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

public class ZPropertyDescriptor : ZFieldDescriptor {
  public bool IsIgnoredForFormat(string? format = null) {
    if (!IsSettable) return true;

    // If it has an explicit separate GetXXX accessor, use THOSE formats
    var checkFormats = ExecutionMethod?.Formats ?? Formats;
    if (checkFormats.Any()) {
      if (IsOutputIgnored && ExecutionMethod == null) throw new SystemException($"{this} is both ignored and formatted");
      return !checkFormats.Contains(format);
    }

    // Only consider the API ignore flag if it was not explicitly included
    if (IsOutputIgnored) return true;

    return !FieldTypeDescriptor.ObjectDescriptor.IsScalar;
  }

  public ZPropertyDescriptor(PropertyInfo propertyInfo, PropertyInfo? parentProp) : base(propertyInfo, propertyInfo.PropertyType, propertyInfo.GetMethod != null && new NullabilityInfoContext()
    .Create(propertyInfo.GetMethod!.ReturnParameter!).ReadState == NullabilityState.Nullable) {

    var jsonPropName = propertyInfo.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
    PropertyInfo = propertyInfo;
    IsInherited = parentProp != null;
    Name = propertyInfo.Name;
    FieldName = jsonPropName ?? propertyInfo.Name.ToFieldName();
    IsSettable = propertyInfo.CanWrite;
    Order = propertyInfo.GetCustomAttribute<ApiOrderAttribute>()?.Order ?? -1;
    Observable = propertyInfo.GetCustomAttribute<ObservableAttribute>();
    var isValid = IsSettable && propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>(true) == null && !FieldType.HasAssignableType(typeof(IAmInternal));
    IsOutputIgnored = propertyInfo.GetCustomAttribute<OutputIgnoreAttribute>(true) != null || !isValid;
    // IsLogIgnored = propertyInfo.GetCustomAttribute<LogIgnoreAttribute>() != null || hasJsonIgnore;
    IsInputIgnored = propertyInfo.GetCustomAttribute<InputIgnoreAttribute>() != null || !isValid;

    var defVal = propertyInfo.GetCustomAttribute<DefaultValueAttribute>();
    if (defVal != null) DefaultValue = defVal.Value;
    else if (FieldType == typeof(int)) DefaultValue = 0;
    else if (FieldType == typeof(uint)) DefaultValue = (uint) 0;
    else if (FieldType == typeof(short)) DefaultValue = (short) 0;
    else if (FieldType == typeof(ushort)) DefaultValue = (ushort) 0;
    else if (FieldType == typeof(long)) DefaultValue = (long) 0;
    else if (FieldType == typeof(ulong)) DefaultValue = (ulong) 0;
    else if (FieldType == typeof(double)) DefaultValue = (double) 0;
    else if (FieldType == typeof(float)) DefaultValue = (float) 0;
    else if (FieldType == typeof(decimal)) DefaultValue = (decimal) 0;
    else if (FieldType == typeof(byte)) DefaultValue = (byte) 0;
    else if (FieldType == typeof(bool)) DefaultValue = (bool) false;

    var parent = propertyInfo.GetCustomAttribute<ApiParentAttribute>();
    if (parent != null) {
      ChildPropertyName = parent.ChildProperty;
      ThroughPropertyType = parent.ThroughModelType;
      ChildDeleteBehavior = parent.DeleteBehavior;
    }
  }
  private PropertyInfo PropertyInfo { get; }

  // public bool IsLogIgnored { get; }

  public bool IsInputIgnored { get; }

  public bool IsOutputIgnored { get; private set; }

  public bool IsSettable { get; }

  public bool IsInherited { get; }

  public object? DefaultValue { get; }

  public int Order { get; }

  public string? ChildPropertyName { get; }

  public ApiDeleteBehavior ChildDeleteBehavior { get; } = ApiDeleteBehavior.SetNull;

  public Type? ThroughPropertyType { get; }

  public ZMethodDescriptor? ExecutionMethod { get; set; }

  public ObservableAttribute? Observable { get; private set; }

  public object? GetValue(object obj) => PropertyInfo.GetValue(obj);

  public void SetValue(object obj, object? val) {
    if (!IsSettable) throw new SystemException($"{this} is not settable");
    if (PropertyInfo.SetMethod == null) throw new SystemException($"{this} has no setter");
    PropertyInfo.SetMethod!.Invoke(obj, new[] {
      val
    });
  }

  public override string ToString() => $"<{PropertyInfo.Name}: {FieldTypeDescriptor} {IsSettable}>";
}
