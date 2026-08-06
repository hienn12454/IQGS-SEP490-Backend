namespace DomainLayer.Constants;

public static class FeedbackStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static bool IsValid(string status) => status switch
    {
        Pending or Approved or Rejected => true,
        _ => false
    };
}
