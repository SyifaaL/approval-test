# INTEGRITY.md

> **Catatan sebelum submit**: isi di bawah ini disusun berdasarkan proses kerja yang sebenarnya terjadi selama sesi ini (lihat [AI_USAGE.md](AI_USAGE.md) untuk detail interaksi). Review ulang tiap baris dan pastikan kamu benar-benar bisa menjelaskan setiap file sebelum menandatangani deklarasi ini — termasuk kenapa tiap keputusan desain diambil, bukan sekadar bahwa AI yang menulis kodenya.

## Deklarasi

I confirm that this submission represents my own engineering work.
AI-assisted tools were used as documented in AI_USAGE.md.
I reviewed the submitted code and can explain its architecture, behavior,
known limitations, security implications, and trade-offs.

**Date:** 2026-10-20

**Primary AI tools used:** Claude Code (Claude Sonnet 5), dipakai sebagai coding agent untuk seluruh implementasi (schema/migration, domain logic, endpoint, test, frontend, dokumentasi) — diarahkan fase per fase, setiap fase diverifikasi lewat manual HTTP test dan/atau automated test sebelum lanjut, dan di-commit ke git oleh kandidat sendiri (bukan oleh AI) setelah review.

**External human assistance:** None.

**Starter/template code used:** Tidak ada starter project eksternal. Scaffold awal (struktur solution, `global.json`, `dotnet-tools.json`, `.gitignore`) dan arsitektur mengikuti rencana yang sudah ditulis sebelumnya di `access-request-hub-playbook.md` (dokumen perencanaan internal kandidat sendiri, bukan template pihak ketiga).

**External repositories/snippets copied or adapted:** None. Seluruh kode (entity, `AppDbContext`, `ApprovalPolicy`, endpoint, middleware, test, frontend) ditulis baru untuk assessment ini, tidak ada kode yang disalin dari repository/snippet eksternal.
