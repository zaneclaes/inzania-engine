using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Core.Contexts;

namespace IZ.Core.Navigation;

public interface ISitePage : ISitemapPage {
  public string Title { get; }

  public string Path { get; }

  public string? Description { get; }

  public List<string> Keywords { get; }

  public EmbeddingBehaviour EmbedBehaviour { get; }

  public DeepLink GetDeepLink(params string[] paths);

  public List<ISitePage> GetAllSubPages();

  public Task<List<ISitemapPage>> GetSitemapPages(IZContext context);

  public bool IncludeInSiteMap { get; }
}
