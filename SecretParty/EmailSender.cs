using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace SecretParty.Web.Data
{
	public class EmailSender : IEmailSender
	{
		private readonly IConfiguration _configuration;
		private readonly ILogger _logger;

		public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
		{
			_configuration = configuration;
			_logger = logger;
		}

		public async Task SendEmailAsync(string toEmail, string subject, string message)
		{
			if (string.IsNullOrEmpty(_configuration["SendGridKey"]))
			{
				throw new Exception("Null SendGridKey");
			}
			await Execute(_configuration["SendGridKey"], subject, message, toEmail);
		}

		public async Task Execute(string apiKey, string subject, string message, string toEmail)
		{
			var client = new SendGridClient(apiKey);
			var msg = new SendGridMessage()
			{
				From = new EmailAddress("support@SecretParty.net", "SecretParty.net"),
				Subject = subject,
				PlainTextContent = message,
				HtmlContent = message
			};
			msg.AddTo(new EmailAddress(toEmail));

			// Disable click tracking.
			// See https://sendgrid.com/docs/User_Guide/Settings/tracking.html
			msg.SetClickTracking(false, false);
			var response = await client.SendEmailAsync(msg);
			string result = await response.Body.ReadAsStringAsync();
			_logger.LogInformation(response.IsSuccessStatusCode
				? $"Email to {toEmail} queued successfully!"
				: $"Failure Email to {toEmail}");
		}
	}
}
