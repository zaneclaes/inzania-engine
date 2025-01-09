#region

using HotChocolate.Types;
using IZ.Core.Data;

#endregion

namespace IZ.Schema.Types;

public class ZModelIdType : ObjectType<ModelId> {
  protected override void Configure(IObjectTypeDescriptor<ModelId> descriptor) {
    descriptor.Field(x => x.Id).Type<NonNullType<IdType>>();
    // ZEnv.Log.Information("[DM] {d}", descriptor));
    base.Configure(descriptor);
  }
}
