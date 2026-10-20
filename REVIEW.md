# REVIEW.md — Self-review production readiness

## Status ringkas

Phase 1 selesai sesuai Definition of Done: create → approval/rejection → audit berjalan, business rules utama enforced di backend, database reproducible dari migration + seed, 28 automated test hijau, frontend minimal berfungsi end-to-end. Phase 2 **belum** diimplementasikan (sesuai instruksi — menunggu Change Request dari assessor).

## Findings

| # | Finding | Severity | Action | Status | Evidence |
| --- | --- | --- | --- | --- | --- |
| 1 | Authentication hanya simulasi (header `X-User-Email` dikirim dari `localStorage`, bisa diganti bebas lewat dropdown). | Info (by design) | Tidak diperbaiki — memang sesuai spesifikasi ("Authentication boleh disimulasikan"). Authorization tetap ditegakkan server-side di setiap endpoint. | Accepted | `Auth/CurrentUserAccessor.cs`, `Domain/ApprovalPolicy.cs` (CanDecide, IsRelatedParty); test di `AuthorizationAndEdgeCaseTests.cs` |
| 2 | SQLite write lock serial — test concurrency (`Task.WhenAll`) bisa lebih lemah/flaky dibanding RDBMS server sungguhan, terutama kalau dijalankan di CI yang lambat/paralel tinggi. | Medium | Diterima sebagai trade-off reproducibility (lihat PLAN.md trade-off #1). Pola `UPDATE ... WHERE Id AND Version AND Status` dengan transaksi minimal mengurangi risiko, tapi tidak menghilangkan. | Known limitation | `ConcurrencyTests.cs` — lolos di tiap run lokal selama pengembangan, tapi tidak dijamin 100% bebas flaky di semua environment. |
| 3 | Daftar aplikasi (`CRM`, `Finance Portal`) di-hardcode di dropdown frontend (`app.js`), bukan di-fetch dari endpoint `GET /api/applications` (endpoint ini tidak dibangun). | Low | Diterima — master data aplikasi sengaja fixed di Phase 1 (lihat brief 2.3), dan backend tetap validasi nama aplikasi terhadap tabel `Applications` (kalau tidak cocok → 422), jadi tidak ada risiko data invalid masuk. | Deferred | `wwwroot/app.js` (`APPLICATIONS` constant), `Endpoints/AccessRequestEndpoints.cs` (`CreateAsync` validasi `db.Applications`) |
| 4 | Tidak ada endpoint/UI untuk melihat daftar `Applications` beserta owner-nya secara eksplisit (hanya implisit lewat behavior approve). | Low | Tidak masuk scope wajib (bukan bagian "Scope Teknis yang Wajib Dibangun"). | Deferred | — |
| 5 | Logging masih level `ILogger` bawaan + correlation id di tiap request — tidak ada structured logging ke sink eksternal (Seq/ELK) atau dashboard observability. | Info (by design) | Sesuai brief: "Full observability stack tidak diperlukan." | Accepted | `Infrastructure/CorrelationIdMiddleware.cs`, `Infrastructure/GlobalExceptionHandler.cs` |
| 6 | Tidak ada rate limiting / anti-abuse di endpoint `POST /api/requests` — seseorang bisa spam create request dengan `ClientRequestId` yang selalu baru (bukan duplicate, jadi tidak kena guard idempotency). | Low | Di luar scope Phase 1 (bukan ancaman yang relevan untuk tool internal lokal dengan user terbatas). | Deferred | — |
| 7 | Tidak ada test otomatis untuk frontend (vanilla JS) — hanya diverifikasi manual (lihat AI_USAGE.md) karena tidak ada akses browser interaktif selama development oleh AI. | Medium | Frontend test eksplisit dicatat "bernilai tambah, tidak wajib" di brief, karena critical business behavior sudah dibuktikan lewat 28 backend/integration test. Kandidat disarankan verifikasi manual tambahan di browser sebelum submission final. | Deferred / perlu verifikasi manual kandidat | `tests/AccessRequestHub.Tests/` (tidak ada test FE) |
| 8 | `GET /api/users` bersifat publik tanpa autentikasi (sengaja, untuk user switcher demo) — membocorkan daftar email seluruh user ke siapa pun yang akses endpoint ini. | Low | Diterima, sesuai desain demo lokal; tidak ada data sensitif selain nama/email seed yang memang fiktif. | Accepted | `Endpoints/UserEndpoints.cs` |
| 9 | Tidak ada mekanisme reset/clear data selain hapus file `.db*` manual. | Info | Cukup untuk kebutuhan demo lokal; tidak diminta brief. | Accepted | README.md bagian "Run" |

## Known limitations (ringkas)

- SQLite (bukan server RDBMS) — reproducibility lebih baik, tapi concurrency test secara inheren lebih lemah.
- Authentication simulasi, bukan identity provider nyata — sesuai scope.
- Master data (`Applications`, `PolicyVersions`) fixed/hardcoded di seed, tidak ada admin UI untuk mengelolanya — sengaja tidak diminta (lihat brief 3.5).
- Belum ada browser-based UI test otomatis — hanya manual testing oleh kandidat.

## Deferred work (Phase 2 dan seterusnya)

- Apa pun yang berkaitan dengan Change Request Phase 2 — belum diimplementasikan, menunggu dokumen dari assessor sesuai instruksi di brief section 5.
- Observability/monitoring stack production-grade.
- Identity provider nyata (OAuth/OIDC), email/notifikasi nyata, microservices/distributed infra — sengaja tidak diminta (brief 3.5).
