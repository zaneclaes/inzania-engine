#region

using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Api;

[ApiDocs("Passed into Execution logic to define what fields to include in the response")]
public class ResultSet {
  public string? Format { get; set; }

  public override string ToString() => $"{Format}";
}
