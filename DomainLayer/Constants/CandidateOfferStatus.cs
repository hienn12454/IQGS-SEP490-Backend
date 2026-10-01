namespace DomainLayer.Constants;

public static class CandidateOfferStatus
{
    public const string Sent = "SENT";
    public const string Accepted = "ACCEPTED";
    /// <summary>SCRUM-482: candidate từ chối lời mời/offer.</summary>
    public const string Rejected = "REJECTED";
}
