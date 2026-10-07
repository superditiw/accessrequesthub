## 1. Problem understanding

Permintaan akses aplikasi internal saat ini lewat email/chat sehingga sulit dilacak, bisa terkirim dua kali, dan dua approver bisa memproses request yang sama bersamaan. Saya membangun MVP Access Request Hub sebagai satu sumber kebenaran untuk request, approval, dan audit trail.

Inti yang harus benar:
- Semua request lewat Manager approval dulu; high-risk (Production ATAU Admin) lanjut ke System Owner.
- Approved/Rejected adalah terminal.
- Create idempotent; approve/reject aman dari stale/concurrent action.
- Audit event atomic dengan perubahan state, dan append-only.
- Authorization ditegakkan di backend, bukan di UI.

## 2. Stack

| Komponen | Pilihan |
|---|---|
| Runtime | .NET (LTS terbaru yang terpasang) |
| UI | Blazor Server |
| API | ASP.NET Core Minimal API di proyek yang sama |
| ORM | EF Core + SQLite, **Fluent API**, EF Migrations |
| Test | xUnit + `WebApplicationFactory` |

Database = satu file SQLite (`app.db`), dibuat otomatis saat startup (`Database.Migrate()` lalu seeder). Tidak ada instalasi database; `app.db` masuk `.gitignore`. Nama kolom snake_case lewat loop di `OnModelCreating` (tanpa package tambahan).

## 3. Arsitektur

```
Blazor components ----\
                       >--> AccessRequestService (aturan bisnis + authorization)
Minimal API endpoints -/              |
                                      v
                              EF Core / SQLite
```

**Semua aturan bisnis dan authorization ada di service layer.** Blazor dan API memanggil service yang sama, jadi aturan tidak bisa terlewat lewat salah satu jalur.

Simulasi login:
- API: header `X-Demo-User: <email>`, divalidasi terhadap tabel `users`. Tidak ada/tidak dikenal -> 401.
- Blazor: user switcher menyimpan current user di scoped state.
- Requester selalu diambil dari current user, tidak pernah dari payload.

## 4. Model peran (keputusan)

Kolom `users.role` hanya `Employee` atau `Auditor`. **Peran lain diturunkan dari relasi data**, supaya tidak ada dua sumber kebenaran:

| Peran | Cara menentukan |
|---|---|
| Requester | Employee yang membuat request (current user) |
| Manager | Ada user lain dengan `manager_id` = id dia |
| System Owner | Ada baris `applications.system_owner_id` = id dia |
| Auditor | `users.role = 'Auditor'` (hanya baca, bukan approver) |

Pengecekan hak per request:
- Tahap `PendingManager`: `request.requester.manager_id == actor.id`.
- Tahap `PendingSystemOwner`: `request.application.system_owner_id == actor.id`.
- Tolak jika `actor.id == request.requester_id`, apa pun perannya.
- Auditor tidak pernah boleh approve.

**Create hanya untuk user yang punya `manager_id`.** Jika tidak punya -> 400 ("requester has no manager"). Alasan: semua request wajib lewat Manager approval, dan hierarki manager lengkap di luar scope Phase 1. Dengan data seed, hanya Alice yang bisa membuat request (lihat known limitation).

## 5. Data model

Nama tabel/kolom snake_case.

**users**: `id`, `email` (unique), `display_name`, `role` (Employee|Auditor), `manager_id` (FK users, nullable)
- CHECK role valid; CHECK `manager_id IS NULL OR manager_id <> id`

**applications**: `id`, `name` (unique), `system_owner_id` (FK users)

**policy_versions**: `code` (PK, mis. `v1`), `effective_from`

**access_requests**
- `id` (PK, Guid), `client_request_id` (Guid, **bukan FK**, ID idempotency dari client)
- `requester_id` (FK users), `application_id` (FK applications)
- `environment`, `access_level`, `justification`, `is_high_risk`, `status`
- `policy_version` (FK policy_versions.code), `version` (int, concurrency token), `created_at`, `updated_at`
- **UNIQUE (`requester_id`, `client_request_id`)** -> idempotency di level DB
- CHECK: nilai valid untuk status/environment/access_level, `length(trim(justification)) > 0`, `version >= 1`
- CHECK: `is_high_risk = (environment='Production' OR access_level='Admin')`
- CHECK: `is_high_risk = 1 OR status <> 'PendingSystemOwner'`

**audit_events** (append-only)
- `id`, `access_request_id` (FK), `actor_user_id` (FK), `event_type`, `from_status`, `to_status`, `reason`, `version_after`, `created_at`
- CHECK: event reject (`ManagerRejected`/`OwnerRejected`) wajib punya `reason`
- Trigger SQLite menolak UPDATE dan DELETE (ditulis di migration)

Relasi:

