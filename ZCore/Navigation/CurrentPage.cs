#region

using System;
using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Navigation;

public class CurrentPage<TPage, TLink, TMap> : TransientObject
  where TPage : SitePage
  where TLink : DeepLink<TPage>
  where TMap : Sitemap<TPage, TLink> {

  public TMap Sitemap { get; }

  [Observable] public TLink? DeepLink { get; }

  public TPage? TopPage => DeepLink?.Page;

  public ISiteContent? Content => _content ?? TopPage;

  [Observable] public string Title => Content?.Title ?? $"{Path.Split("/").First()}";

  // Fix warning of titles too short
  public string GetSeoTitle() => CreateTitle(Content?.GetSeoTitle() ?? Title, Context.App.ProductName);//  Title.Length < 30 ? $"{Title} | {Context.App.ProductName}" : Title;

  public static string CreateTitle(string title, string subSuffix) {
    const int idealLen = 50;
    var suffix = " | " + subSuffix;
    if ((title.Length + suffix.Length) > idealLen || title.EndsWith(suffix)) return title;
    return $"{title}{suffix}";
  }

  [Observable] public string Path { get; }

  private ISiteContent? _content;

  protected CurrentPage(IZContext context, string path, TMap sitemap, TLink? deepLink) : base(context) {
    // Nav = nav;
    Path = deepLink?.Path ?? path;
    Sitemap = sitemap;
    DeepLink = deepLink; // TuneDeepLink.FromPath(context, Path);
    _content = TopPage?.GetContent(new DeepLink(Context, path).SubPaths);
  }
  // public NavigationManager Nav { get; private set; }

  public void SetContent(ISiteContent? content, bool forcePageView = false) {
    // var oldPath = AnalyticsPath;
    var oldTitle = Title;
    _content = content;
    if (forcePageView || oldTitle != Title) {
      SendPageView();
    }
  }

  public void SendPageView() {
    // The PageViews hook is the single analytics path: the consuming app's subscriber forwards each
    // view to GA *and* to its own store (Chordzy: TuneAnalytics -> GA + UserEvent). Also sending the
    // engine GA event here double-counted every view, so the direct send is only a fallback for apps
    // with no subscriber (ZGoogleAnalytics.PageView dedupes repeats of the same path itself).
    if (!PageViews.HasSubscribers) Context.Analytics?.PageView(Path, Title);
    PageViews.Raise(Path, Title);
  }

  public override string ToString() => $"<{Title} {Path} />";
}
