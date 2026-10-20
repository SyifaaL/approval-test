# Access Request Hub

MVP single source of truth untuk permintaan akses aplikasi internal: create request → Manager approval → (kalau high-risk) System Owner approval → terminal (Approved/Rejected), dengan audit trail lengkap. Lihat [PLAN.md](PLAN.md) untuk arsitektur dan keputusan desain.

## Prasyarat

- [.NET SDK 10.0.402+](https://dotnet.microsoft.com/download) (dipin lewat `global.json`)
- Tidak perlu database eksternal — SQLite, file-based, dibuat otomatis.

## Setup

```powershell
# dari root repo
dotnet tool restore        # restore dotnet-ef (local tool manifest)
dotnet build AccessRequestHub.slnx
```

## Run

```powershell
cd src/AccessRequestHub.Api
dotnet run
```

Buka `http://localhost:5000` di browser. Database `accessrequesthub.db` (SQLite) otomatis dibuat, di-migrate, dan di-seed di folder yang sama saat startup — tidak perlu langkah manual.

Untuk mulai dari data bersih lagi, hapus `accessrequesthub.db`, `accessrequesthub.db-wal`, `accessrequesthub.db-shm` sebelum `dotnet run`.

## Migration & seed manual (opsional)

Migration dan seed sudah otomatis jalan di startup (`Program.cs`). Kalau mau generate migration baru secara manual:

```powershell
dotnet ef migrations add <Name> --project src/AccessRequestHub.Api --startup-project src/AccessRequestHub.Api -o Data/Migrations
```

## Test

```powershell
dotnet test AccessRequestHub.slnx
```

28 test (xUnit + `WebApplicationFactory<Program>`, satu file SQLite temp per test class) mencakup 7 demo scenario wajib, authorization failure, idempotency, optimistic concurrency, dan trigger append-only audit. Lihat [AccessRequestHub.slnx](AccessRequestHub.slnx) → `tests/AccessRequestHub.Tests/`.

## Demo users

Pilih lewat dropdown user switcher di kanan atas (simulasi login, bukan security boundary — lihat [REVIEW.md](REVIEW.md)).

| Email | Role |
| --- | --- |
| alice@example.local | Requester, melapor ke Bob |
| bob@example.local | Manager Alice |
| carol@example.local | System Owner — CRM |
| dana@example.local | System Owner — Finance Portal |
| erin@example.local | Admin/Auditor (lihat semua request, bukan approver) |

## Demo flow

1. **Standard**: login sebagai Alice → New Request → CRM, NonProduction, Read → submit. Login sebagai Bob → Approval Inbox → approve → status `Approved`.
2. **Production (high-risk)**: Alice → CRM, Production, Read → submit. Bob approve → status `PendingSystemOwner`. Login Carol → Approval Inbox → approve → `Approved`.
3. **Admin (high-risk)**: Alice → Finance Portal, NonProduction, Admin → submit. Bob approve → menunggu Dana.
4. **Unauthorized**: coba approve request CRM yang masih `PendingManager` sebagai Carol → ditolak backend (403).
5. **Reject**: approver reject dengan reason wajib diisi → status `Rejected`, reason + audit event tersimpan, request tidak bisa diproses lagi (409 kalau dicoba lagi).
6. **Audit timeline**: buka detail request manapun untuk lihat riwayat lengkap (Created → Approved/Rejected, siapa, kapan).

## Struktur proyek

```
src/AccessRequestHub.Api/   ASP.NET Core Minimal API + UI statis (wwwroot/)
  Endpoints/                HTTP: parse input, panggil domain, map response
  Domain/                   ApprovalPolicy, status transition, CanDecide, DomainException
  Data/                     AppDbContext, entities, Migrations, Seeder
  Auth/                     CurrentUserAccessor (header X-User-Email)
  Infrastructure/           GlobalExceptionHandler (ProblemDetails), CorrelationIdMiddleware
  wwwroot/                  index.html + app.js (vanilla JS, no build step)
tests/AccessRequestHub.Tests/   xUnit + WebApplicationFactory<Program>
```

## Evidence lain

- [PLAN.md](PLAN.md) — problem understanding, data model, keputusan desain, trade-off.
- [AI_USAGE.md](AI_USAGE.md) — interaksi AI yang paling berpengaruh.
- [REVIEW.md](REVIEW.md) — self-review production readiness, known limitations.
- [INTEGRITY.md](INTEGRITY.md) — deklarasi ownership.
