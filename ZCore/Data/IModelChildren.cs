namespace IZ.Core.Data;

public interface IModelChildren<TKey> {
  public TData CreateChildModelId<TData, TChildKey>(TChildKey id) where TData : ModelKey<TChildKey>, new();
}
