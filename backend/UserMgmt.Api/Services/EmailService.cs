using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

#pragma warning disable CS0618

namespace UserMgmt.Api.Services
{
    // Sends the account-verification e-mail after a new user registers.
    // Uses the SendGrid HTTP API when configured (works on hosts that block SMTP),
    // otherwise falls back to plain SMTP (e.g. local development).
    public class EmailService
    {
        private const string SendGridEndpoint = "https://api.sendgrid.com/v3/mail/send";

        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<EmailService> _logger;
        private readonly HttpClient _http;

        public EmailService(HttpClient http, IConfiguration config, IWebHostEnvironment env, ILogger<EmailService> logger)
        {
            _http = http;
            _config = config;
            _env = env;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string toName, Guid verificationToken)
        {
            try
            {
                var appUrl = _config["App:PublicUrl"]
                    ?? (_env.IsDevelopment() ? "http://localhost:5173"
                        : throw new InvalidOperationException("App:PublicUrl must be configured in non-Development environments."));

                var subject = "Verify your e-mail";
                var body = $"Hi {toName},\n\nPlease verify your e-mail by clicking the link below:\n{appUrl}/verify?token={verificationToken}\n\nIf you didn't sign up, ignore this message.";

                var apiKey = _config["SendGrid:ApiKey"];
                var from = _config["SendGrid:From"];

                if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(from))
                {
                    await SendWithSendGridAsync(apiKey, from, toEmail, subject, body);
                }
                else
                {
                    await SendWithSmtpAsync(toEmail, subject, body);
                }
            }
            catch (Exception ex)
            {
                // A failed e-mail should never prevent registration from completing.
                _logger.LogWarning(ex, "Failed to send verification e-mail to {Email}", toEmail);
            }
        }

        private async Task SendWithSendGridAsync(string apiKey, string from, string toEmail, string subject, string body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SendGridEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    personalizations = new[] { new { to = new[] { new { email = toEmail } } } },
                    from = new { email = from, name = "User Management App" },
                    subject,
                    content = new[] { new { type = "text/plain", value = body } }
                }),
                Encoding.UTF8,
                "application/json");

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"SendGrid returned {(int)response.StatusCode}: {error}");
            }
        }

        private async Task SendWithSmtpAsync(string toEmail, string subject, string body)
        {
            var smtpHost = _config["Smtp:Host"];
            var smtpPort = int.Parse(_config["Smtp:Port"] ?? "587");
            var smtpUser = _config["Smtp:User"];
            var smtpPass = _config["Smtp:Password"];

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            using var message = new MailMessage
            {
                From = new MailAddress(smtpUser ?? "no-reply@example.com", "User Management App"),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
        }
    }
}
