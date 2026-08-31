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

  public static void Raise(string path, string? title) => OnPageView?.Invoke(path, title);
}