| Kolom | Mengarah ke |
|---|---|
| `users.manager_id` | `users.id` |
| `applications.system_owner_id` | `users.id` |
| `access_requests.requester_id` | `users.id` |
| `access_requests.application_id` | `applications.id` |
| `access_requests.policy_version` | `policy_versions.code` |
| `audit_events.access_request_id` | `access_requests.id` |
| `audit_events.actor_user_id` | `users.id` |

Semua FK memakai `ON DELETE RESTRICT`.

## 6. State machine

```
PendingManager --(manager approve, non-high-risk)--> Approved
PendingManager --(manager approve, high-risk)------> PendingSystemOwner
PendingManager --(manager reject + reason)---------> Rejected
PendingSystemOwner --(owner approve)---------------> Approved
PendingSystemOwner --(owner reject + reason)-------> Rejected
Approved / Rejected --> terminal (semua transition ditolak dengan 409)
```

## 7. Desain idempotency dan concurrency

**Idempotency (create)**
1. Cari request dengan (`requester_id`, `client_request_id`); jika ada, kembalikan yang sama.
2. Jika belum ada, insert request + audit event `Created` dalam satu `SaveChanges` (satu transaksi).
3. Jika dua request bersamaan, unique index menolak yang kedua; service menangkap pelanggaran unique, lalu membaca dan mengembalikan row yang sudah ada.
4. `client_request_id` sama tetapi payload berbeda -> 409 conflict

**Concurrency (approve/reject)**
- Client mengirim `expectedVersion`.
- `access_requests.version` dipasang `IsConcurrencyToken()`; EF menambahkan `WHERE version = <nilai awal>` pada UPDATE.
- Alur: muat request; jika `request.version != expectedVersion` -> 409; ubah status, `version + 1`, tambah audit event, lalu `SaveChanges` sekali (satu transaksi). Jika baris lain sudah mengubahnya di antara muat dan simpan, EF melempar `DbUpdateConcurrencyException` -> 409.
- Karena version naik di setiap transition, dua action pada version yang sama hanya satu yang berhasil.
- SQLite tidak punya `rowversion`, jadi `version` adalah integer yang dinaikkan manual.

## 8. Kontrak error

| Kondisi | HTTP |
|---|---|
| Validasi gagal (justification kosong, reject tanpa reason, enum salah, requester tanpa manager) | 400 |
| User tidak ada/tidak dikenal | 401 |
| Dikenal tapi tidak berhak | 403 |
| Request tidak ditemukan | 404 |
| Version stale, request sudah terminal, payload idempotency beda | 409 |

Body error konsisten (ProblemDetails dengan `code` yang jelas).

## 9. Endpoint

| Method | Path | Fungsi |
|---|---|---|
| GET | `/api/me` | Profil + kemampuan user (untuk menentukan tab) |
| POST | `/api/requests` | Create (idempotent) |
| GET | `/api/requests/mine` | My Requests |
| GET | `/api/requests/{id}` | Detail + audit timeline |
| GET | `/api/requests` | Semua request (khusus Auditor) |
| GET | `/api/inbox` | Approval inbox |
| POST | `/api/requests/{id}/approve` | Approve (`expectedVersion`) |
| POST | `/api/requests/{id}/reject` | Reject (`expectedVersion`, `reason`) |

`/api/me` mengembalikan: `id`, `email`, `displayName`, `role`, `canCreate` (punya manager), `isManager`, `ownedApplications`, `canViewAll` (Auditor).

Inbox:
- Manager: `status = PendingManager` dan `requester.manager_id = actor.id`.
- System Owner: `status = PendingSystemOwner` dan `application.system_owner_id = actor.id`.
- Jika satu user keduanya, isinya digabung.

Detail request boleh dilihat oleh: requester, manager requester, system owner aplikasinya, dan Auditor.

## 10. UI: tab per user

Tab ditentukan dari `/api/me`. Ini hanya kenyamanan tampilan; **penegakan hak tetap di backend**.

| User | Create | My Requests | Approval Inbox | All Requests |
|---|---|---|---|---|
| Alice | ✓ | ✓ | - | - |
| Bob | - | ✓ | ✓ (tahap Manager) | - |
| Carol | - | ✓ | ✓ (tahap Owner, CRM) | - |
| Dana | - | ✓ | ✓ (tahap Owner, Finance Portal) | - |
| Erin | - | - | - | ✓ (semua request + audit) |

Semua halaman memiliki state loading, empty, error, dan conflict (409) yang jelas.

## 11. Urutan implementasi

Satu item = satu sesi chat = satu commit.

- [x] 0. Template Blazor Server, `.gitignore`, commit, tag `assessment-start`
- [x] 0b. Commit PLAN.md (dan file aturan untuk agent)
- [x] 1. Entity, DbContext (Fluent), migration + trigger append-only, seeder
- [x] 2. Current-user resolver (header), `/api/me`, format error global
- [x] 3. Create request + validasi + idempotency
- [ ] 4. My Requests, detail + audit timeline, inbox, All Requests (Auditor)
- [ ] 5. Approve (manager, system owner) + version check + audit atomic
- [ ] 6. Reject + reason + audit
- [ ] 7. Integration tests (bagian 12)
- [ ] 8. Blazor UI: user switcher, tab per user, create form, list/detail, inbox
- [ ] 9. README, REVIEW.md, AI_USAGE.md, INTEGRITY.md
- [ ] 10. Jalankan semua test dari database baru, tag `phase-1-complete`

