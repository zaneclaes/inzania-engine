#region

using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Navigation;

public class CurrentPage : TransientObject {
  public CurrentPage(IZContext context, string path, Sitemap sitemap) : base(context) {
    // Nav = nav;
    Path = path;
    Sitemap = sitemap;
    DeepLink = DeepLink.FromPath(context, Path);
  }
  // public NavigationManager Nav { get; private set; }

  public Sitemap Sitemap { get; }

  [Observable] public DeepLink? DeepLink { get; }

  public SitePage? SitePage => DeepLink?.Page;

  [Observable] public string Path { get; }

  public void SendPageView() {
    string title = SitePage?.Title ?? $"{Path.Split("/").First()}";
    // Context.Log.Information("[GA] {path} => {title}", Page?.Path ?? Path, title);
    Context.Analytics?.PageView(SitePage?.Path ?? Path, title);
  }
}
