using System.Text.Json.Serialization;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Navigation.SchemaOrg;

/// <summary>
/// One `Question` of an `FAQPage`, with the single `Answer` schema.org allows it. Both halves are
/// required: a question with no accepted answer is dropped from the rich result rather than shown
/// empty.
/// </summary>
public class SchemaQuestionJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = "Question";

  [JsonPropertyName("name")] public string Name { get; set; } = null!;

  [JsonPropertyName("acceptedAnswer")] [ApiFormat] public SchemaAnswerJson? AcceptedAnswer { get; set; }
}

public class SchemaAnswerJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = "Answer";

  /// <summary>The answer's body. May contain a little HTML; plain text is safer and is what we emit.</summary>
  [JsonPropertyName("text")] public string Text { get; set; } = null!;
}
