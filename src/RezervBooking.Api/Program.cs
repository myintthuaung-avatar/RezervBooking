using RezervBooking.Application;
using RezervBooking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddBookingInfrastructure(builder.Configuration);

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

var packages = app.MapGroup("/api/packages").WithTags("Packages");
packages.MapGet("", async (int customerId, IBookingService service, CancellationToken ct) => Results.Ok(await service.GetPackagesAsync(customerId, ct)))
    .WithName("GetPackages").Produces<IReadOnlyList<PackageDto>>();
packages.MapPost("/purchase", async (PurchasePackageRequest request, IBookingService service, CancellationToken ct) =>
    await ExecuteAsync(() => service.PurchasePackageAsync(request, ct), true)).WithName("PurchasePackage").Produces<PackageDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest);

app.MapGet("/api/timetable", async (int? businessId, DateOnly? date, IBookingService service, CancellationToken ct) =>
    Results.Ok(await service.GetSchedulesAsync(businessId, date, ct))).WithTags("Timetable").WithName("GetTimetable").Produces<IReadOnlyList<ScheduleDto>>();

var bookings = app.MapGroup("/api/bookings").WithTags("Bookings");
bookings.MapPost("", async (CreateBookingRequest request, IBookingService service, CancellationToken ct) =>
    await ExecuteAsync(() => service.BookAsync(request, ct), true)).WithName("BookClass").Produces<BookingResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest);
bookings.MapPost("/cancel", async (CancelBookingRequest request, IBookingService service, CancellationToken ct) =>
    await ExecuteAsync(() => service.CancelAsync(request, ct))).WithName("CancelBooking").Produces<CancelBookingResult>().ProducesProblem(StatusCodes.Status400BadRequest);

app.MapPost("/api/waitlist", async (JoinWaitlistRequest request, IBookingService service, CancellationToken ct) =>
    await ExecuteAsync(() => service.JoinWaitlistAsync(request, ct), true)).WithTags("Waitlist").WithName("JoinWaitlist").Produces<WaitlistResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest);

app.Run();

static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action, bool created = false)
{
    try { var result = await action(); return created ? Results.Created(string.Empty, result) : Results.Ok(result); }
    catch (DomainException exception) { return Results.BadRequest(new { error = exception.Message }); }
}

public partial class Program;
