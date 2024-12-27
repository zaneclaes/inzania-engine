#region

using HotChocolate.Types;
using IZ.Core.Data;

#endregion

namespace IZ.Schema.Types;

public class TuneModelIdType : ObjectType<ModelId> {
  protected override void Configure(IObjectTypeDescriptor<ModelId> descriptor) {
    descriptor.Field(x => x.Id).Type<NonNullType<IdType>>();
    // IZEnv.Log.Information("[DM] {d}", descriptor));
    base.Configure(descriptor);
  }
}
