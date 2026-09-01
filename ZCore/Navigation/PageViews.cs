#region

using System;

#endregion

namespace IZ.Core.Navigation;

/// <summary>
/// A hook raised after every page view, so a consuming app can mirror page views somewhere besides
/// analytics (Chordzy also writes them to its own `UserEvent` table). It is a hook rather than a
/// virtual on <c>CurrentPage&lt;,,&gt;</c> because page views are raised from two call sites there —
/// overriding one would silently miss the other — and because a static on a generic type would exist
/// once per closed type.
/// </summary>
public static class PageViews {
  public static Action<string, string?>? OnPageView { get; set; }

  public static bool HasSubscribers => OnPageView != null;

  /// <summary>
  /// One human navigation can reach <see cref="Raise" /> twice: once on the route change and again
  /// when async content arrives and refines the title (<c>CurrentPage.SetContent</c>). The dedup
  /// lives here — the one funnel both call sites share — so a title-only refinement of the same path
  /// never counts as a second view, while a real path change (A → B → A included) always raises.
  /// </summary>
  private static string? _lastPath;

  public static void Raise(string path, string? title) {
    if (path == _lastPath) return;
    _lastPath = path;
    OnPageView?.Invoke(path, title);
  }
}
