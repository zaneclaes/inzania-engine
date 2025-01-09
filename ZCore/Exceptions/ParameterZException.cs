#region

using System;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Exceptions;

public class ParameterZException : ZException {
  public ParameterZException(IZContext context, string message, Exception? innerException = null) :
    base(context, message, innerException) { }
}
