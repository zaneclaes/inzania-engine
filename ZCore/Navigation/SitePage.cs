#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
  WebGlBrowser = 2, // WebGL opens the browser URL (other apps use native)
}

public abstract class SitePage : LogicBase, ISitePage {

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

  public virtual ISitemapImage? SitemapImage => null;

  public virtual DateTime? LastModified => null;

  public abstract DeepLink GetDeepLink(params string[] paths);

  // protected virtual XElement GetSitePageElement(string rootUrl, ISitePage page) {
  //   var url = new XElement("url", new XElement("loc", rootUrl + "/" + page.CanonicalPath));
  //
  //   if (page.LastModified != null) {
  //     url.Add(new XElement("lastmod", page.LastModified.Value.ToUniversalTime()));
  //   }
  //
  //   if (page.Image != null) {
  //     url.Add(new XElement("image:image", new XElement("image:loc", page.Image.Url), new XElement("image:title", page.Image.Caption)));
  //   }
  //
  //   // localization??
  //   //   <xhtml:link   rel="alternate" hreflang="es" href="https://example.com/es/exercises/scales/major-octave-in-g"/>
  //   return url;
  // }
  //
  // public List<XElement> GenerateSitemap(string rootUrl) {
  //   var ret = new List<XElement>() { GetSitePageElement(rootUrl, this) };
  //   foreach (var subContent in SubContent) {
  //     var subPages = subContent.GetAllSubPages();
  //     if (subContent.IncludeInSiteMap)
  //       ret.Add(GetSitePageElement(rootUrl, subContent));
  //     foreach (var subPage in subPages) {
  //       if (subPage.IncludeInSiteMap)
  //         ret.Add(GetSitePageElement(rootUrl, subPage));
  //     }
  //   }
  //   return ret;
  // }

  public virtual async Task<List<ISitemapPage>> GetSitemapPages(IZContext context) {
    var ret = new List<ISitemapPage>() { this };
    foreach (var subContent in SubContent) {
      var subPages = await subContent.GetSitemapPages(context);
      foreach (var subPage in subPages) {
        ret.Add(subPage);
      }
    }
    return ret;
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

  public List<ISitePage> GetAllSubPages() => SubContent;
  public virtual List<ISitePage> SubContent { get; } = new List<ISitePage>();
  public virtual ISitePage? GetContent(params string[] components) => null;

  public SitePage WithSubPaths(params string[] paths) {
    foreach (string path in paths) {
      Paths.Add(Path + '/' + path.Trim('/'));
    }
    return this;
  }

  public override string ToString() => $"<{Title} {Path} />";
}
