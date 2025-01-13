#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Observability.Logging;

public class ZError : TransientObject {
  public static Func<Exception, ZError?> TransformException = e => null;

  public ZError(string name, string message, IZContext context, Exception? ex = null) : base(context) {
    Name = name;
    Message = message;
    InnerException = ex == null ? null : Guard(ex);
  }

  public ZError(Exception ex, IZContext? context = null) : base(context) {
    Name = ex.GetType().Name;
    Message = ex.Message;
    StackTrace.AddRange(ex.GetStackTrace());
    Data = ex.Data;
    InnerException = ex.InnerException != null ? Guard(ex.InnerException, context) : null;
  }
  [JsonPropertyName("name")] public string Name { get; set; }

  [JsonPropertyName("message")] public string Message { get; set; }

  [JsonPropertyName("stackTrace")] public List<string> StackTrace { get; set; } = new List<string>();

  [ApiIgnore] [JsonIgnore] public IDictionary Data { get; } = null!;

  [JsonPropertyName("innerException")] public ZError? InnerException { get; set; }

  public static ZError Guard(Exception ex, IZContext? context = null) => TransformException(ex) ?? new ZError(ex, context);

  private string ExceptionStr => InnerException == null ? "" : (" (" + InnerException.ToString() + ")");

  public override string ToString() => $"[{Name}] {Message}{ExceptionStr}";
}
