#region

using System;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Exceptions;

[ApiDocs("A remote service failed (i.e., HTTP request)")]
public class RemoteZException : ZException {
  public RemoteZException(IZContext context, string message, Exception? innerException = null) :
    base(context, message, innerException) { }
}
