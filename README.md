# Rezerv Booking Assessment

A .NET 8 studio-booking API using MySQL with Entity Framework Core, Redis locking, Swagger, and an hourly waitlist-expiry worker.

## Run

1. Start Redis with `docker compose up -d redis` (MySQL defaults to your local server at `localhost:3306`).
2. `dotnet run --project src/RezervBooking.Api`
3. Open the Swagger URL printed by ASP.NET Core (normally `http://localhost:5000/swagger`).

Schema and seed data are created on first start. Override `ConnectionStrings__MySql` and `ConnectionStrings__Redis` for a non-local environment. Run checks with `dotnet test`.

To create the schema manually, run [docs/create-schema.sql](docs/create-schema.sql) in MySQL Workbench, or run `mysql -u root -p < docs/create-schema.sql` from the repository root. Then start the API once to insert its sample seed data.

## Endpoints

| Method | Endpoint | Purpose |
| --- | --- | --- |
| GET | `/api/packages?customerId=1` | Customer packages |
| POST | `/api/packages/purchase` | Mock package purchase |
| GET | `/api/timetable?businessId=1&date=2026-09-22` | Schedules and availability |
| POST | `/api/bookings` | Confirm booking and deduct credit |
| POST | `/api/bookings/cancel` | Cancel, refund where eligible, promote waitlist |
| POST | `/api/waitlist` | Join a full schedule's FIFO waitlist |

Booking payload: `{ "customerId": 1, "scheduleId": 2 }`.

## Architecture and seed data

`Domain` contains entities; `Application` owns contracts and ports; `Infrastructure` implements EF persistence, the booking workflow, Redis locks, seeding and the background worker; `Api` provides minimal endpoints and Swagger.

The seed includes 10 customers, two businesses, 10 future schedules, an already-full schedule with a waiting customer, matching packages, and an expired package. [Schema documentation](docs/schema.md) contains the ERD and indexes.

## Assumptions

- The automatically selected package is the customer’s matching-business, non-expired package that expires first.
- Times are UTC. A cancellation refunds strictly more than four hours before start.
- Waitlisting does not reserve a credit. At promotion, the FIFO first entry is rechecked for a usable credit and conflicts; it is not silently skipped if ineligible.
- Started classes cannot be booked or waitlisted. Finished waitlist entries expire without any credit deduction.

## Concurrency and production trade-offs

Every booking, cancellation, waitlist join, and promotion takes a Redis `SET NX PX` lock per schedule. A token-checked Lua release prevents deleting a subsequent owner’s lock. The durable state change is also a serializable MySQL transaction, so MySQL remains the source of truth and overbooking is prevented even under contention.

The 15-second lease assumes a short request. Production should add lease renewal/Redlock, idempotency keys, EF migrations instead of `EnsureCreated`, an outbox for domain events, and a durable scheduler. If Redis is unavailable, mutations fail rather than risking capacity oversell. At scale, cache timetable reads with invalidation, partition by business, use read replicas, and monitor lock contention and transaction retries.
