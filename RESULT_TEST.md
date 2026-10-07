# Hasil Integration Tests

Semua 10 skenario *Integration Test* untuk `AccessRequestHub` telah ditulis dan berhasil dieksekusi dengan status **PASSED**. 
Pengujian dilakukan menggunakan `WebApplicationFactory` beserta `Microsoft.EntityFrameworkCore.Sqlite` di memori terisolasi (`.db` terpisah per-instance test).

Berikut adalah ringkasan hasil test:

| No | Skenario Uji | Status |
|----|-------------|--------|
| 1 | **Test1_StandardFlow**<br>Alice (requester) membuat request, Bob (manager) approve. Status sukses menjadi 'Approved'. | ✅ Passed |
| 2 | **Test2_HighRiskFlow**<br>Alice request `Production`. Bob (manager) approve menjadi `PendingSystemOwner`. Carol (System Owner) approve menjadi `Approved`. | ✅ Passed |
| 3 | **Test3_AuthorizationFailure**<br>Penegakan hak akses: Aktor tak berwenang, auditor, dan *self-approval* mendapat 403 Forbidden. | ✅ Passed |
| 4 | **Test4_Idempotency**<br>Pengiriman payload kembar dalam waktu bersamaan (simulasi parallel) tepat membuahkan 1 row di DB tanpa duplikasi. | ✅ Passed |
| 5 | **Test5_Concurrency**<br>Dua aksi Approval dan Rejection berjalan paralel pada Version yang sama; satu aksi lolos dengan `200 OK` dan yang lain ditolak dengan `409 Conflict`. | ✅ Passed |
| 6 | **Test6_RejectReason**<br>Menolak request tanpa `reason` menghasilkan 400 Bad Request. Reject dengan alasan valid berhasil dengan tipe event `Rejected`. | ✅ Passed |
| 7 | **Test7_TerminalState**<br>Aksi apa pun (approve/reject) pada request yang sudah `Approved`/`Rejected` ditolak secara konsisten dengan `409 Conflict`. | ✅ Passed |
| 8 | **Test8_RequesterTanpaManager**<br>Membuat request oleh karyawan yang tidak punya manager (Dana) langsung ditolak dengan `400 Bad Request`. | ✅ Passed |
| 9 | **Test9_AuditAppendOnly**<br>Upaya memodifikasi record (misalnya `Reason`) di tabel `AuditEvents` gagal dan digagalkan oleh SQLite Trigger dengan melemparkan DbUpdateException. | ✅ Passed |
| 10 | **Test10_ConstraintDB**<br>Memasukkan data by-pass EF langsung ke DbContext yang melanggar aturan bisnis (seperti Environment `Production` tetapi `is_high_risk = false`) secara mutlak digagalkan oleh Check Constraint SQLite. | ✅ Passed |

## Output Eksekusi Test:
```
Test run for D:\dt\accessrequesthub\AccessRequestHub.Tests\bin\Debug\net10.0\AccessRequestHub.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 2 s - AccessRequestHub.Tests.dll (net10.0)
```

Seluruh fondasi backend telah 100% tervalidasi kesesuaiannya dengan spesifikasi *PLAN.md*.
