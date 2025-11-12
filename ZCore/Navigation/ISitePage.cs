using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

namespace IZ.Core.Navigation;

public interface ISiteContent : ISitemapPage {
  public string Title { get; }

  public string? Description { get; }

  public List<string> Keywords { get; }

  public SiteContent AsDto() => new SiteContent() {
    CanonicalPath = CanonicalPath,
    Image = SitemapImage?.AsDto(),
    Title = Title,
    Description = Description,
    LastModified = LastModified,
    Keywords = Keywords.ToList(),
  };
}

public class SiteContent : ISiteContent {
  public string CanonicalPath { get; set; } = null!;
  public ISitemapImage? SitemapImage => Image;
  public DateTime? LastModified { get; set; }
  public string Title { get; set; } = null!;
  public string? Description { get; set; }
  public List<string> Keywords { get; set; } = new List<string>();
  public SiteImage? Image { get; set; }
}

public interface ISitePage : ISiteContent {
  public string Path { get; }

  public EmbeddingBehaviour EmbedBehaviour { get; }

  public DeepLink GetDeepLink(params string[] paths);

  public List<ISitePage> GetAllSubPages();

  public Task<List<ISitemapPage>> GetSitemapPages(IZContext context);

  public bool IncludeInSiteMap { get; }
}
