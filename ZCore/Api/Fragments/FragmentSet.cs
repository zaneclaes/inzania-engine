#region

using System.Collections.Generic;
using System.Linq;
using IZ.Core.Api.Types;

#endregion

namespace IZ.Core.Api.Fragments;

public class FragmentSet {
  public Dictionary<string, Fragment> Fragments { get; } = new Dictionary<string, Fragment>();

  public ResultSet ResultSet { get; }

  public Fragment Root { get; }

  public string Headers => string.Join("\n", Fragments.Select(f => f.Value.Contents));

  public FragmentSet(IFragmentProvider provider, ZTypeDescriptor rootType, ResultSet resultSet) {
    Root = provider.LoadRequired(rootType.ObjectDescriptor, resultSet.Format);
    ResultSet = resultSet;
    LoadDependencies(provider, Root, new HashSet<string>());
  }

  private void LoadDependencies(IFragmentProvider provider, Fragment fragment, HashSet<string> breadcrumbs) {
    foreach (string? name in fragment.DependencyNames) {
      if (Fragments.ContainsKey(name) || !breadcrumbs.Add(name)) continue;
      Fragments[name] = provider.LoadRequired(name);
      LoadDependencies(provider, Fragments[name], breadcrumbs);
    }
  }
}
