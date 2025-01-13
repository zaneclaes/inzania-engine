#region

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#endregion

namespace IZ.Core.Data;

public class ModelChildId<TParent> : ModelId where TParent : ModelId {
  [Key] [Column(Order = 0)] [MaxLength(MaxIdLength)]
  public override string Id { get; set; } = null!;

  [Key] [Column(Order = 1)]
  public string ParentId { get; set; } = null!;

  public TParent Parent { get; set; } = null!;
}
