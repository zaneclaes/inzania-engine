#region

using System.Collections.Generic;
using System.Linq;
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

public interface ISitePageContent {
  public string? Title { get; }

  public DeepLink GetDeepLink(params string[] paths);
}

public class SitePage : LogicBase {

  protected SitePage(
    IZContext context, string path, string title, string? desc = null, string? author = null, params string[] keywords
  ) : base(context) {
    // PageType = type;
    Template = path.Trim('/');
    List<string>? args = Template.Split(':').ToList();
    Path = args.First().Trim('/');
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

  [Observable] public List<string> Paths { get; } = new List<string>();

  [Observable] public string Title { get; }

  public string Path { get; }

  public string[] Args { get; }

  public string Template { get; }

  public string? Author { get; }

  public string? Description { get; }

  public List<string> Keywords { get; }
  // public Type PageType { get; }

  public virtual ISitePageContent? GetContent(params string[] components) => null;

  public SitePage WithSubPaths(params string[] paths) {
    foreach (string path in paths) {
      Paths.Add(Path + '/' + path.Trim('/'));
    }
    return this;
  }

  public override string ToString() => $"<{Title} {Path} />";
}
