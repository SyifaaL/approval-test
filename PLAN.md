# PLAN.md — Access Request Hub (Phase 1)

Dibuat: 2026-10-20

## 1. Problem understanding

MVP single source of truth untuk permintaan akses aplikasi internal: create request -> Manager approval -> (kalau high-risk) System Owner approval -> terminal (Approved/Rejected), dengan audit trail lengkap. Fokus penilaian: correctness bisnis rule, idempotency, optimistic concurrency, audit atomic, authorization server-side — bukan jumlah fitur UI.

High-risk = `Environment == Production` ATAU `AccessLevel == Admin`. Aplikasi (CRM/Finance Portal) hanya menentukan siapa System Owner-nya, tidak memengaruhi high-risk.

## 2. Keputusan desain untuk 6 pertanyaan terbuka

Soal tidak menjawab enam hal ini. Keputusan final dan alasannya:

| # | Pertanyaan | Keputusan | Alasan |
| --- | --- | --- | --- |
| 1 | Carol/Dana request untuk aplikasi miliknya sendiri | **Ditolak saat create** (422/400 validasi) | Mencegah situasi "tidak ada yang bisa approve" tanpa perlu delegation flow yang di luar scope. |
| 2 | Bob/Carol/Dana/Erin tidak punya manager di seed — siapa yang approve kalau mereka create request? | **Hanya user yang punya `ManagerId` non-null yang boleh create request** (ditolak di backend kalau tidak) | Sederhana, eksplisit, tidak perlu menambah manager dummy yang tidak ada di spesifikasi demo user. |
| 3 | User bukan approver memanggil approve/reject: 403 atau 404? | **404 untuk user yang sama sekali tidak terkait request** (requester lain, bukan manager/owner-nya); **403 untuk user yang terkait tapi bukan approver tahap ini** (termasuk requester sendiri, dan Erin) | Mencegah ID enumeration (404 untuk pihak asing) sekaligus memberi sinyal jelas ke pihak yang memang terkait kenapa ditolak. |
| 4 | `ClientRequestId` sama, payload beda | **409 `idempotency_mismatch`**; kalau payload identik, kembalikan row yang sudah ada (200/201 idempotent) | Mencegah silent data corruption kalau client retry dengan payload yang salah. |
| 5 | Urutan pengecekan error approve/reject | 401 (header tidak ada/tidak dikenal) -> 404 (request tidak ada / actor tidak terkait sama sekali) -> 409 `invalid_state` (sudah terminal) -> 403 (terkait tapi bukan approver tahap ini / self-approve) -> 422 (reason kosong saat reject / payload invalid) -> 409 `version_conflict` (expectedVersion tidak cocok) | Urutan ini memastikan informasi paling "aman" dibocorkan duluan (eksistensi dulu, baru detail state), dan version conflict dicek terakhir karena harus jadi database statement pertama di transaksi. |
| 6 | Siapa boleh GET detail request | **Requester, manager langsungnya, System Owner aplikasi terkait, dan Erin (auditor)** | Selaras dengan siapa yang punya kepentingan sah melihat data tersebut; mencegah IDOR dari user yang tidak terkait. |

## 3. Arsitektur & data model ringkas

Monolith kecil, 2 project: `src/AccessRequestHub.Api` (ASP.NET Core Minimal API, menyajikan UI statis dari `wwwroot`) dan `tests/AccessRequestHub.Tests` (xUnit + `WebApplicationFactory<Program>`). SQLite sebagai database.

```
src/AccessRequestHub.Api
├─ Endpoints/        HTTP: parse input, panggil domain, map error
├─ Domain/           ApprovalPolicy, status transition, CanDecide, enum, DomainException
├─ Data/             AppDbContext, entities, Migrations, Seeder
├─ Auth/             HeaderAuthHandler (X-User-Email)
├─ Infrastructure/   GlobalExceptionHandler (ProblemDetails), CorrelationIdMiddleware
└─ wwwroot/          index.html + app.js (vanilla JS, no build step)
```

