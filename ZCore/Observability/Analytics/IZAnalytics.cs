#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Core.Auth;
using IZ.Core.Contexts;

#if Z_UNITY
using Cysharp.Threading.Tasks;
using ZTask = Cysharp.Threading.Tasks.UniTask;
using Tasks = Cysharp.Threading.Tasks.UniTask;
#else
using ZTask = System.Threading.Tasks.Task;
#endif


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

  // Record points earned
  public ZTask EarnPoints(long points, int? skillLevel = null, string? character = null);

  // i.e., "Score" + scoreId
  public ZTask SelectContent(string contentType, string contentId);

  public ZTask Configure(IAnalyticsSink? sink, IZIdentity? identity = null);

  public ZTask SetUserProperties(string installId, string sessionId, string? userId, Dictionary<string, object> props);

  public ZTask SetIdentity(IZIdentity identity) {
    Dictionary<string, object>? props = new Dictionary<string, object> {
      ["env"] = Context.App.Env.ToString()
    };
    var user = identity.IZUser;
    if (user != null) {
      var age = ZEnv.Now - user.CreatedAt;
      if (age.TotalDays < 7) props["user_age"] = "days";
      else if (age.TotalDays < 30) props["user_age"] = "weeks";
      else if (age.TotalDays < 365) props["user_age"] = "months";
      else props["user_age"] = "years";
      // props["user_id"] = ;
    }
    return SetUserProperties(identity.ClientId, identity.SessionId, user?.Id, props);
  }

  private ZTask SendEvent(string name) => SendEvent(new AnalyticsEvent<NullParams>(name, new NullParams()));
  public ZTask SendEvent<T>(string name, T pars) where T : IEventParams => SendEvent(new AnalyticsEvent<T>(name, pars));

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
