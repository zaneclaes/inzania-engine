#region

using System;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Exceptions;

[ApiDocs("When something happens unrelated to parameters or remote services, failing due to internal logic instead")]
public class InternalTuneException : TuneException {
  public InternalTuneException(ITuneContext context, string message, Exception? innerException = null) :
    base(context, message, innerException) { }
}
