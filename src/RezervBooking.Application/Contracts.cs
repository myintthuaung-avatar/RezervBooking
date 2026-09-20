namespace RezervBooking.Application;

public interface IBookingService
{
    Task<IReadOnlyList<PackageDto>> GetPackagesAsync(int customerId, CancellationToken cancellationToken);
    Task<PackageDto> PurchasePackageAsync(PurchasePackageRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScheduleDto>> GetSchedulesAsync(int? businessId, DateOnly? date, CancellationToken cancellationToken);
    Task<BookingResult> BookAsync(CreateBookingRequest request, CancellationToken cancellationToken);
    Task<CancelBookingResult> CancelAsync(CancelBookingRequest request, CancellationToken cancellationToken);
    Task<WaitlistResult> JoinWaitlistAsync(JoinWaitlistRequest request, CancellationToken cancellationToken);
    Task<int> ExpireWaitlistsAsync(CancellationToken cancellationToken);
}

public interface IScheduleLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(int scheduleId, CancellationToken cancellationToken);
}

public interface IClock { DateTime UtcNow { get; } }

public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
public sealed class DomainException(string message) : Exception(message);

public sealed record PurchasePackageRequest(int CustomerId, int BusinessId, int Credits, DateTime ExpiresAtUtc);
public sealed record CreateBookingRequest(int CustomerId, int ScheduleId);
public sealed record CancelBookingRequest(int CustomerId, int BookingId);
public sealed record JoinWaitlistRequest(int CustomerId, int ScheduleId);
public sealed record PackageDto(int Id, int CustomerId, int BusinessId, string BusinessName, int TotalCredits, int RemainingCredits, DateTime ExpiresAtUtc, bool IsExpired);
public sealed record ScheduleDto(int ScheduleId, string ClassName, string InstructorName, DateTime StartTimeUtc, DateTime EndTimeUtc, int AttendanceCount, int AvailableSlots, int BusinessId, string BusinessName);
public sealed record BookingResult(int BookingId, int ScheduleId, BookingStatusDto Status, string Message);
public sealed record CancelBookingResult(int BookingId, bool Refunded, int? PromotedWaitlistEntryId, string Message);
public sealed record WaitlistResult(int WaitlistEntryId, string Message);
public enum BookingStatusDto { Confirmed, Waitlisted }
