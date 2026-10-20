# AI Usage

Dikerjakan dengan Claude Code (Claude Sonnet 5) sebagai coding agent, diarahkan dan direview langkah demi langkah oleh kandidat — setiap fase di-build terpisah, di-test manual lewat HTTP call sebelum dianggap selesai, dan commit ke git dilakukan sendiri oleh kandidat (bukan oleh AI) setelah review.

## Interaksi paling berpengaruh

### 1. Ask: "gunakan rtk ya" (pakai Redux Toolkit untuk frontend)
**Suggestion**: AI langsung mengasumsikan frontend React + Redux Toolkit (dengan pilihan RTK Query vs createSlice manual, dan skema build Vite → `wwwroot`), lalu mengajukan clarifying question soal detail setup-nya.
**Accepted/changed**: Rejected sebagian — setelah melihat implikasi build step tambahan, kandidat menjawab "gausah pake react... FE yang simple aja, cuma soal test". AI kembali ke rencana awal di PLAN.md: vanilla JS, no build step, satu `dotnet run`.
**Why**: Scope assessment ini kecil dan dinilai dari correctness backend, bukan kompleksitas frontend. Build step tambahan menambah waktu setup tanpa menambah nilai penilaian.

### 2. Ask: "lu migrate ke postgresql?"
**Suggestion**: AI mengklarifikasi dulu lewat pertanyaan terstruktur (trade-off reproducibility SQLite vs Postgres, sesuai PLAN.md trade-off #1) sebelum mengubah apa pun.
**Accepted/changed**: Ternyata kebutuhan sebenarnya cuma "gimana cara saya lihat datanya" (mencari tool setara pgAdmin/HeidiSQL) — bukan migrasi database. AI tetap pakai SQLite, cuma merekomendasikan DB Browser for SQLite.
**Why**: Menunjukkan pentingnya klarifikasi sebelum eksekusi perubahan arsitektur besar — permintaan yang terdengar seperti keputusan teknis besar kadang sebenarnya kebutuhan tooling kecil.

### 3. Bug ditemukan sendiri: file `.db` kelihatan kosong di viewer
Saat kandidat pertama kali buka `accessrequesthub.db` di viewer eksternal (bukan DB Browser), tabelnya kosong padahal API sudah jalan dan seed sukses. AI mendiagnosis bahwa SQLite jalan di WAL mode — data masih di file `-wal`, belum ter-checkpoint ke file utama, sehingga tool yang cuma baca file tunggal tidak melihatnya. AI menambahkan `PRAGMA wal_checkpoint(TRUNCATE)` **sementara** di `Program.cs`, menjalankan app sekali untuk checkpoint, lalu me-revert kode itu lagi (tidak ikut ke dalam commit).
**Why**: Perbaikan tooling/dev-experience tidak boleh membocorkan kode dev-only ke source yang di-commit.

### 4. Ask: urutan error check approve/reject
**Suggestion**: AI mengikuti persis urutan yang sudah diputuskan di PLAN.md (keputusan #5): 401 → 404 (unrelated, cegah enumeration) → 409 `invalid_state` → 403 → 422 → 409 `version_conflict`, diimplementasikan sebagai satu fungsi `DecideAsync` di belakang endpoint approve dan reject.
**Accepted**: Diterima tanpa revisi, lalu diverifikasi lewat 11 skenario manual (termasuk concurrency `Task.WhenAll`) sebelum ditulis jadi automated test di Fase E.
**Why**: Urutan ini sengaja membocorkan informasi paling "aman" duluan (eksistensi vs detail state) untuk mencegah IDOR, sesuai keputusan desain yang sudah didokumentasikan di PLAN.md sebelum coding dimulai.

## Three things AI got wrong

1. **Kill paksa proses `dotnet run` (`Stop-Process -Force`) meninggalkan data di WAL, bukan di file utama.** Ini bikin file `.db` yang diserahkan ke kandidat pertama kali kelihatan kosong di viewer yang naif terhadap WAL — seharusnya dari awal AI mempertimbangkan shutdown yang lebih bersih atau langsung checkpoint, bukan setelah ditegur.
2. **Mencoba load assembly .NET modern (`Microsoft.Data.Sqlite.dll`) lewat `Add-Type` di Windows PowerShell 5.1** untuk menjalankan checkpoint — gagal karena PowerShell 5.1 (.NET Framework) tidak kompatibel dengan assembly .NET modern. Percobaan ini membuang satu putaran sebelum pindah ke pendekatan yang benar (jalankan lewat `dotnet run` sendiri).
3. **Asumsi awal scope frontend terlalu besar** saat merespons "gunakan rtk ya" — AI langsung menyiapkan pertanyaan seputar React + RTK Query + Vite build tanpa mempertimbangkan bahwa instruksi singkat itu mungkin salah tafsir dari kandidat sendiri (ternyata memang ingin tetap simple). Klarifikasi lebih awal dengan opsi "tetap vanilla JS" sebagai pilihan pertama akan menghemat satu putaran diskusi.
