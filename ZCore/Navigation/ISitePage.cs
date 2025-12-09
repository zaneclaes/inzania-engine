using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Navigation.SchemaOrg;
using IZ.Core.Observability.Logging;

namespace IZ.Core.Navigation;

public interface ISiteContent : ISitemapPage {
  public const int MaxPreviewLength = 120; // For search engines.

  public string Title { get; }

  public string? Preview { get; }

  public List<string> Keywords { get; }

  public List<SchemaItemJson> GetSchemaItems();

  public string GetSeoTitle();

  public string? GetSeoPreview() => Preview == null || Preview.Length < MaxPreviewLength ?
    Preview : Preview.Substring(0, MaxPreviewLength);

  public SiteContent AsDto() => new SiteContent() {
    CanonicalPath = CanonicalPath,
    Image = SitemapImage?.AsDto(),
    Title = Title,
    SeoTitle = GetSeoTitle(),
    Preview = Preview,
    LastModified = LastModified,
    Keywords = Keywords.ToList(),
    SchemaItems = GetSchemaItems().ToList(),
  };
}

public class SiteContent : ISiteContent {
  public string CanonicalPath { get; set; } = null!;
  public ISitemapImage? SitemapImage => Image;
  public DateTime? LastModified { get; set; }
  public string Title { get; set; } = null!;
  public string SeoTitle { get; set; } = null!;
  public string? Preview { get; set; }
  public List<string> Keywords { get; set; } = new List<string>();
  public List<SchemaItemJson> SchemaItems { get; set; } = new List<SchemaItemJson>();
  public SiteImage? Image { get; set; }

  public string GetSeoTitle() => SeoTitle;
  public List<SchemaItemJson> GetSchemaItems() => SchemaItems;
}

public interface ISitePage : ISiteContent {
  public string Path { get; }

  public EmbeddingBehaviour EmbedBehaviour { get; }

  public DeepLink GetDeepLink(params string[] paths);

  public List<ISitePage> GetAllSubPages();

  public Task<List<ISitemapPage>> GetSitemapPages(IZContext context);

  public bool IncludeInSiteMap { get; }
}
