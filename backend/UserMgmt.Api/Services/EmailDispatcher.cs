using System.Threading.Channels;

namespace UserMgmt.Api.Services
{
    // A lightweight in-process email queue that feeds a background hosted service.
    // Emails are retried on failure and dropped only after exhausting all attempts.
    public class EmailDispatcher
    {
        private readonly Channel<EmailJob> _channel;

        public EmailDispatcher()
        {
            _channel = Channel.CreateUnbounded<EmailJob>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default)
        {
            return _channel.Writer.WriteAsync(job, ct);
        }

        public ValueTask<EmailJob> DequeueAsync(CancellationToken ct)
        {
            return _channel.Reader.ReadAsync(ct);
        }
    }

    public record EmailJob(string ToEmail, string ToName, Guid VerificationToken, int Attempt = 0);
}
