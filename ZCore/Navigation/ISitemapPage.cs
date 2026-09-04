using System;
using System.Net.Mime;
using System.Xml.Linq;
using IZ.Core.Contexts;

namespace IZ.Core.Navigation;

public interface ISitemapPage {
  public string CanonicalPath { get; }

  public ISitemapImage? SitemapImage { get; }

  public DateTime? LastModified { get; }
}

public class SiteImage : ISitemapImage {
  public string Url { get; set; } = null!;
  public string? Title { get; set; }
  public string? Caption { get; set; }
  public int Width { get; set; }
  public int Height { get; set; }
}

public interface ISitemapImage {
  public string Url { get; }

  public string? Title { get; }

  public string? Caption { get; }

  public int Width { get; }

  public int Height { get; }

  public SiteImage AsDto() => new SiteImage() {
    Url = Url,
    Title = Title,
    Caption = Caption,
    Width = Width,
    Height = Height
  };
}

public static class SitemapPageExtensions {

  public static XElement ToSitemapXml(this ISitemapPage page, IZContext context) {
    // CanonicalUrl, not Url: a sitemap must list canonical (production) locations even when the
    // staging or dev server generates it.
    var url = new XElement(Sitemap.XmlNs + "url", new XElement(Sitemap.XmlNs + "loc", context.App.CanonicalUrl + "/" + page.CanonicalPath));

    // if (ChangeFrequency != null) url.Add(new XElement("changefreq", ChangeFrequency));
    // if (Priority != null) url.Add(new XElement("priority", Priority.Value.ToString("0.0")));
    if (page.LastModified != null) url.Add(new XElement(Sitemap.XmlNs + "lastmod", page.LastModified.Value.ToString(Sitemap.LastModFormat)));

    if (page.SitemapImage != null) {
      // The image URL is built from `App.Url`, which is `http://localhost:5292` in Development and
      // the staging host on staging — so a sitemap generated anywhere but production listed image
      // locations nothing could fetch. Same rule as `loc` above: a sitemap names canonical locations
      // whoever generated it.
      var img = new XElement(Sitemap.XmlNsImg + "image",
        new XElement(Sitemap.XmlNsImg + "loc", Canonical(context, page.SitemapImage.Url)));
      if (page.SitemapImage.Title != null) img.Add(new XElement(Sitemap.XmlNsImg + "title", page.SitemapImage.Title));
      if (page.SitemapImage.Caption != null) img.Add(new XElement(Sitemap.XmlNsImg + "caption", page.SitemapImage.Caption));
      url.Add(img);
    }

    // localization??
    //   <xhtml:link   rel="alternate" hreflang="es" href="https://example.com/es/exercises/scales/major-octave-in-g"/>
    return url;
  }

  /// <summary>Swaps this environment's origin for the canonical one, leaving an already-absolute
  /// foreign URL (a CDN, someone else's image) alone.</summary>
  private static string Canonical(IZContext context, string url) =>
    url.StartsWith(context.App.Url, StringComparison.OrdinalIgnoreCase)
      ? context.App.CanonicalUrl + url.Substring(context.App.Url.Length)
      : url;
}
