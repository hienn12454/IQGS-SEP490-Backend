namespace ApplicationLayer.ResponseCode;

public static class RespCode
{
  // default
  public const int OK = 200;
  public const int CREATED = 201;
  public const int ACCEPTED = 202;
  public const int REDIRECT = 302;
  public const int NO_CONTENT = 204;
  public const int BAD_REQUEST = 400;
  public const int UNAUTHORIZED = 401;
  public const int FORBIDDEN = 403;
  public const int NOT_FOUND = 404;
  public const int METHOD_NOT_ALLOWED = 405;
  public const int INTERNAL_SERVER_ERROR = 500;
}

public static class RespMsg
{
  public const string OK = "OK";
  public const string CREATED = "Created";
  public const string ACCEPTED = "Accepted";
  public const string NO_CONTENT = "No Content";
  public const string BAD_REQUEST = "Bad Request";
  public const string UNAUTHORIZED = "Unauthorized";
  public const string FORBIDDEN = "Forbidden";
  public const string NOT_FOUND = "Not Found";
  public const string METHOD_NOT_ALLOWED = "Method Not Allowed";
  public const string INTERNAL_SERVER_ERROR = "Internal Server Error";
}