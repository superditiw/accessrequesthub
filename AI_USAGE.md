# AI Usage Notes

Selama masa pengembangan iterasi *Access Request Hub*, asisten AI dilibatkan dalam penulisan spesifikasi (*PLAN.md*), pembuatan kode, pemecahan masalah (debugging), hingga *refactoring*. Berikut adalah beberapa poin keterlibatan dan keputusan desain AI:

## 1. Desain Enum `AccessLevel`
Dalam spesifikasi dasar, *access level* hanya disebutkan memuat status `Admin` (untuk *flag* `is_high_risk`). AI mengambil inisiatif *best practice* dengan memperluas struktur `AccessLevel` menjadi `Read`, `Write`, dan `Admin`. Hal ini disetujui karena jenjang akses CRUD adalah fondasi umum pada skenario permohonan akses TI mana pun.

## 2. Inisiasi Current User di Blazor Server
Karena *AppDbContext* (EF Core) berjalan secara _scoped_ pada Blazor (di mana satu *circuit* bisa berjalan panjang), AI merancang dan mengimplementasikan mekanisme *Service Scope Factory* khusus pada komponen UI. AI kemudian menemui kendala *State* di mana *Scope* baru menghilangkan konteks dari *Current User*. AI lalu mengeksekusi _hotfix_ (dengan melakukan _mirroring context_ ke Scope baru) agar otentikasi bekerja persis layaknya HTTP Header pada layer API konvensional.

## 3. Resolusi Integrasi UI - Database
AI mengambil inisiatif untuk mengubah skema `ManagerId` pada `DbSeeder` guna menyempurnakan alur pengujian batas logika hierarki (*top-level manager/CEO* dan karyawan *low-level*). 

Keterlibatan AI diarahkan oleh *User* lewat siklus *pair-programming*, dengan berpegang teguh pada pemisahan logika (Bisnis selalu di *Service*, Presentasi selalu di UI/Endpoint API).

## 4. Diskusi Awal Perancangan PLAN.md
Sebelum implementasi dimulai, *User* berdiskusi dengan asisten AI lewat chat untuk merancang *PLAN.md*. Diskusi mencakup pemahaman isi assessment, pemilihan stack, rancangan tabel dan relasi, model peran, serta penanganan kasus requester tanpa manager. AI menyusun draf awal *PLAN.md*, kemudian *User* meninjau dan menyesuaikannya sendiri. Keputusan akhir pada *PLAN.md* ada pada *User*.