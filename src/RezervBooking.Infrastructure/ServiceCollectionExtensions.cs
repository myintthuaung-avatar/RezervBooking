using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RezervBooking.Application;
using StackExchange.Redis;

namespace RezervBooking.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBookingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var mysql = configuration.GetConnectionString("MySql") ?? throw new InvalidOperationException("ConnectionStrings:MySql is required.");
        var redis = configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");
        // Booking workflows explicitly control a serializable transaction; Pomelo's retry strategy cannot wrap user-started transactions.
        services.AddDbContext<BookingDbContext>(options => options.UseMySql(mysql, ServerVersion.AutoDetect(mysql)));
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
        services.AddScoped<IBookingService, BookingService>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IScheduleLock, RedisScheduleLock>();
        services.AddScoped<DatabaseInitializer>();
        services.AddHostedService<WaitlistExpiryWorker>();
        return services;
    }
}
