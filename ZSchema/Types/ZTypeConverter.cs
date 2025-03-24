#region

using System;
using System.Diagnostics.CodeAnalysis;
using HotChocolate.Utilities;

#endregion

namespace IZ.Schema.Types;

public class ZTypeConverter : IChangeTypeProvider {
  public bool TryCreateConverter(Type source, Type target, ChangeTypeProvider root, [NotNullWhen(true)] out ChangeType? converter) {
    // Log.Information("[CT] {source} to {targ} from {root}", source, target, root);
    converter = null;
    return false;
  }
}
