# Access Request Hub

Access Request Hub adalah MVP (Minimum Viable Product) untuk manajemen pengajuan hak akses aplikasi internal. Aplikasi ini bertujuan menggantikan proses manual (via email/chat) menjadi satu sistem tersentralisasi dengan jejak audit (*audit trail*) yang kuat.

## Fitur Utama
1. **Hierarki Approval Otomatis**: 
   - Semua *request* membutuhkan *approval* dari manajer requester.
   - Jika *request* bersifat **High-Risk** (misalnya environment `Production` atau access level `Admin`), maka *request* juga harus disetujui oleh *System Owner* dari aplikasi yang dituju.
2. **Current-User Resolver**:
   - UI dilengkapi dengan *User Switcher* terintegrasi.
   - UI akan menyesuaikan tampilan (Tab dan Tombol Aksi) secara reaktif berdasarkan role pengguna, kepemilikan aplikasi, dan ada tidaknya atasan (manajer).
3. **Integritas Data Terjamin**:
   - **Idempotency**: Mencegah duplikasi submit dari *client*.
   - **Concurrency Control**: Menggunakan token versi (`Version`) untuk menghindari *stale-data override* jika dua atasan menekan *Approve*/*Reject* di waktu yang bersamaan.
   - **Audit Events (Append-Only)**: Di level database (SQLite), terdapat trigger yang mengunci tabel log agar tidak bisa di-*Update* atau di-*Delete*.
4. **Validasi Kewajiban Alasan Penolakan**:
   - *Reject action* akan gagal jika *reason* tidak dilampirkan.

## Teknologi
- **Runtime**: .NET (C#)
- **UI Framework**: Blazor Server (InteractiveServer)
- **Database**: Entity Framework Core dengan Provider SQLite

## Cara Menjalankan Aplikasi
1. Pastikan .NET SDK telah terpasang.
2. Buka terminal di direktori root aplikasi.
3. Jalankan perintah:
   ```bash
   dotnet watch
   ```
4. Aplikasi akan secara otomatis berjalan (biasanya di `https://localhost:xxxx`).
5. Database `app.db` akan dibuat secara otomatis saat pertama kali di-build/dijalankan dan akan langsung di-*seed* dengan data pengguna contoh (Alice, Bob, Carol, Dana, Erin).

## Pengujian (Testing)
Proyek ini dilengkapi dengan 10 skenario *Integration Test* end-to-end yang menggunakan `WebApplicationFactory`.
Untuk menjalankan pengujian:
```bash
dotnet test
```
