#region

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Navigation;

public abstract class Sitemap : LogicBase {
  public XElement Xml { get; protected set; } = null!;

  public abstract T GetPageType<T>() where T : SitePage;

  public abstract T? GetPagePath<T>(string path) where T : SitePage;
}

public abstract class Sitemap<TPage, TLink> : Sitemap where TPage : SitePage where TLink : DeepLink<TPage>  {
  public string Fqdn { get; }

  protected override bool AllowRootContext => true;

  private List<TPage> Pages { get; }

  protected Sitemap(ZApp app, params TPage[] pages) {
    Pages = pages.ToList();
    Fqdn = app.Fqdn;
    Map = GetRouteTypeMap(Pages);
    Xml = Generate(app.Url, Map);
  }

  private Dictionary<string, TPage> Map { get; }

  // public SitePage? GetPage(SiteCategory category) => Pages.FirstOrDefault(p => p.Category == category);

  public override T GetPageType<T>() => (Pages.First(p => p is T) as T)!;

  public override T? GetPagePath<T>(string path) where T : class => GetPage(path) as T;

  public TPage? GetPage(string path) {
    path = path.ToLowerInvariant();
    // if (path.StartsWith("/")) path = path.Substring(1);
    if (path.Contains("?")) path = path.Split("?").First();
    if (path.Contains("#")) path = path.Split("#").First();
    path = path.Trim('/');
    // Log.Information("[PAGE] find '{path}' in {paths}", path, string.Join(", ", Map.Values.SelectMany(p => p.Paths)));
    if (string.IsNullOrWhiteSpace(path)) return Pages.First() as TPage;
    return Map.Values.FirstOrDefault(sp => sp.Paths.Any(p => path.StartsWith(p + "/") || path.Equals(p))) as TPage;
  }

  private static Dictionary<string, TPage> GetRouteTypeMap(List<TPage> types) => types
    // .SelectMany(d => d.GetSitePages())
    .ToDictionary(d => d.Path, d => d);

  private static XElement Generate(string rootUrl, Dictionary<string, TPage> map) {
    XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    var urlset = new XElement(ns + "urlset");
    foreach (string path in map.Keys) {
      var page = map[path];
      if (!page.IncludeInSiteMap) continue;
      urlset.Add(new XElement("url",
        new XElement("loc", rootUrl + "/" + page.Path)
        // TODO: if you have a way to detect last changes...
        // new XElement("lastmod", "...");
      ));
    }
    return urlset;
  }
}
