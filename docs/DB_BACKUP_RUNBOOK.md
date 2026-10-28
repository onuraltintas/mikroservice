# Veritabanı yedeği (P1)

Her gece 03:17'de (Türkiye saati) sunucudaki her PostgreSQL veritabanının yedeği alınır. Ayarlanırsa şifreli bir kopyası sunucu dışına da gönderilir.

- **Betik:** `infrastructure/backup/eduivme-db-backup.sh`
- **Zamanlayıcı:** `infrastructure/systemd/eduivme-db-backup@.timer`, servis: `infrastructure/systemd/eduivme-db-backup@.service`. Örnek adı konteyner adıdır.

## Ne yapılır

- Konteynerdeki her veritabanı için `pg_dump -Fc` alınır; roller için `pg_dumpall --globals-only`.
- Her döküm `pg_restore --list` ile geri okunur. Sonra `SHA256SUMS` yazılır.
- Hata olan gece yarım yedek bırakılmaz. Durum `last-status` dosyasına yazılır (`ok …` ya da `failed …`).
- **Sunucuda saklanan:** son 7 gün, son 4 pazar ve son 6 ayın ilk yedeği.
  - Haftalık ve aylık kopyalar sabit bağlantıdır (hard link); günlük kopya durdukça ek yer kaplamaz.
  - Yer: `/var/backups/eduivme-db/<konteyner>/{daily,weekly,monthly}/yyyy-MM-dd/`
- **Sunucu dışı kopya (isteğe bağlı):**
  - Her dosya `age` ile şifrelenir; sunucuda yalnız açık anahtar durur.
  - `rclone` ile `<remote>/<konteyner>/<tarih>/` altına kopyalanır.
  - 190 günden eski kopyalar silinir.
  - Şifreleme anahtarı verilmeden dışarı kopya gönderilmez.

## Kurulum (sunucuda, bir kez)

1. Araçları kur: `sudo apt install age rclone` (`curl` zaten var).
2. Sunucu dışı depo için:
   - `rclone config` ile bir remote tanımla (ör. Backblaze B2 ya da Cloudflare R2), ör. `offsite:eduivme-yedek`.
   - Şifre anahtarını **kendi bilgisayarında** üret: `age-keygen -o eduivme-yedek.key`.
   - Özel anahtarı parola yöneticisinde sakla; sunucuya **koyma**. Açık anahtar (`age1…`) sunucuya yazılır.
3. Ayar dosyasını hazırla:
   - `sudo mkdir -p /etc/eduivme`
   - `sudo cp infrastructure/backup/db-backup.env.example /etc/eduivme/db-backup.env`
   - `sudo chmod 600 /etc/eduivme/db-backup.env`
   - `BACKUP_RCLONE_REMOTE` ve `BACKUP_AGE_RECIPIENT` değerlerini doldur. İstersen `BACKUP_HEALTHCHECK_URL` de ekle; ör. healthchecks.io, yedek gelmezse e-posta atar.
4. Birimleri kur ve zamanlayıcıları aç. Kurs yönetimi veritabanı ayrı konteynerdedir; adını `docker ps` ile kontrol et.

   ```bash
   sudo cp infrastructure/systemd/eduivme-db-backup@.service infrastructure/systemd/eduivme-db-backup@.timer /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable --now eduivme-db-backup@postgres.timer
   sudo systemctl enable --now eduivme-db-backup@eduivme-production-course-management-db-1.timer
   ```
5. İlk yedeği hemen al ve kontrol et:

   ```bash
   sudo systemctl start eduivme-db-backup@postgres.service
   cat /var/backups/eduivme-db/postgres/last-status
   ls -l /var/backups/eduivme-db/postgres/daily/
   ```

## Geri yükleme provası (ayda bir önerilir)

Geri yükleme her zaman önce ayrı bir prova veritabanına yapılır; canlı veritabanının üzerine yazılmaz.

```bash
cd /var/backups/eduivme-db/postgres/daily/<tarih>
sha256sum -c SHA256SUMS
user=$(docker exec postgres printenv POSTGRES_USER)
docker exec postgres createdb -U "$user" coaching_db_drill
docker exec -i postgres pg_restore -U "$user" -d coaching_db_drill --no-owner < coaching_db.dump
docker exec postgres psql -U "$user" -d coaching_db_drill -c 'select count(*) from coaching.assignments'
docker exec postgres dropdb -U "$user" coaching_db_drill
```

Sunucu dışı kopyadan geri yükleme:
1. Kopyayı indir: `rclone copy offsite:eduivme-yedek/postgres/<tarih> ./yedek`
2. Özel anahtarla çöz: `age -d -i eduivme-yedek.key -o coaching_db.dump coaching_db.dump.age`
3. Yukarıdaki adımlarla aynı şekilde geri yükle.

> Migration geri almadan (Down) önce mutlaka güncel yedek alın. EF Core 10'da reddedilen bir geri alma, daha yeni migration'ları geri alınmış bırakabilir; düzeltme yedekten dönmektir.
