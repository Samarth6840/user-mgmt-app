namespace UserMgmt.Api.Services
{
    // Pulls email jobs off the channel and sends them via EmailService.
    // Retries up to 3 times with a short delay between attempts.
    public class EmailDispatcherHostedService : BackgroundService
    {
        private readonly EmailDispatcher _dispatcher;
        private readonly EmailService _email;
        private readonly ILogger<EmailDispatcherHostedService> _logger;
        private const int MaxAttempts = 3;

        public EmailDispatcherHostedService(EmailDispatcher dispatcher, EmailService email, ILogger<EmailDispatcherHostedService> logger)
        {
            _dispatcher = dispatcher;
            _email = email;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _dispatcher.DequeueAsync(stoppingToken);

                    if (job.Attempt >= MaxAttempts)
                    {
                        _logger.LogWarning("Dropping e-mail to {Email} after {Attempts} failed attempts", job.ToEmail, job.Attempt);
                        continue;
                    }

                    try
                    {
                        await _email.SendVerificationEmailAsync(job.ToEmail, job.ToName, job.VerificationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "E-mail send attempt {Attempt}/{Max} failed for {Email}", job.Attempt + 1, MaxAttempts, job.ToEmail);
                        await _dispatcher.EnqueueAsync(job with { Attempt = job.Attempt + 1 }, stoppingToken);
                        await Task.Delay(TimeSpan.FromSeconds(2 * (job.Attempt + 1)), stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in e-mail dispatcher");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
    }
}