Jika waktu mendekati 8 jam: berhenti di state stabil, prioritaskan item 1-7 (backend benar dan teruji) di atas kelengkapan UI, lalu catat yang belum selesai di REVIEW.md.

## 12. Strategi test

Integration test dengan `WebApplicationFactory` dan database SQLite sementara per test:

1. **Standard flow**: Alice -> CRM/NonProduction/Read, Bob approve -> Approved.
2. **High-risk flow**: Production (CRM): Bob approve -> PendingSystemOwner, Carol approve -> Approved. Admin (Finance Portal): Bob approve -> menunggu Dana.
3. **Authorization failure**: Dana approve request CRM -> 403; Carol approve di tahap manager -> 403; Alice approve request sendiri -> 403; Erin approve -> 403.
4. **Idempotency**: `client_request_id` sama dikirim berulang dan paralel -> tepat satu row.
5. **Concurrency**: dua approve/reject paralel dengan version sama -> satu sukses, satu 409.
6. **Reject**: reason tersimpan, audit event ada, transition berikutnya ditolak; reject tanpa reason -> 400.
7. **Terminal state**: approve pada Approved/Rejected -> 409.
8. **Requester tanpa manager**: Bob create request -> 400.
9. **Audit**: jumlah dan urutan audit event sesuai; UPDATE/DELETE pada `audit_events` ditolak database.
10. **Constraint DB**: insert langsung yang melanggar check constraint (mis. high-risk tidak konsisten) ditolak.

Command: `dotnet test` (ditulis di README).

## 13. Trade-off penting

1. **SQLite vs PostgreSQL**: SQLite dipilih agar assessor bisa menjalankan tanpa instalasi apa pun. Konsekuensi: write dikunci per database dan tidak ada `rowversion`. Untuk production pindah ke PostgreSQL.
2. **Peran diturunkan dari relasi vs disimpan di kolom role**: diturunkan, supaya satu sumber kebenaran dan tidak bisa bertentangan. Konsekuensi: perlu query tambahan untuk menghitung peran (diringkas di `/api/me`).
3. **`version` integer + `IsConcurrencyToken` vs mekanisme lain**: mudah dijelaskan, portabel, bekerja di SQLite.
4. **Satu proyek Blazor Server + Minimal API vs dua proyek**: satu proyek menghindari CORS dan konfigurasi ganda, tapi pemisahan lapisan lebih lemah. Dimitigasi dengan menaruh semua aturan di service layer.
5. **Fluent API vs Data Annotations**: Fluent, karena foreign key `Restrict`, check constraint, dan konversi enum tidak bisa diatur lewat atribut; semua aturan database terbaca di satu file.
6. **404 vs 403 untuk request yang tidak boleh dilihat**: 404 karena Mencegah kebocoran informasi mengenai eksistensi suatu ID request kepada pihak yang tidak berkepentingan, namun menyulitkan debugging di sisi frontend atau internal developer karena tidak bisa membedakan secara instan apakah ID-nya salah ketik atau hanya terhalang hak akses.

## 14. Known limitation yang sudah diketahui (untuk REVIEW.md)

- Hanya user yang punya manager yang bisa membuat request (praktisnya hanya Alice pada data seed). Di production perlu hierarki manager lengkap atau aturan escalation.
- Autentikasi disimulasikan lewat header/switcher; tidak aman untuk production.
- SQLite dipakai demi kemudahan menjalankan; production sebaiknya PostgreSQL.

## 15. Di luar scope

SSO/OIDC nyata, email/notifikasi, microservices, message broker, cloud deployment, RBAC admin UI, dashboard, pagination kompleks. **Phase 2 tidak dikerjakan** sebelum Change Request dari assessor diberikan.

## 16. Perubahan plan

| Tanggal | Perubahan | Alasan |
|---|---|---|
| 07/10/26 | Tabel direvisi dari rancangan kasar: tambah `requester_id`, `policy_versions`, `is_high_risk`, kolom `from_status`/`to_status`/`version_after` di audit | Aturan bisnis butuh pemilik request, jejak policy, dan timeline audit |
| 07/10/26 | `users.role` disederhanakan jadi Employee/Auditor | Hak approve diturunkan dari relasi; hindari data ganda |
| 07/10/26 | Create ditolak (400) jika requester tidak punya manager | Semua request wajib lewat Manager approval; hierarki lengkap di luar scope |
| 07/10/26 | Pilih SQLite + Fluent API + Blazor Server | Lihat bagian Trade-off |