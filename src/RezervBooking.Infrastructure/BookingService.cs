using System.Data;
using Microsoft.EntityFrameworkCore;
using RezervBooking.Application;
using RezervBooking.Domain;

namespace RezervBooking.Infrastructure;

public sealed class BookingService(BookingDbContext db, IScheduleLock scheduleLock, IClock clock) : IBookingService
{
    public async Task<IReadOnlyList<PackageDto>> GetPackagesAsync(int customerId, CancellationToken ct) =>
        await db.Packages.AsNoTracking().Where(x => x.CustomerId == customerId).OrderBy(x => x.ExpiresAtUtc)
            .Select(x => new PackageDto(x.Id, x.CustomerId, x.BusinessId, x.Business.Name, x.TotalCredits, x.RemainingCredits, x.ExpiresAtUtc, x.ExpiresAtUtc <= clock.UtcNow))
            .ToListAsync(ct);

    public async Task<PackageDto> PurchasePackageAsync(PurchasePackageRequest request, CancellationToken ct)
    {
        if (request.Credits <= 0) throw new DomainException("Credits must be greater than zero.");
        if (request.ExpiresAtUtc <= clock.UtcNow) throw new DomainException("Package expiry must be in the future.");
        if (!await db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct)) throw new DomainException("Customer was not found.");
        var business = await db.Businesses.FindAsync([request.BusinessId], ct) ?? throw new DomainException("Business was not found.");
        var package = new Package { CustomerId = request.CustomerId, BusinessId = request.BusinessId, TotalCredits = request.Credits, RemainingCredits = request.Credits, ExpiresAtUtc = request.ExpiresAtUtc, PurchasedAtUtc = clock.UtcNow };
        db.Packages.Add(package); await db.SaveChangesAsync(ct);
        return new(package.Id, package.CustomerId, package.BusinessId, business.Name, package.TotalCredits, package.RemainingCredits, package.ExpiresAtUtc, false);
    }

    public async Task<IReadOnlyList<ScheduleDto>> GetSchedulesAsync(int? businessId, DateOnly? date, CancellationToken ct)
    {
        var query = db.Schedules.AsNoTracking().AsQueryable();
        if (businessId.HasValue) query = query.Where(x => x.BusinessId == businessId);
        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc); var end = start.AddDays(1);
            query = query.Where(x => x.StartTimeUtc >= start && x.StartTimeUtc < end);
        }
        return await query.OrderBy(x => x.StartTimeUtc).Select(x => new ScheduleDto(x.Id, x.ClassName, x.InstructorName, x.StartTimeUtc, x.EndTimeUtc,
            x.Bookings.Count(b => b.Status == BookingStatus.Confirmed), x.Capacity - x.Bookings.Count(b => b.Status == BookingStatus.Confirmed), x.BusinessId, x.Business.Name)).ToListAsync(ct);
    }

    public async Task<BookingResult> BookAsync(CreateBookingRequest request, CancellationToken ct)
    {
        await using var handle = await AcquireAsync(request.ScheduleId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var schedule = await GetScheduleAsync(request.ScheduleId, ct);
        EnsureFuture(schedule);
        if (await AttendanceAsync(schedule.Id, ct) >= schedule.Capacity) throw new DomainException("This class is full. Join its waitlist instead.");
        await EnsureNoOverlapAsync(request.CustomerId, schedule, ct);
        var package = await FindUsablePackageAsync(request.CustomerId, schedule.BusinessId, ct);
        package.RemainingCredits--;
        var booking = new Booking { CustomerId = request.CustomerId, TimetableScheduleId = schedule.Id, PackageId = package.Id, BookedAtUtc = clock.UtcNow };
        db.Bookings.Add(booking); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(booking.Id, schedule.Id, BookingStatusDto.Confirmed, "Booking confirmed; one credit was deducted.");
    }

    public async Task<WaitlistResult> JoinWaitlistAsync(JoinWaitlistRequest request, CancellationToken ct)
    {
        await using var handle = await AcquireAsync(request.ScheduleId, ct);
        var schedule = await GetScheduleAsync(request.ScheduleId, ct); EnsureFuture(schedule);
        if (await AttendanceAsync(schedule.Id, ct) < schedule.Capacity) throw new DomainException("This class still has space; book it directly instead.");
        if (await db.WaitlistEntries.AnyAsync(x => x.CustomerId == request.CustomerId && x.TimetableScheduleId == request.ScheduleId && x.Status == WaitlistStatus.Waiting, ct)) throw new DomainException("Customer is already on this waitlist.");
        await EnsureNoOverlapAsync(request.CustomerId, schedule, ct);
        var entry = new WaitlistEntry { CustomerId = request.CustomerId, TimetableScheduleId = request.ScheduleId, JoinedAtUtc = clock.UtcNow };
        db.WaitlistEntries.Add(entry); await db.SaveChangesAsync(ct);
        return new(entry.Id, "Added to the waitlist. No credit has been deducted.");
    }

    public async Task<CancelBookingResult> CancelAsync(CancelBookingRequest request, CancellationToken ct)
    {
        var booking = await db.Bookings.Include(x => x.TimetableSchedule).Include(x => x.Package).SingleOrDefaultAsync(x => x.Id == request.BookingId && x.CustomerId == request.CustomerId, ct) ?? throw new DomainException("Confirmed booking was not found.");
        await using var handle = await AcquireAsync(booking.TimetableScheduleId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (booking.Status != BookingStatus.Confirmed) throw new DomainException("Booking has already been cancelled.");
        booking.Status = BookingStatus.Cancelled; booking.CancelledAtUtc = clock.UtcNow;
        var refunded = booking.TimetableSchedule.StartTimeUtc > clock.UtcNow.AddHours(4);
        if (refunded) { booking.Package.RemainingCredits++; booking.CreditRefunded = true; }
        // Persist the cancellation inside the transaction before checking capacity for promotion.
        await db.SaveChangesAsync(ct);
        var promotedId = await TryPromoteFirstWaitlistedCustomerAsync(booking.TimetableSchedule, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(booking.Id, refunded, promotedId, refunded ? "Booking cancelled and credit refunded." : "Booking cancelled; cancellation was within four hours so no refund was issued.");
    }

    public async Task<int> ExpireWaitlistsAsync(CancellationToken ct)
    {
        var expired = await db.WaitlistEntries.Include(x => x.TimetableSchedule).Where(x => x.Status == WaitlistStatus.Waiting && x.TimetableSchedule.EndTimeUtc <= clock.UtcNow).ToListAsync(ct);
        foreach (var item in expired) item.Status = WaitlistStatus.Expired;
        return expired.Count == 0 ? 0 : await db.SaveChangesAsync(ct);
    }

    private async Task<int?> TryPromoteFirstWaitlistedCustomerAsync(TimetableSchedule schedule, CancellationToken ct)
    {
        if (schedule.StartTimeUtc <= clock.UtcNow || await AttendanceAsync(schedule.Id, ct) >= schedule.Capacity) return null;
        var entry = await db.WaitlistEntries.Include(x => x.Customer).Where(x => x.TimetableScheduleId == schedule.Id && x.Status == WaitlistStatus.Waiting).OrderBy(x => x.JoinedAtUtc).FirstOrDefaultAsync(ct);
        if (entry is null) return null;
        var package = await db.Packages.Where(x => x.CustomerId == entry.CustomerId && x.BusinessId == schedule.BusinessId && x.RemainingCredits > 0 && x.ExpiresAtUtc > clock.UtcNow).OrderBy(x => x.ExpiresAtUtc).FirstOrDefaultAsync(ct);
        if (package is null || await HasOverlapAsync(entry.CustomerId, schedule, ct)) return null;
        package.RemainingCredits--; entry.Status = WaitlistStatus.Promoted; entry.PromotedAtUtc = clock.UtcNow;
        db.Bookings.Add(new Booking { CustomerId = entry.CustomerId, TimetableScheduleId = schedule.Id, PackageId = package.Id, BookedAtUtc = clock.UtcNow });
        return entry.Id;
    }

    private async Task<IAsyncDisposable> AcquireAsync(int scheduleId, CancellationToken ct) => await scheduleLock.TryAcquireAsync(scheduleId, ct) ?? throw new DomainException("This schedule is busy. Please retry shortly.");
    private async Task<TimetableSchedule> GetScheduleAsync(int id, CancellationToken ct) =>
        await db.Schedules.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("Schedule was not found.");
    private void EnsureFuture(TimetableSchedule schedule) { if (schedule.StartTimeUtc <= clock.UtcNow) throw new DomainException("Cannot book or waitlist a class that has started."); }
    private Task<int> AttendanceAsync(int id, CancellationToken ct) => db.Bookings.CountAsync(x => x.TimetableScheduleId == id && x.Status == BookingStatus.Confirmed, ct);
    private async Task<Package> FindUsablePackageAsync(int customerId, int businessId, CancellationToken ct) => await db.Packages.Where(x => x.CustomerId == customerId && x.BusinessId == businessId && x.RemainingCredits > 0 && x.ExpiresAtUtc > clock.UtcNow).OrderBy(x => x.ExpiresAtUtc).FirstOrDefaultAsync(ct) ?? throw new DomainException("No usable package credit exists for this business.");
    private async Task EnsureNoOverlapAsync(int customerId, TimetableSchedule schedule, CancellationToken ct) { if (await HasOverlapAsync(customerId, schedule, ct)) throw new DomainException("Customer already has an overlapping confirmed booking."); }
    private Task<bool> HasOverlapAsync(int customerId, TimetableSchedule schedule, CancellationToken ct) => db.Bookings.AnyAsync(x => x.CustomerId == customerId && x.Status == BookingStatus.Confirmed && x.TimetableSchedule.StartTimeUtc < schedule.EndTimeUtc && x.TimetableSchedule.EndTimeUtc > schedule.StartTimeUtc, ct);
}
