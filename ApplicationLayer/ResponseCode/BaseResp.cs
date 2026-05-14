namespace ApplicationLayer.ResponseCode;

public class BaseResp
{
  public int Code { get; set; }
  public string Message { get; set; } = null!;
}

public class GenericResp<T> : BaseResp
{
  public T? Data { get; set; }
}

