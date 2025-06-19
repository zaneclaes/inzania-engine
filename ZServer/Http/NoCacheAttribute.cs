using Microsoft.AspNetCore.Mvc.Filters;

namespace IZ.Server.Http;

public class NoCacheAttribute : ActionFilterAttribute {
  public override void OnActionExecuting(ActionExecutingContext context)
  {
    var headers = context.HttpContext.Response.Headers;
    headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
    headers["Pragma"] = "no-cache";
    headers["Expires"] = "0";

    base.OnActionExecuting(context);
  }
}
