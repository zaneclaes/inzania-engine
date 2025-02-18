#region

using System;
using HotChocolate.Types;
using IZ.Core;

#endregion

namespace IZ.Schema.Types;

public class ZEnumType<TData> : EnumType<TData> where TData : struct, Enum {
  protected override void Configure(IEnumTypeDescriptor<TData> descriptor) {
    TData[] vals = Enum.GetValues<TData>();
    descriptor.BindValuesExplicitly();
    foreach (var val in vals) {
      // ZEnv.Log.Information("V {val} {t} v {type}", val, val.GetType(), typeof(TData));
      descriptor
        .Value(val)
        .Name(val.SerializeZEnum())
        ;
    }
    base.Configure(descriptor);
  }
}
