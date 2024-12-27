#region

using System;
using System.Reflection;
using System.Text.Json.Serialization;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

public class TunePropertyDescriptor : TuneFieldDescriptor {
  private PropertyInfo PropertyInfo { get; }

  // public bool IsLogIgnored { get; }

  public bool IsJsonIgnored { get; }

  public bool IsInputIgnored { get; }

  public bool IsSettable { get; }

  public TuneMethodDescriptor? ExecutionMethod { get; set; }

  public ObservableAttribute? Observable { get; private set; }

  public override Type FieldType => PropertyInfo.PropertyType;

  public object? GetValue(object obj) => PropertyInfo.GetValue(obj);

  public void SetValue(object obj, object? val) {
    if (!IsSettable) throw new SystemException($"{this} is not settable");
    if (PropertyInfo.SetMethod == null) throw new SystemException($"{this} has no setter");
    PropertyInfo.SetMethod!.Invoke(obj, new[] {
      val
    });
  }

  public TunePropertyDescriptor(PropertyInfo propertyInfo) : base(propertyInfo) {
    if (propertyInfo.GetMethod != null) { // Properties have a weird case where they do not expose nullability
      IsOptional = new NullabilityInfoContext()
        .Create(propertyInfo.GetMethod!.ReturnParameter!).ReadState == NullabilityState.Nullable;
    }

    PropertyInfo = propertyInfo;
    FieldName = propertyInfo.Name.ToFieldName();
    IsSettable = propertyInfo.CanWrite;
    Observable = propertyInfo.GetCustomAttribute<ObservableAttribute>();
    bool hasJsonIgnore = propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>() != null;
    IsJsonIgnored = !IsSettable || hasJsonIgnore;
    // IsLogIgnored = propertyInfo.GetCustomAttribute<LogIgnoreAttribute>() != null || hasJsonIgnore;
    IsInputIgnored = propertyInfo.GetCustomAttribute<InputIgnoreAttribute>() != null || IsJsonIgnored;
  }

  public override string ToString() => $"<{PropertyInfo.Name}: {PropertyInfo.PropertyType}{(IsOptional ? "?" : "!")}>";
}
