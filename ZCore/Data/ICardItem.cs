using IZ.Core.Data;

namespace ZCore.Data;

// Can be used in UI cards
public interface ICardItem : IItemizable {
  public string Title { get; }

  public string? CoverImagePath { get; }

  public string? About { get; }

  public string? GetCoverImageUrlForHost(string url) => CoverImagePath == null ? null : $"{url}/{CoverImagePath}";
}
