#region

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Navigation;

public abstract class Sitemap : LogicBase {
  public XElement Xml { get; }

  public string Fqdn { get; }

  protected override bool AllowRootContext => true;

  protected List<SitePage> Pages { get; }

  public Sitemap(string fqdn, params SitePage[] pages) {
    Pages = pages.ToList();
    Fqdn = fqdn;
    Map = GetRouteTypeMap(Pages);
    Xml = Generate(fqdn, Map);
  }

  private Dictionary<string, SitePage> Map { get; }

  public SitePage? GetPage(string path) {
    path = path.ToLowerInvariant();
    // if (path.StartsWith("/")) path = path.Substring(1);
    if (path.Contains("?")) path = path.Split("?").First();
    if (path.Contains("#")) path = path.Split("#").First();
    path = path.Trim('/');
    // Log.Information("[PAGE] find '{path}' in {paths}", path, string.Join(", ", Map.Values.SelectMany(p => p.Paths)));
    if (string.IsNullOrWhiteSpace(path)) return Pages.First();
    return Map.Values.FirstOrDefault(sp => sp.Paths.Any(p => path.StartsWith(p + "/") || path.Equals(p)));
  }

  public static Dictionary<string, SitePage> GetRouteTypeMap(List<SitePage> types) => types
    // .SelectMany(d => d.GetSitePages())
    .ToDictionary(d => d.Path, d => d);

  public static XElement Generate(string fqdn, Dictionary<string, SitePage> map) {
    XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    var urlset = new XElement(ns + "urlset");
    foreach (string? path in map.Keys)
      urlset.Add(new XElement("url",
        new XElement("loc", fqdn + "/" + map[path].Path)
        // TODO: if you have a way to detect last changes...
        // new XElement("lastmod", "...");
      ));
    return urlset;
  }
}
