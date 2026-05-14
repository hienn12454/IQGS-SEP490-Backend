namespace ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Mvc;

public static class SuccessResp
{
  public static IActionResult Ok(string? message)
  {
    return new JsonResult(new { Message = message ?? RespMsg.OK, Code = RespCode.OK }) { StatusCode = RespCode.OK };
  }

  public static IActionResult Ok(object? data)
  {
    var resp = new GenericResp<object> { Data = data, Code = RespCode.OK, Message = RespMsg.OK };

    return new JsonResult(resp) { StatusCode = RespCode.OK };
  }

  public static IActionResult Created(string? message)
  {
    return new JsonResult(new { Message = message ?? RespMsg.CREATED, Code = RespCode.CREATED }) { StatusCode = RespCode.CREATED };
  }

  public static IActionResult Created(object? data)
  {
    var resp = new GenericResp<object> { Data = data, Code = RespCode.CREATED, Message = RespMsg.CREATED };

    return new JsonResult(resp) { StatusCode = RespCode.CREATED };
  }

  public static IActionResult NoContent()
  {
    return new JsonResult(new { }) { StatusCode = RespCode.NO_CONTENT };
  }

  public static IActionResult Redirect(string url)
  {
    return new RedirectResult(url, false);
  }

  public static IActionResult Content(string htmlTemplate)
  {
    return new ContentResult { Content = htmlTemplate, ContentType = "text/html", StatusCode = RespCode.OK };
  }
}
