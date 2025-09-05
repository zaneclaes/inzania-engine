#region

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Navigation;

public enum EmbeddingBehaviour {
  NativeOnly = -1,
  PreferNative = 0, // WebGL opens the native screen (default)
  EmbedWebView = 1, // Not implemented in native...
  OpenExternal = 2
}

public class SeoImage {
  public string Url { get; set; } = null!;

  public string? Alt { get; set; }

  public int Width { get; set; } = 1280;

  public int Height { get; set; } = 630;
}

public interface ISitePage {
  public string? Title { get; }

  public string Path { get; }

  public string CanonicalPath { get; }

  public string? Description { get; }

  public List<string> Keywords { get; }

  public SeoImage? Image { get; }
}

public interface ISiteSubPage : ISitePage {
  public DeepLink GetDeepLink(params string[] paths);
}

public class SitePage : LogicBase, ISitePage {

  protected SitePage(
    IZContext context, string path, string title, string? desc = null, string? author = null, params string[] keywords
  ) : base(context) {
    // PageType = type;
    Template = path.Trim('/');
    List<string>? args = Template.Split(':').ToList();
    Path = args.First().Trim('/');
    Section = Path.Split('/').First().ToLowerInvariant();
    args.RemoveAt(0);
    Args = args.Select(a => a.Trim('/')).ToArray();
    Title = title;
    Description = desc;
    Author = author;
    Keywords = keywords.ToList();
    Paths.Add(Path);
  }
  protected override bool AllowRootContext => true;

  public virtual bool IncludeInSiteMap => MinimumRoleRequired <= ZUserRole.Visitor;

  public virtual ZUserRole MinimumRoleRequired => ZUserRole.Visitor;

  public virtual EmbeddingBehaviour EmbedBehaviour => EmbeddingBehaviour.PreferNative;

  public virtual bool MatchesPath(string path) => path.Split('/').First().ToLowerInvariant() == Section;

  [Observable] public List<string> Paths { get; } = new List<string>();

  [Observable] public string Title { get; }

  public SeoImage? Image => null;

  public XElement GenerateSitemap(string rootUrl) {
    return new XElement("url",
      new XElement("loc", rootUrl + "/" + CanonicalPath)
    );
  }

  public string Path { get; }

  public virtual string CanonicalPath => Path;

  public string Section { get; }

  public string[] Args { get; }

  public string Template { get; }

  public string? Author { get; }

  public string? Description { get; }

  public List<string> Keywords { get; }
  // public Type PageType { get; }

  public virtual ISiteSubPage? GetContent(params string[] components) => null;

  public SitePage WithSubPaths(params string[] paths) {
    foreach (string path in paths) {
      Paths.Add(Path + '/' + path.Trim('/'));
    }
    return this;
  }

  public override string ToString() => $"<{Title} {Path} />";
}
