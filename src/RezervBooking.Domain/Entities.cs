namespace RezervBooking.Domain;

public sealed class Business
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<TimetableSchedule> Schedules { get; set; } = new List<TimetableSchedule>();
}

public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ICollection<Package> Packages { get; set; } = new List<Package>();
}

public sealed class Package
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int BusinessId { get; set; }
    public int TotalCredits { get; set; }
    public int RemainingCredits { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime PurchasedAtUtc { get; set; }
    public Customer Customer { get; set; } = null!;
    public Business Business { get; set; } = null!;
}

public sealed class TimetableSchedule
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public int Capacity { get; set; }
    public Business Business { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();
}

public enum BookingStatus { Confirmed, Cancelled }

public sealed class Booking
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int TimetableScheduleId { get; set; }
    public int PackageId { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTime BookedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public bool CreditRefunded { get; set; }
    public Customer Customer { get; set; } = null!;
    public TimetableSchedule TimetableSchedule { get; set; } = null!;
    public Package Package { get; set; } = null!;
}

public enum WaitlistStatus { Waiting, Promoted, Expired, Cancelled }

public sealed class WaitlistEntry
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int TimetableScheduleId { get; set; }
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;
    public DateTime JoinedAtUtc { get; set; }
    public DateTime? PromotedAtUtc { get; set; }
    public Customer Customer { get; set; } = null!;
    public TimetableSchedule TimetableSchedule { get; set; } = null!;
}
