#region

using System;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Exceptions;

public class ParameterTuneException : TuneException {
  public ParameterTuneException(ITuneContext context, string message, Exception? innerException = null) :
    base(context, message, innerException) { }
}
