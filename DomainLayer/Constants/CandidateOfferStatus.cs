namespace DomainLayer.Constants;

public static class CandidateOfferStatus
{
    public const string Sent = "SENT";
    public const string Accepted = "ACCEPTED";
    /// <summary>SCRUM-482: candidate từ chối in-app — sync để LatestOfferStatus không kẹt SENT.</summary>
    public const string Rejected = "REJECTED";
}
