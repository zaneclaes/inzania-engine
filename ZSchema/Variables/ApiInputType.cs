using System;
using HotChocolate.Types;

namespace IZ.Schema.Variables;

public class ApiInputType : IInputType {

  public TypeKind Kind { get; }

  public Type RuntimeType { get; }

  public ApiInputType(TypeKind kind, Type runtimeType) {
    Kind = kind;
    RuntimeType = runtimeType;
  }
}
