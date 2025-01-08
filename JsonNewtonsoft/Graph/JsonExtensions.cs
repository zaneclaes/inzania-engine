#region

using IZ.Core.Contexts;
using IZ.Core.Data;
using Newtonsoft.Json.Linq;

#endregion

namespace IZ.Json.Newtonsoft.Graph;

public static class JsonExtensions {
  public static TData? ToDataModel<TData>(this JObject obj, ITuneContext context) where TData : DataObject, new() => (TData?) GraphJson.ConvertObject(context, typeof(TData), obj);

  public static TData[] ToDataModels<TData>(this JArray obj, ITuneContext context) where TData : DataObject, new() => (TData[]) GraphJson.ConvertArray(context, typeof(TData), obj);
}
