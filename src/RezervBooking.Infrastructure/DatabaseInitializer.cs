using Microsoft.EntityFrameworkCore;
using RezervBooking.Application;
using RezervBooking.Domain;

namespace RezervBooking.Infrastructure;

public sealed class DatabaseInitializer(BookingDbContext db, IClock clock)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        if (await db.Businesses.AnyAsync(ct)) return;
        var fitness = new Business { Name = "Rezerv Fitness" };
        var wellness = new Business { Name = "Rezerv Wellness" };
        var customers = Enumerable.Range(1, 10).Select(i => new Customer { Name = $"Customer {i}", Email = $"customer{i}@example.test" }).ToList();
        db.AddRange(fitness, wellness); db.AddRange(customers); await db.SaveChangesAsync(ct);

        var today = DateTime.UtcNow.Date.AddDays(2);
        var schedules = new List<TimetableSchedule>();
        for (var i = 0; i < 10; i++)
        {
            var start = today.AddDays(i / 4).AddHours(8 + (i % 4) * 2);
            schedules.Add(new TimetableSchedule { BusinessId = i % 2 == 0 ? fitness.Id : wellness.Id, ClassName = i % 2 == 0 ? "Yoga Flow" : "Pilates Reformer", InstructorName = i % 2 == 0 ? "John Doe" : "Jane Smith", StartTimeUtc = start, EndTimeUtc = start.AddHours(1), Capacity = i == 0 ? 5 : 12 });
        }
        db.Schedules.AddRange(schedules);
        db.Packages.AddRange(customers.Select((customer, index) => new Package { CustomerId = customer.Id, BusinessId = index == 9 ? wellness.Id : fitness.Id, TotalCredits = 10, RemainingCredits = 10, PurchasedAtUtc = clock.UtcNow, ExpiresAtUtc = index == 9 ? clock.UtcNow.AddDays(-1) : clock.UtcNow.AddDays(30) }));
        await db.SaveChangesAsync(ct);

        var fitnessPackages = await db.Packages.Where(x => x.BusinessId == fitness.Id).OrderBy(x => x.CustomerId).ToListAsync(ct);
        for (var i = 0; i < 5; i++)
        {
            fitnessPackages[i].RemainingCredits--;
            db.Bookings.Add(new Booking { CustomerId = customers[i].Id, TimetableScheduleId = schedules[0].Id, PackageId = fitnessPackages[i].Id, BookedAtUtc = clock.UtcNow.AddDays(-1) });
        }
        db.WaitlistEntries.Add(new WaitlistEntry { CustomerId = customers[5].Id, TimetableScheduleId = schedules[0].Id, JoinedAtUtc = clock.UtcNow.AddHours(-2) });
        await db.SaveChangesAsync(ct);
    }
}
