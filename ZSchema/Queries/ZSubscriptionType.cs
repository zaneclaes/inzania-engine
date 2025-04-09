using HotChocolate.Types;
using IZ.Core.Api;

namespace IZ.Schema.Queries;

public class ZSubscriptionType : ObjectType {
  protected override void Configure(IObjectTypeDescriptor descriptor) {
    descriptor.AddZRequestDescriptors<ZSubscriptionBase>(ApiExecutionType.Subscription);
  }
}
