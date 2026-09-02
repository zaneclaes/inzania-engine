using System;
using System.Collections;
using System.Collections.Generic;
using IZ.Core.Api;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Utils;

#if !Z_UNITY
using HotChocolate.Language;

namespace IZ.Client.Api;

public class HotChocolateParameterConverter : LogicBase, IParameterConverter {

  public object ConvertParameter(object? arg) => Convert(arg);

  private IValueNode Convert(object? arg) {
    if (arg == null) return NullValueNode.Default;
    if (arg is IList list) {
      List<IValueNode> arr = new List<IValueNode>();
      for (int i = 0; i < list.Count; i++) {
        arr.Add(Convert(list[i]));
      }
      return new ListValueNode(arr);
    }
    var desc = ZApi.LoadTypeDescriptor(arg.GetType());

    if (desc.ObjectDescriptor.IsScalar) {
      if (arg is string stringVal) return new StringValueNode(stringVal);
      if (arg is sbyte sbyteVal) return new IntValueNode(sbyteVal);
      if (arg is byte byteVal) return new IntValueNode(byteVal);
      if (arg is int intVal) return new IntValueNode(intVal);
      if (arg is uint uintVal) return new IntValueNode(uintVal);
      if (arg is short shortVal) return new IntValueNode(shortVal);
      if (arg is ushort ushortVal) return new IntValueNode(ushortVal);
      if (arg is bool boolVal) return new BooleanValueNode(boolVal);
      if (arg is long longVal) return new IntValueNode(longVal);
      if (arg is ulong ulongVal) return new IntValueNode(ulongVal);
      if (arg is float floatVal) return new FloatValueNode(floatVal);
      if (arg is double doubleVal) return new FloatValueNode(doubleVal);
      if (arg is decimal decVal) return new FloatValueNode(decVal);
      if (arg is Enum e) return new EnumValueNode(e.ToString());
      // DateTime/Guid/char are scalars per TypeUtils but have no GraphQL literal of their own; they
      // travel as strings, in the exact format ZObjectDescriptor.ConvertValue parses on the far end.
      if (arg is DateTime dateVal) return new StringValueNode(dateVal.ToApiString());
      if (arg is DateTimeOffset offsetVal) return new StringValueNode(offsetVal.UtcDateTime.ToApiString());
      if (arg is Guid guidVal) return new StringValueNode(guidVal.ToString());
      if (arg is char charVal) return new StringValueNode(charVal.ToString());
      throw new ArgumentException($"{arg.GetType().Name} cannot be translated into a value node");
    }
    // if (!(arg is ApiObject obj)) return arg;

    List<ObjectFieldNode> fields = new List<ObjectFieldNode>();
    foreach (string inputName in desc.ObjectDescriptor.Inputs.Keys) {
      var node = Convert(desc.ObjectDescriptor.Inputs[inputName].GetValue(arg));
      fields.Add(new ObjectFieldNode(inputName, node));
    }
    return new ObjectValueNode(fields);
  }

  public HotChocolateParameterConverter(IZContext context) : base(context) { }
}
#endif
