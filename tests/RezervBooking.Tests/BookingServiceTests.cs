using Microsoft.EntityFrameworkCore;
using RezervBooking.Application;
using RezervBooking.Domain;
using RezervBooking.Infrastructure;

namespace RezervBooking.Tests;

public sealed class BookingServiceTests
{
    [Fact]
    public async Task Booking_deducts_the_earliest_expiring_eligible_package_credit()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Service.BookAsync(new(fixture.Customer.Id, fixture.Schedule.Id), default);

        Assert.Equal(BookingStatusDto.Confirmed, result.Status);
        Assert.Equal(1, await fixture.Db.Bookings.CountAsync(x => x.Status == BookingStatus.Confirmed));
        Assert.Equal(1, (await fixture.Db.Packages.SingleAsync()).RemainingCredits);
    }

    [Fact]
    public async Task Booking_rejects_an_overlapping_confirmed_booking()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.Bookings.Add(new Booking { CustomerId = fixture.Customer.Id, TimetableScheduleId = fixture.Schedule.Id, PackageId = fixture.Package.Id, BookedAtUtc = fixture.Clock.UtcNow });
        fixture.Db.Schedules.Add(new TimetableSchedule { BusinessId = fixture.Business.Id, ClassName = "Overlapping", InstructorName = "Coach", StartTimeUtc = fixture.Schedule.StartTimeUtc.AddMinutes(30), EndTimeUtc = fixture.Schedule.EndTimeUtc.AddMinutes(30), Capacity = 10 });
        await fixture.Db.SaveChangesAsync();
        var other = await fixture.Db.Schedules.OrderByDescending(x => x.Id).FirstAsync();

        var exception = await Assert.ThrowsAsync<DomainException>(() => fixture.Service.BookAsync(new(fixture.Customer.Id, other.Id), default));
        Assert.Contains("overlapping", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Early_cancellation_refunds_and_promotes_the_first_waitlisted_customer()
    {
        await using var fixture = await Fixture.CreateAsync(capacity: 1);
        var secondCustomer = new Customer { Name = "Waitlisted", Email = "waitlisted@example.test" };
        fixture.Db.Customers.Add(secondCustomer); await fixture.Db.SaveChangesAsync();
        var secondPackage = new Package { CustomerId = secondCustomer.Id, BusinessId = fixture.Business.Id, TotalCredits = 2, RemainingCredits = 2, ExpiresAtUtc = fixture.Clock.UtcNow.AddDays(1), PurchasedAtUtc = fixture.Clock.UtcNow };
        fixture.Db.Packages.Add(secondPackage);
        fixture.Package.RemainingCredits--; // Simulate the credit consumed when the original booking was made.
        fixture.Db.Bookings.Add(new Booking { CustomerId = fixture.Customer.Id, TimetableScheduleId = fixture.Schedule.Id, PackageId = fixture.Package.Id, BookedAtUtc = fixture.Clock.UtcNow });
        fixture.Db.WaitlistEntries.Add(new WaitlistEntry { CustomerId = secondCustomer.Id, TimetableScheduleId = fixture.Schedule.Id, JoinedAtUtc = fixture.Clock.UtcNow.AddMinutes(-5) });
        await fixture.Db.SaveChangesAsync();
        var booking = await fixture.Db.Bookings.SingleAsync();

        var result = await fixture.Service.CancelAsync(new(fixture.Customer.Id, booking.Id), default);

        Assert.True(result.Refunded);
        Assert.NotNull(result.PromotedWaitlistEntryId);
        Assert.Equal(2, (await fixture.Db.Packages.FindAsync(fixture.Package.Id))!.RemainingCredits);
        Assert.Equal(1, (await fixture.Db.Packages.FindAsync(secondPackage.Id))!.RemainingCredits);
        Assert.Equal(WaitlistStatus.Promoted, (await fixture.Db.WaitlistEntries.SingleAsync())!.Status);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required BookingDbContext Db { get; init; }
        public required BookingService Service { get; init; }
        public required FixedClock Clock { get; init; }
        public required Business Business { get; init; }
        public required Customer Customer { get; init; }
        public required Package Package { get; init; }
        public required TimetableSchedule Schedule { get; init; }

        public static async Task<Fixture> CreateAsync(int capacity = 10)
        {
            var options = new DbContextOptionsBuilder<BookingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options;
            var db = new BookingDbContext(options); var clock = new FixedClock(DateTime.UtcNow);
            var business = new Business { Name = "Fitness" }; var customer = new Customer { Name = "Customer", Email = "customer@example.test" };
            db.AddRange(business, customer); await db.SaveChangesAsync();
            var package = new Package { CustomerId = customer.Id, BusinessId = business.Id, TotalCredits = 2, RemainingCredits = 2, PurchasedAtUtc = clock.UtcNow, ExpiresAtUtc = clock.UtcNow.AddDays(1) };
            var schedule = new TimetableSchedule { BusinessId = business.Id, ClassName = "Yoga", InstructorName = "Coach", StartTimeUtc = clock.UtcNow.AddDays(1), EndTimeUtc = clock.UtcNow.AddDays(1).AddHours(1), Capacity = capacity };
            db.AddRange(package, schedule); await db.SaveChangesAsync();
            return new() { Db = db, Clock = clock, Business = business, Customer = customer, Package = package, Schedule = schedule, Service = new BookingService(db, new AlwaysAvailableLock(), clock) };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class FixedClock(DateTime utcNow) : IClock { public DateTime UtcNow => utcNow; }
    private sealed class AlwaysAvailableLock : IScheduleLock { public Task<IAsyncDisposable?> TryAcquireAsync(int scheduleId, CancellationToken cancellationToken) => Task.FromResult<IAsyncDisposable?>(new LockHandle()); private sealed class LockHandle : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; } }
}
