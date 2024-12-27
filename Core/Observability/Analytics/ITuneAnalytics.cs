#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Core.Auth;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Observability.Analytics;

public interface ITuneAnalytics : IHaveContext, IDisposable {
  public Task SendEvent<T>(AnalyticsEvent<T> e) where T : IEventParams;

  public Task PageView(string path, string? title = null);

  public Task ScreenView(string name, string? klass = null);

  public Task LoginBegin(string method);

  public Task LoginEnd(string method);

  public Task SignUp(string method);

  public Task Search(string searchTerm);

  public Task Share(string method);

  public Task Exception(string desc, bool fatal = false);

  // Record points earned
  public Task EarnPoints(long points, int? skillLevel = null, string? character = null);

  // i.e., "Score" + scoreId
  public Task SelectContent(string contentType, string contentId);

  public Task Configure(IAnalyticsSink sink, ITuneIdentity? identity = null);

  public Task SetUserProperties(string installId, string sessionId, string? userId, Dictionary<string, object> props);

  public Task SetIdentity(ITuneIdentity identity) {
    Dictionary<string, object>? props = new Dictionary<string, object> {
      ["env"] = Context.App.Env.ToString()
    };
    var user = identity.IZUser;
    if (user != null) {
      var age = IZEnv.Now - user.CreatedAt;
      if (age.TotalDays < 7) props["user_age"] = "days";
      else if (age.TotalDays < 30) props["user_age"] = "weeks";
      else if (age.TotalDays < 365) props["user_age"] = "months";
      else props["user_age"] = "years";
      // props["user_id"] = ;
    }
    return SetUserProperties(identity.InstallId, identity.SessionId, user?.Id, props);
  }

  private Task SendEvent(string name) => SendEvent(new AnalyticsEvent<NullParams>(name, new NullParams()));
  public Task SendEvent<T>(string name, T pars) where T : IEventParams => SendEvent(new AnalyticsEvent<T>(name, pars));

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
