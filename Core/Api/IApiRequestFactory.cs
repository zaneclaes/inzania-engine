using IZ.Core.Contexts;

namespace IZ.Core.Api;

public interface IApiRequestFactory {
  public TReq CreateApiRequest<TReq>(ITuneContext context) where TReq : ZRequestBase;
}
