# Review Implementation

Dokumen ini berisi tinjauan implementasi MVP `Access Request Hub` berdasarkan dokumen spesifikasi awal (`PLAN.md`).

## 1. Domain & Entity
- ✅ Entity `User`, `Application`, `PolicyVersion`, `AccessRequest`, dan `AuditEvent` telah dibuat menggunakan atribut mapping Fluent API di `AppDbContext`.
- ✅ Kolom tabel dikonversi menjadi penamaan `snake_case` melalui sebuah _loop_ otomatis di `OnModelCreating`, tanpa *package* tambahan.
- ✅ Tabel `audit_events` diikat dengan batasan *trigger* SQLite (Append-only) yang ditambahkan langsung lewat EF Core Migrations.

## 2. API & Service Layer (Logika Inti)
- ✅ `DemoAuthMiddleware` / `CurrentUserService` berjalan dengan baik untuk mensimulasikan login pengguna.
- ✅ Format balasan *Error* telah mengadopsi standar global (400, 401, 403, 404, 409).
- ✅ Endpoint/Service Idempotent: `client_request_id` digunakan bersama `requester_id` (Unique Constraint di Database) agar *Double-Submit* tidak menghasilkan dua baris request.
- ✅ *State Machine* tertangani dengan bersih. Dari status `PendingManager` ke `Approved` (jika Non-High-Risk) atau menuju `PendingSystemOwner` (Jika High-Risk).
- ✅ *Self-Approval / Self-Rejection* ditolak (HTTP 403).
- ✅ Persyaratan alasan (reason) saat *Reject* wajib (HTTP 400).

## 3. UI Blazor
- ✅ Komponen navigasi (`NavMenu.razor`) dapat membaca hak (Capabilities) profil setiap kali user berganti sesi, lalu menyembunyikan Tab yang tidak relevan (seperti `My Requests` disembunyikan untuk System Owner, dan tab `Create Request` disembunyikan jika tidak memiliki atasan).
- ✅ Interaktivitas secara dinamis (Tombol Approve / Reject dengan validasi *concurrency* bawaan yang melakukan refresh jika data *stale*).

## Kesimpulan
Keseluruhan fasa 1 (*MVP*) telah sepenuhnya terpenuhi dan lolos 10 *Integration Test*. Konsep arsitektur yang meletakkan seluruh validasi dan pengamanan otorisasi pada *Service Layer* terbukti solid karena UI dan API bisa mengonsumsi logika yang sama secara aman.
