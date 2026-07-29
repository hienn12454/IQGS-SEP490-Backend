namespace ApplicationLayer.DTOs.Subscription;

/// <summary>Payload SignalR event PaymentPaid — FE đóng modal và refresh subscription.</summary>
public class SubscriptionPaymentPaidEventDto
{
    public string OrderCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Paid";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime ConfirmedAt { get; set; }
}
