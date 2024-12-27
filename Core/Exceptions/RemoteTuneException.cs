#region

using System;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Exceptions;

[ApiDocs("A remote service failed (i.e., HTTP request)")]
public class RemoteTuneException : TuneException {
  public RemoteTuneException(ITuneContext context, string message, Exception? innerException = null) :
    base(context, message, innerException) { }
}
