#region

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Data;

public interface IModelId<TKey> {
  TKey Id { get; set; }
}

public interface IStringKeyData : IModelId<string> { }

public abstract class ModelKey : DataObject {
  protected ModelKey(IZContext? context = null) : base(context) { }

  [JsonIgnore] [ApiIgnore]
  public abstract string? KeyId { get; }

  protected override string UuidId => KeyId ?? base.UuidId;
}

public abstract class ModelKey<TKey> : ModelKey, IModelId<TKey> {

  protected ModelKey(IZContext? context = null) : base(context) { }

  [JsonIgnore] [ApiIgnore]
  public override string? KeyId => Id?.ToString();

  public abstract TKey Id { get; set; }

  protected virtual void LoadIdFromParent<TParent>(TKey id, TParent? parent = null) where TParent : class {
    Id = id;
  }

  public virtual TData CreateChildModelId<TData, TChildKey>(TChildKey id) where TData : ModelKey<TChildKey>, new() {
    var ret = Context.CreateModelId<TData, TChildKey>(id); // Id + separator + typeof(TData).Name + id
    ret.LoadIdFromParent(id, this);
    return ret;
  }
}

public abstract class ModelNumber : ModelKey<long>, IModelChildren<long> {
  [Key] public override long Id { get; set; } = default!;
}

public abstract class ModelId : ModelKey<string>, IStringKeyData, IModelChildren<string> {
  public const int MaxIdLength = 128; // Guid length (32), plus lots of space for children expansion

  protected ModelId(IZContext? context = null, string? id = null) : base(context) {
    // ReSharper disable once VirtualMemberCallInConstructor
    if (id != null) Id = id;
  }

  [Key] [MaxLength(MaxIdLength)] public override string Id { get; set; } = default!;

  public static string CreateChildId(string parent, string child, string spacer = "-") => $"{child}{spacer}{parent}";

  public static Tuple<string?, string> RemoveLastChild(string parent, string spacer = "-") {
    int idx = parent.IndexOf(spacer, StringComparison.Ordinal);
    if (idx < 0) return new Tuple<string?, string>(null, parent);
    return new Tuple<string?, string>(parent.Substring(0, idx), parent.Substring(idx + spacer.Length));
  }

  public static Tuple<long, string> RemoveLongChild(string parent, string spacer = "-") {
    Tuple<string?, string> ch = RemoveLastChild(parent, spacer);
    return new Tuple<long, string>(ch.Item1 == null ? -1 : long.Parse(ch.Item1), ch.Item2);
  }

  public virtual string GetChildId(string child) => CreateChildId(Id, child);

  protected override void LoadIdFromParent<TParent>(string id, TParent? parent = null) where TParent : class {
    string chId = id;
    if (parent is ModelKey mk && mk.KeyId != null) {
      chId = CreateChildId(mk.KeyId, chId);
      if (chId.Length >= MaxIdLength)
        Log.Warning("[MODEL] ID length {len} for {type}#{id}", chId.Length, GetType().Name, chId);
    }
    Id = chId;
  }

  public static string GenerateId(int length = 16) => Guid.NewGuid().ToString()
    .Replace("-", "").Substring(0, length);
}
