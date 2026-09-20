using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RezervBooking.Application;

namespace RezervBooking.Infrastructure;

public sealed class WaitlistExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<WaitlistExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
                var count = await service.ExpireWaitlistsAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Expired {WaitlistEntryCount} waitlist entries.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Waitlist expiry job failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
