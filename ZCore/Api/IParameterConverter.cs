using IZ.Core.Contexts;

namespace IZ.Core.Api;

public interface IParameterConverter {
  public object? ConvertParameter(object? param);
}
