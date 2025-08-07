using System;
using HotChocolate.Types;

namespace IZ.Schema.Variables;

public class ApiInputType : IInputType {

  public ApiInputType(TypeKind kind, Type runtimeType) {
    Kind = kind;
    RuntimeType = runtimeType;
  }

  public TypeKind Kind { get; }

  public Type RuntimeType { get; }
}
