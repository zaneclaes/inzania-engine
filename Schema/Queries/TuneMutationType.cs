#region

using HotChocolate.Types;
using IZ.Core.Api;

#endregion

namespace IZ.Schema.Queries;

public class TuneMutationType : ObjectType {
  protected override void Configure(IObjectTypeDescriptor descriptor) {
    descriptor.AddTuneRequestDescriptors<TuneMutationBase>(ApiExecutionType.Mutation);
  }
}
