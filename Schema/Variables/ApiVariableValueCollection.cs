using System;
using System.Collections;
using System.Collections.Generic;
using HotChocolate.Execution;
using HotChocolate.Language;

namespace IZ.Schema.Variables;

internal class ApiVariableValueCollection : IVariableValueCollection {
  private readonly Dictionary<string, ApiVariableValueOrLiteral> _coercedValues;

  public ApiVariableValueCollection(Dictionary<string, ApiVariableValueOrLiteral> coercedValues) {
    _coercedValues = coercedValues;
  }

  public static ApiVariableValueCollection Empty { get; } = new ApiVariableValueCollection(new Dictionary<string, ApiVariableValueOrLiteral>());

  public T? GetVariable<T>(string name) {
    if (TryGetVariable(name, out T? value)) {
      return value;
    }

    if (_coercedValues.ContainsKey(name)) {
      throw new ArgumentException(name);
    }

    throw new KeyNotFoundException(name);
  }

  public bool TryGetVariable<T>(string name, out T? value) {
    if (string.IsNullOrEmpty(name)) {
      throw new ArgumentNullException(nameof(name));
    }

    if (_coercedValues.TryGetValue(name, out var variableValue)) {
      var requestedType = typeof(T);

      if (requestedType == typeof(IValueNode)) {
        value = (T) variableValue.ValueLiteral;
        return true;
      }

      if (typeof(IValueNode).IsAssignableFrom(requestedType)) {
        if (variableValue.ValueLiteral is T casted) {
          value = casted;
          return true;
        }

        value = default!;
        return false;
      }

      if (variableValue.Value is null) {
        value = default;
        return true;
      }

      if (variableValue.Value.GetType() == requestedType) {
        value = (T) variableValue.Value;
        return true;
      }

      if (variableValue.Value is T castedValue) {
        value = castedValue;
        return true;
      }
    }

    value = default!;
    return false;
  }

  public IEnumerator<VariableValue> GetEnumerator() {
    foreach (KeyValuePair<string, ApiVariableValueOrLiteral> item in _coercedValues) {
      var type = item.Value.Type;
      var value = item.Value.ValueLiteral;
      yield return new VariableValue(item.Key, type, value);
    }
  }

  IEnumerator IEnumerable.GetEnumerator()
    => GetEnumerator();
}
