#region

using HotChocolate.Types;
using IZ.Core.Api;

#endregion

namespace IZ.Schema.Queries;

public class TuneQueryType : ObjectType {
  protected override void Configure(IObjectTypeDescriptor descriptor) {
    descriptor.AddTuneRequestDescriptors<TuneQueryBase>(ApiExecutionType.Query);
  }
}
