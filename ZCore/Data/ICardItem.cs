using IZ.Core.Data;

namespace ZCore.Data;

// Can be used in UI cards
public interface ICardItem : IItemizable {
  public string Title { get; }

  public string? ImagePath { get; }

  public string? About { get; }

  public string GetImageUrlForHost(string url) => $"{url}/{ImagePath}";
}
