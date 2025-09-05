#region

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Navigation;

public abstract class Sitemap : LogicBase {

  public Sitemap(IZContext context) : base(context) { }
  public XElement Xml { get; protected set; } = null!;

  public abstract T GetPage<T>() where T : SitePage;

  public abstract T? GetPagePath<T>(string path) where T : SitePage;
}

public abstract class Sitemap<TPage, TLink> : Sitemap where TPage : SitePage where TLink : DeepLink<TPage> {

  protected Sitemap(ZApp app, params TPage[] pages) : base(new WorkContext(app)) {
    Fqdn = app.Fqdn;
    AddPages(pages);
  }
  public string Fqdn { get; }

  protected override bool AllowRootContext => true;

  private List<TPage> Pages { get; } = new List<TPage>();

  private Dictionary<string, TPage> Map { get; set; } = null!;

  protected void AddPages(params TPage[] pages) {
    Pages.AddRange(pages);
    Map = GetRouteTypeMap(Pages);
    Xml = Generate(Context.App.Url, Map);
  }

  // public SitePage? GetPage(SiteCategory category) => Pages.FirstOrDefault(p => p.Category == category);

  public override T GetPage<T>() => (Pages.First(p => p is T) as T)!;

  public override T? GetPagePath<T>(string path) where T : class => GetPage(path) as T;

  public TPage? GetPage(string path) {
    path = path.ToLowerInvariant();
    // if (path.StartsWith("/")) path = path.Substring(1);
    if (path.Contains("?")) path = path.Split("?").First();
    if (path.Contains("#")) path = path.Split("#").First();
    path = path.Trim('/');
    // Log.Information("[PAGE] find '{path}' in {paths}", path, string.Join(", ", Map.Values.SelectMany(p => p.Paths)));
    return Map.Values.FirstOrDefault(sp => sp.MatchesPath(path));
  }

  private static Dictionary<string, TPage> GetRouteTypeMap(List<TPage> types) => types
    // .SelectMany(d => d.GetSitePages())
    .ToDictionary(d => d.Path, d => d);

  private static XElement Generate(string rootUrl, Dictionary<string, TPage> map) {
    XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    var urlset = new XElement(ns + "urlset");
    foreach (var page in map.Values) {
      if (!page.IncludeInSiteMap) continue;
      // var page = map[path];
      urlset.Add(page.GenerateSitemap(rootUrl));
    }
    return urlset;
  }
}
