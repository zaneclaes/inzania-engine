#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Navigation;

public abstract class Sitemap : LogicBase {

  public Sitemap(IZContext context) : base(context) { }

  public abstract T GetPage<T>() where T : SitePage;

  public abstract T? GetPagePath<T>(string path) where T : SitePage;

  public async Task<string> EnsureSitemapFile(IZContext context, int page = 0) {
    var fn = page > 0 ? $"sitemap-{page}.xml" : "sitemap.xml";
    var dir = await EnsureSitemaps(context);
    var fp = Path.Combine(dir, fn);
    return fp;
  }

  public const int MaxSitemapsPerPage = 50000;
  public static readonly XNamespace XmlNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
  public static XNamespace XmlNsImg = "http://www.google.com/schemas/sitemap-image/1.1";
  public const string LastModFormat = "yyyy-MM-ddTHH:mm:ssZ";

  private async Task<string> EnsureSitemaps(IZContext context) {
    var dir = Path.Combine(context.App.Storage.UserDir, "sitemaps");
    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    await GenerateSitemaps(context, dir);
    return dir;
  }

  private async Task GenerateSitemaps(IZContext context, string dir) {
    string fp = "";
    var index = new XElement(XmlNs + "sitemapindex");
    var curSitemapNum = 1;
    var curSitemapCnt = 0;
    DateTime? curSitemapLastModified = null;
    var curSitemapEntry = new XElement(XmlNs + "sitemap");
    curSitemapEntry.Add(new XElement(XmlNs + "loc", $"{context.App.Url}/sitemap-{curSitemapNum}.xml"));
    index.Add(curSitemapEntry);

    var curSitemapPage = new XElement(XmlNs + "urlset", new XAttribute(XNamespace.Xmlns + "image", XmlNsImg));
    var pages = await GetSitemapPages(context);
    foreach (var page in pages) {
      if (curSitemapCnt >= MaxSitemapsPerPage) {
        fp = Path.Combine(dir, $"sitemap-{curSitemapNum}.xml");
        await File.WriteAllTextAsync(fp, curSitemapPage.ToString());
        if (curSitemapLastModified != null) {
          curSitemapEntry.Add(new XElement(XmlNs + "lastmod", curSitemapLastModified.Value.ToString(LastModFormat)));
          File.SetLastWriteTimeUtc(fp, curSitemapLastModified.Value);
        }

        curSitemapNum++;
        curSitemapCnt = 0;
        curSitemapLastModified = null;
        curSitemapEntry = new XElement(XmlNs + "sitemap");
        curSitemapEntry.Add(new XElement(XmlNs + "loc", $"/{context.App.Url}/sitemap-{curSitemapNum}.xml"));
        index.Add(curSitemapEntry);
        curSitemapPage = new XElement(XmlNs + "urlset");
      }
      curSitemapPage.Add(page.ToSitemapXml(context));
      if (page.LastModified != null) {
        if (curSitemapLastModified == null == curSitemapLastModified < page.LastModified)
          curSitemapLastModified = page.LastModified.Value;
      }
      curSitemapCnt++;
    }

    fp = Path.Combine(dir, $"sitemap-{curSitemapNum}.xml");
    await File.WriteAllTextAsync(fp, curSitemapPage.ToString());
    if (curSitemapLastModified != null) {
      curSitemapEntry.Add(new XElement(XmlNs + "lastmod", curSitemapLastModified.Value.ToString(LastModFormat)));
      File.SetLastWriteTimeUtc(fp, curSitemapLastModified.Value);
    }

    var sm = index.ToString();
    var fpSitemap = Path.Combine(dir, "sitemap.xml");
    if (!File.Exists(fpSitemap) || (await File.ReadAllTextAsync(fpSitemap)) != sm) {
      await File.WriteAllTextAsync(fpSitemap, sm);
    }
  }

  public abstract Task<List<ISitemapPage>> GetSitemapPages(IZContext context);
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
    // Xml = Generate(Context.App.Url, Map);
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

  // private static XElement Generate(string rootUrl, Dictionary<string, TPage> map) {
  //   XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
  //   var urlset = new XElement(ns + "urlset");
  //   foreach (var page in map.Values) {
  //     if (!page.IncludeInSiteMap) continue;
  //     // var page = map[path];
  //     urlset.Add(page.GenerateSitemap(rootUrl));
  //   }
  //   return urlset;
  // }

  public override async Task<List<ISitemapPage>> GetSitemapPages(IZContext context) {
    List<ISitemapPage> pages = new List<ISitemapPage>();
    foreach (var page in Map.Values) {
      if (!page.IncludeInSiteMap) continue;
      pages.AddRange(await page.GetSitemapPages(context));
    }
    return pages;
  }
}
