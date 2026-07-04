namespace Trading.Bot.Services;

public class EmailService(EmailConfiguration emailConfig, ILogger<EmailService> logger)
{
    public async Task SendMailAsync(EmailData emailData)
    {
        try
        {
            using var emailMessage = new MimeMessage();

            var emailFrom = new MailboxAddress(emailConfig.UserName, emailConfig.From);

            emailMessage.From.Add(emailFrom);

            var emailTo = new MailboxAddress(emailData.EmailToName, emailData.EmailToAddress);

            emailMessage.To.Add(emailTo);

            emailMessage.Subject = emailData.EmailSubject;

            var emailBodyBuilder = new BodyBuilder
            {
                TextBody = emailData.EmailBody
            };

            emailMessage.Body = emailBodyBuilder.ToMessageBody();

            using var mailClient = new SmtpClient();

            await mailClient.ConnectAsync(emailConfig.SmtpServer, emailConfig.Port,
                MailKit.Security.SecureSocketOptions.StartTls);

            await mailClient.AuthenticateAsync(emailConfig.UserName, emailConfig.Password);

            await mailClient.SendAsync(emailMessage);

            await mailClient.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send an email.");
        }
    }
}