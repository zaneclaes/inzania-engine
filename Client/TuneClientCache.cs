using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Tuneality.Core;
using Tuneality.Core.Songs;
using Tuneality.Core.Songs.Scores;

namespace IZ.Client;

public class TuneClientCache : ClientCache {

  public TuneClientCache(ITuneContext context) : base(context) { }


  private readonly Dictionary<string, Score> _scores = new Dictionary<string, Score>();
  public async Task<Score> LoadScore(string scoreId) {
    var score = Get<Score>(scoreId);
    if (score != null) return score;
    // if (_scores.TryGetValue(scoreId, out var v)) return v;
    score = await Context.BeginRequest<SongsQuery>().GetScore(scoreId).Execute("Score");
    score.AssignParents();
    Set(score);
    return score;
  }
}
