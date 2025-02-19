#region

using System;
using HotChocolate.Utilities;

#endregion

namespace IZ.Schema.Types;

public class ZTypeConverter : IChangeTypeProvider {
  public bool TryCreateConverter(Type source, Type target, ChangeTypeProvider root, out ChangeType? converter) {
    // Log.Information("[CT] {source} to {targ} from {root}", source, target, root);
    converter = null;
    return false;
  }
}
