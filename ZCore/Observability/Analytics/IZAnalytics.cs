using System;
using System.Collections.Generic;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Utils;
#region

#endregion

namespace IZ.Core.Observability.Analytics;

public interface IZAnalytics : IHaveContext, IDisposable {
  public ZTask SendEvent<T>(AnalyticsEvent<T> e) where T : IEventParams;

  public ZTask PageView(string path, string? title = null);

  public ZTask ScreenView(string name, string? klass = null);

  public ZTask LoginBegin(string method);

  public ZTask LoginEnd(string method);

  public ZTask SignUp(string method);

  public ZTask Search(string searchTerm);

  public ZTask Share(string method);

  public ZTask Exception(string desc, bool fatal = false);

  public ZTask FileDownload(string url, string name, string fileType);

  public ZTask TutorialBegin(); // should be used for onboarding tutorial only
  public ZTask TutorialComplete(); // should be used for onboarding tutorial only

  // Record points earned
  public ZTask EarnPoints(long points, int? skillLevel = null, string? character = null);

  public ZTask OperationTimingSuccess(string op, long elapsedMilliseconds, int successCode = 1);
  public ZTask OperationTimingFailure(string op, long elapsedMilliseconds, Exception? ex = null);

  public ZTask OperationTiming(string op, long elapsedMilliseconds, Exception? ex = null) =>
    ex == null ? OperationTimingSuccess(op, elapsedMilliseconds) : OperationTimingFailure(op, elapsedMilliseconds, ex);

  // i.e., "Score" + scoreId
  public ZTask SelectContent(string contentType, string contentId);

  public ZTask Configure(IAnalyticsSink? sink, Installation install, IZIdentity? identity, Dictionary<string, object>? props = null);

  public ZTask SetUserProperties(IZIdentity? identity, Dictionary<string, object>? props = null);

  // public ZTask SetIdentity(IZIdentity identity, Dictionary<string, object>? userProps = null) {
  //   userProps ??= new Dictionary<string, object>();
  //   userProps["env"] = Context.App.Env.ToString();
  //   var user = identity.IZUser;
  //   return SetUserProperties(user?.Id, userProps);
  // }

  public ZTask SendEvent(string name) => SendEvent(new AnalyticsEvent<BaseParams>(name, new BaseParams()));
  public ZTask SendEvent<T>(string name, T pars) where T : IEventParams => SendEvent(new AnalyticsEvent<T>(name, pars));

  public ZTask UserEngagement();

  // public Task SelectScorePart(ScorePart part) => SelectContent(nameof(ScorePart), part.GetScoreUuid());

  // https://support.google.com/analytics/answer/9267735?hl=en

  // Purchase, Refund, AddToCart, AddToWishlist, RemoveFromCart, SelectItem,
  // SelectPromotion, ViewCart, ViewItem, ViewItemList, ViewPromotion
  // GenerateLead,

  // JoinGroup
  // LevelStart, LevelEnd, LevelUp
  // TutorialBegin, TutorialComplete, UnlockAchievement
  // Earn/Spend Virtual Currency
}
