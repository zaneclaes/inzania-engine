using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

namespace IZ.Client.GoogleAnalytics.Events;

public class FileDownloadParams : BaseParams {
  [JsonPropertyName("file_name")] public string FileName { get; set; } = null!;
  [JsonPropertyName("file_extension")] public string FileExtension { get; set; } = null!;
  [JsonPropertyName("file_url")] public string FileUrl { get; set; } = null!;
}
