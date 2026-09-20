using Microsoft.EntityFrameworkCore;
using RezervBooking.Domain;

namespace RezervBooking.Infrastructure;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<TimetableSchedule> Schedules => Set<TimetableSchedule>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Business>().Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Entity<Customer>().HasIndex(x => x.Email).IsUnique();
        builder.Entity<Customer>().Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Entity<Customer>().Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Entity<Package>().HasIndex(x => new { x.CustomerId, x.BusinessId, x.ExpiresAtUtc });
        builder.Entity<TimetableSchedule>().HasIndex(x => new { x.BusinessId, x.StartTimeUtc });
        builder.Entity<TimetableSchedule>().Property(x => x.ClassName).HasMaxLength(150).IsRequired();
        builder.Entity<TimetableSchedule>().Property(x => x.InstructorName).HasMaxLength(150).IsRequired();
        builder.Entity<Booking>().HasIndex(x => new { x.TimetableScheduleId, x.Status });
        builder.Entity<Booking>().HasIndex(x => new { x.CustomerId, x.Status });
        builder.Entity<WaitlistEntry>().HasIndex(x => new { x.TimetableScheduleId, x.Status, x.JoinedAtUtc });
    }
}