Tabel: `Users`, `Applications`, `PolicyVersions`, `AccessRequests`, `AuditEvents`. Detail constraint mengikuti rancangan di access-request-hub-playbook.md section "Rancangan sistem (1/2)": unique `(RequesterId, ClientRequestId)`, FK, CHECK untuk enum status, `Version` int untuk optimistic concurrency, trigger append-only di `AuditEvents`.

Status tidak disimpan sebagai tabel role — relasional: Manager = `requester.ManagerId`, System Owner = `application.OwnerUserId`, Auditor = flag `IsAuditor` pada Erin.

## 4. Implementation order

1. Scaffold solution (.sln/.slnx, 2 project, EF Core + Sqlite, dotnet-ef tool manifest) — **selesai**.
2. PLAN.md — **selesai, dokumen ini**.
3. Schema, migration, seed (Users, Applications, PolicyVersions, AccessRequests, AuditEvents + trigger append-only).
4. Backend core: auth handler header-based, domain (`ApprovalPolicy`, `CanDecide`), create idempotent, list/detail/inbox dengan read-authorization, approve/reject dengan optimistic concurrency, `ProblemDetails` global exception handler, logging dasar + correlation id.
5. Automated tests: 7 demo scenario wajib dari soal + edge case (self-approve, Erin approve, terminal state, reject tanpa reason, wrong owner, IDOR, idempotency mismatch, audit trigger reject update/delete) + concurrency test `Task.WhenAll`.
6. Frontend minimal: user switcher, create form, My Requests + detail/audit timeline, Approval Inbox, state loading/empty/error/conflict.
7. Dokumentasi evidence: README, AI_USAGE.md, REVIEW.md, INTEGRITY.md.

## 5. Test strategy

xUnit + `WebApplicationFactory<Program>`, SQLite file temp per test class (bukan in-memory connection yang hilang saat ditutup), migrate + seed di constructor, helper `client.As("bob@example.local")` untuk set header `X-User-Email`. Satu test per demo scenario (7), satu per 4 kombinasi high-risk matrix, satu untuk tiap edge case di atas, dan dua test concurrency (`Task.WhenAll` untuk stale approval, dan untuk duplicate create).

## 6. Trade-off

1. **SQLite vs PostgreSQL** — pilih SQLite untuk reproducibility tanpa dependency eksternal (`dotnet run` langsung jalan di machine assessor manapun). Trade-off: write lock SQLite serial, jadi test concurrency/race lebih lemah dibanding RDBMS server — dicatat sebagai known limitation di REVIEW.md.
2. **`ExecuteUpdateAsync` + version eksplisit vs EF concurrency token (`[ConcurrencyCheck]`/rowversion)** — pilih `UPDATE ... WHERE Id AND Version AND Status` eksplisit karena race condition-nya terlihat jelas di SQL dan lebih mudah dijelaskan saat walkthrough, dibanding behavior implisit EF concurrency token.
3. **Snapshot `IsHighRisk` saat create vs hitung ulang tiap kali dibaca** — pilih snapshot + simpan `PolicyVersion` di row, supaya request lama tetap konsisten kalau policy berubah di Phase 2. Trade-off: satu kolom tambahan dan risiko snapshot stale kalau ada bug di `ApprovalPolicy` saat create (dimitigasi dengan test matrix).

## 7. Perubahan plan selama implementasi

- **Frontend sempat dipertimbangkan pindah ke React + Redux Toolkit**, lalu dibatalkan dan kembali ke rencana awal (vanilla JS, no build step di section 3). Alasan: scope assessment kecil, dinilai dari correctness backend bukan kompleksitas frontend; build step tambahan tidak menambah nilai penilaian. Lihat AI_USAGE.md interaksi #1.
- **Sempat dipertimbangkan migrasi SQLite → PostgreSQL**, dibatalkan setelah klarifikasi — kebutuhan sebenarnya cuma tooling untuk melihat data (disediakan via DB Browser for SQLite), bukan masalah dengan SQLite itu sendiri. Keputusan trade-off #1 di section 6 tetap berlaku. Lihat AI_USAGE.md interaksi #2.
- Tidak ada perubahan pada data model, domain logic, atau urutan error handling dari rencana awal — keenam keputusan di section 2 dan arsitektur di section 3 diimplementasikan persis seperti direncanakan, divalidasi lewat 28 automated test di Fase E.
