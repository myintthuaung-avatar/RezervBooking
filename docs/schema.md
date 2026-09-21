# Database schema

```mermaid
erDiagram
  BUSINESS ||--o{ TIMETABLE_SCHEDULE : owns
  BUSINESS ||--o{ PACKAGE : scopes
  CUSTOMER ||--o{ PACKAGE : owns
  CUSTOMER ||--o{ BOOKING : makes
  CUSTOMER ||--o{ WAITLIST_ENTRY : joins
  TIMETABLE_SCHEDULE ||--o{ BOOKING : has
  TIMETABLE_SCHEDULE ||--o{ WAITLIST_ENTRY : has
  PACKAGE ||--o{ BOOKING : funds
```

| Table | Key columns | Purpose |
| --- | --- | --- |
| Businesses | Id, Name | Tenant boundary |
| Customers | Id, Name, Email | Email is unique |
| Packages | CustomerId, BusinessId, TotalCredits, RemainingCredits, ExpiresAtUtc | Credit source |
| Schedules | BusinessId, StartTimeUtc, EndTimeUtc, Capacity | Class capacity |
| Bookings | CustomerId, TimetableScheduleId, PackageId, Status | Audit history, including cancellations |
| WaitlistEntries | CustomerId, TimetableScheduleId, Status, JoinedAtUtc | FIFO queue |

Indexes cover package eligibility, business/schedule time, active bookings, and waitlist FIFO ordering. Schedules and bookings also have EF row-version columns.
