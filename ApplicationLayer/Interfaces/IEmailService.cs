namespace ApplicationLayer.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);
    Task SendEmailVerificationAsync(string toEmail, string toName, string verifyLink);
}
