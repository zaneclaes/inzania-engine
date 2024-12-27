#region

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Tuneality.Core.Songs.Sources;

#endregion

namespace IZ.Data.Seed;

public class TuneSeed : TransientObject {
  public static async Task CreateSeed(ITuneContext context) {
    for (var site = (SourceSite) 1; site < SourceSite.NumSites; site++) {
      string? scoresDir = Path.Combine(context.App.Storage.UserDir, "TuneSeed", "scores", site.ToString());
      // if (Directory.Exists(scoresDir)) {
      //   Directory.Delete(scoresDir, true);
      // }
      if (!Directory.Exists(scoresDir)) Directory.CreateDirectory(scoresDir);

      List<ScoreSource>? sources = await context.QueryFor<ScoreSource>()
        .Filter(s => s.Site == site).LoadDataModelsAsync();
      foreach (var source in sources) { }
    }
  }

  public static void ImportSeed(string zipFn) { }
}
