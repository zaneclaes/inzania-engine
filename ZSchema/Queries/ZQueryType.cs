#region

using HotChocolate.Types;
using IZ.Core.Api;

#endregion

namespace IZ.Schema.Queries;

public class ZQueryType : ObjectType {
  protected override void Configure(IObjectTypeDescriptor descriptor) {
    descriptor.AddZRequestDescriptors<ZQueryBase>(ApiExecutionType.Query);
  }
}
