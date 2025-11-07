#region

using System;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Exceptions;

[ApiDocs("A remote service failed (i.e., HTTP request)")]
public class RemoteZException : ZException {
  public string? ResponseBody  { get; private set; }

  public RemoteZException(IZContext context, string message, Exception? innerException = null) :
    base(context, message, innerException) { }

  public RemoteZException(IZContext context, string message, string reason) : base(context, message) {
    Reason = reason;
  }

  public RemoteZException WithResponseBody(string? body) {
    if (body != null) ResponseBody = body;
    return this;
  }
}
