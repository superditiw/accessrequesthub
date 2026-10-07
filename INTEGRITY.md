# Data Integrity

Ketahanan dan keutuhan data (Data Integrity) adalah prioritas utama aplikasi `Access Request Hub`.

## 1. Concurrency Control (Optimistic Concurrency)
Aplikasi memastikan bahwa jika dua *Approver* berusaha menyetujui pengajuan yang sama pada milidetik yang sama, salah satunya akan ditolak.
- **Implementasi**: Kolom `version` pada tabel `access_requests` difungsikan sebagai `[ConcurrencyCheck]`.
- **Perilaku**: Jika data telah termutakhirkan, EF Core melempar `DbUpdateConcurrencyException`. Sistem akan mengubah eksepsi ini menjadi *409 Conflict*. 

## 2. Immutable Audit Trail (Append-Only)
Jejak audit tidak boleh dipalsukan, diedit, atau dihapus meskipun oleh seorang Administrator di level koneksi database.
- **Implementasi**: Database dikonfigurasi melalui EF Migrations untuk menggunakan *Trigger* bawaan SQLite.
- **Script**:
  ```sql
  CREATE TRIGGER trg_audit_events_no_update BEFORE UPDATE ON audit_events
  BEGIN SELECT RAISE(ABORT, 'Updates are not allowed on audit_events.'); END;
  
  CREATE TRIGGER trg_audit_events_no_delete BEFORE DELETE ON audit_events
  BEGIN SELECT RAISE(ABORT, 'Deletes are not allowed on audit_events.'); END;
  ```

## 3. Idempotency Creation
Jika klien gagal menerima respon *success* dan mencoba memanggil ulang operasi *Create* dengan data yang sama persis:
- **Implementasi**: Terdapat batasan `UNIQUE INDEX` pada pasangan `requester_id` dan `client_request_id` (sebuah UUID statis buatan klien).
- **Perilaku**: Database akan menolak duplikasi. Service akan membaca duplikat dan jika *payload* selaras, aplikasi akan merespons 200/Sukses seakan-akan operasi berjalan kembali (padahal mengembalikan baris yang sudah ada).

## 4. Otorisasi Ketat via Middleware & Service Layer
- Identitas didapatkan mutlak dari *Interceptor Header* (`X-Demo-User`). 
- Pengguna dilarang menyetujui atau menolak *Request* miliknya sendiri (Self-Approval = *403 Forbidden*).
- Setiap operasi (Read / Approve / Reject) melewati evaluasi hierarki manajer dan kepemilikan sistem aplikasi.
