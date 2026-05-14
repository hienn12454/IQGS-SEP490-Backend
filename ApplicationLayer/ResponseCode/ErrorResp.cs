namespace ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Mvc;

public static class ErrorResp
{
  public static IActionResult InternalServerError(string? message)
  {
    return new JsonResult(new { Error = message ?? RespMsg.INTERNAL_SERVER_ERROR, Code = RespCode.INTERNAL_SERVER_ERROR }) { StatusCode = RespCode.INTERNAL_SERVER_ERROR };
  }

  public static IActionResult NotFound(string? message)
  {
    return new JsonResult(new { Error = message ?? RespMsg.NOT_FOUND, Code = RespCode.NOT_FOUND }) { StatusCode = RespCode.NOT_FOUND };
  }

  public static IActionResult BadRequest(string? message)
  {
    return new JsonResult(new { Error = message ?? RespMsg.BAD_REQUEST, Code = RespCode.BAD_REQUEST }) { StatusCode = RespCode.BAD_REQUEST };
  }
  public static IActionResult Unauthorized(string? message)
  {
    return new JsonResult(new { Error = message ?? RespMsg.UNAUTHORIZED, Code = RespCode.UNAUTHORIZED }) { StatusCode = RespCode.UNAUTHORIZED };
  }

  public static IActionResult Forbidden(string? message)
  {
    return new JsonResult(new { Error = message ?? RespMsg.FORBIDDEN, Code = RespCode.FORBIDDEN }) { StatusCode = RespCode.FORBIDDEN };
  }

}
