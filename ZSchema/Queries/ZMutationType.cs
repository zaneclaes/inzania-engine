#region

using HotChocolate.Types;
using IZ.Core.Api;

#endregion

namespace IZ.Schema.Queries;

public class ZMutationType : ObjectType {
  protected override void Configure(IObjectTypeDescriptor descriptor) {
    descriptor.AddZRequestDescriptors<ZMutationBase>(ApiExecutionType.Mutation);
  }
}
