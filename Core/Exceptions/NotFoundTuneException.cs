#region

using System;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Exceptions;

public class NotFoundTuneException : TuneException {
  public NotFoundTuneException(ITuneContext context, string message, Exception? innerException = null) :
    base(context, message, innerException) { }
}
