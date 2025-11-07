using System;
using System.Net.Mime;
using System.Xml.Linq;
using IZ.Core.Contexts;

namespace IZ.Core.Navigation;

public interface ISitemapPage : IHaveContext {
  public string CanonicalPath { get; }

  public ISitemapImage? SitemapImage { get; }

  public DateTime? LastModified { get; }
}

public interface ISitemapImage {
  public string Url { get; }

  public string? Title { get; }

  public string? Caption { get; }

  public int Width { get; }

  public int Height { get; }
}

public static class SitemapPageExtensions {

  public static XElement ToSitemapXml(this ISitemapPage page) {
    var url = new XElement("url", new XElement("loc", page.Context.App.Url + "/" + page.CanonicalPath));

    // if (ChangeFrequency != null) url.Add(new XElement("changefreq", ChangeFrequency));
    // if (Priority != null) url.Add(new XElement("priority", Priority.Value.ToString("0.0")));
    if (page.LastModified != null) url.Add(new XElement("lastmod", page.LastModified.Value + "Z"));

    if (page.SitemapImage != null) {
      var img = new XElement("image:image", new XElement("image:loc", page.SitemapImage.Url));
      if (page.SitemapImage.Title != null) img.Add(new XElement("image:title", page.SitemapImage.Title));
      if (page.SitemapImage.Caption != null) img.Add(new XElement("image:caption", page.SitemapImage.Caption));
      url.Add(img);
    }

    // localization??
    //   <xhtml:link   rel="alternate" hreflang="es" href="https://example.com/es/exercises/scales/major-octave-in-g"/>
    return url;
  }
}
