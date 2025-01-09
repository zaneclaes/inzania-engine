using System;
using HotChocolate.Language;
using HotChocolate.Types;

namespace IZ.Schema.Variables;

public readonly struct ApiVariableValueOrLiteral {
  public ApiVariableValueOrLiteral(IInputType type, object? value, IValueNode valueLiteral) {
    if (value is null && valueLiteral.Kind != SyntaxKind.NullValue) {
      throw new ArgumentException(nameof(ApiVariableValueOrLiteral));
    }

    Type = type ?? throw new ArgumentNullException(nameof(type));
    Value = value;
    ValueLiteral = valueLiteral;
  }

  public IInputType Type { get; }

  public object? Value { get; }

  public IValueNode ValueLiteral { get; }

  public override string ToString() => $"{Type.Kind}({Value?.GetType()}) {ValueLiteral.Value}";
}
