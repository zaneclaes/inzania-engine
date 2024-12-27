// #region
//
// using System.Reflection;
// using System.Runtime.CompilerServices;
// using HotChocolate.Types;
// using HotChocolate.Types.Descriptors;
// using Tuneality.Data.Storage;
//
// #endregion
//
// namespace Tuneality.Schema.Types;
//
// public class UseTuneDataContext : ObjectFieldDescriptorAttribute {
//   public UseTuneDataContext([CallerLineNumber] int order = 0) {
//     Order = order;
//   }
//
//   protected override void OnConfigure(
//     IDescriptorContext context,
//     IObjectFieldDescriptor descriptor,
//     MemberInfo member) {
//     descriptor.UseDbContext<TuneDataContext>();
//   }
// }
