using System.Reflection;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Data;

public class AppOptions : TransientObject {

}

public class AppOptions<T> : AppOptions where T : AppOptions<T> {
  public void CopyOptionsTo(T val) {
    var props = typeof(T).GetProperties();
    foreach (var prop in props) {
      if (prop.SetMethod == null || !prop.CanRead || !prop.CanWrite) continue;
      if (typeof(IAmInternal).IsAssignableFrom(prop.PropertyType)) continue;
      if (prop.GetCustomAttribute<ApiSecretAttribute>() != null) continue;
      // ZEnv.Log.Information("[OPT] copy {type}.{prop}", typeof(T).Name, prop.Name);
      var propVal = prop.GetValue(this);
      if (propVal == null) continue;
      prop.SetValue(val, propVal);
    }
  }
}
