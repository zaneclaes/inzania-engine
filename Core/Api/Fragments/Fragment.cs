#region

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IZ.Core.Api.Types;

#endregion

namespace IZ.Core.Api.Fragments;

public class Fragment {
  public TuneObjectDescriptor ObjectTypeDescriptor { get; }

  public string? Format { get; }

  public string Name { get; }

  public string Contents { get; }

  public List<string> DependencyNames { get; }

  public Fragment(TuneObjectDescriptor desc, string? format, string contents) {
    ObjectTypeDescriptor = desc;
    Format = format;
    Name = GetName(desc, Format);
    Contents = contents;
    DependencyNames = ExtractAllFragmentNames(Name, contents);
  }

  public static string GetName(TuneObjectDescriptor desc, string? format) {
    string name = desc.TypeName;
    if (!string.IsNullOrWhiteSpace(format)) name += "_" + format;
    return name;
  }

  private static readonly Regex FragmentRegex = new Regex("\\.\\.\\.([\\w\\d_]+)\\s");

  public static List<string> ExtractAllFragmentNames(string rootFragment, string fragmentContents) {
    var matches = FragmentRegex.Matches(fragmentContents);
    List<string> ret = new List<string> {
      rootFragment
    };
    foreach (Match m in matches) {
      for (int i = 1; i < m.Groups.Count; i++) {
        // IZEnv.Log.Information("[GROUP] {i} {str}", i, m.Groups[i].Value);
        ret.Add(m.Groups[i].Value);
      }
    }
    return ret.Distinct().ToList();
  }
}
