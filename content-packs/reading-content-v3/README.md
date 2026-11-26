# Okuma içeriği v3 (18.11.2026) — canlıya uygulandı

18.11.2026'da canlıda (`speedreading_owned_db`) uygulanmış paketin kaydı. Depoya 26.11.2026'da eklendi; o güne kadar yalnız
yerel kopyada duruyordu ve depodaki içerik paketleri canlıdan geri kaldı.

- **Ne yapar:** kopya ya da birbirinin kırpılmış seçkisi olan **267 metni** özgün metinlerle yeniden yazar (başlık, içerik,
  kelime sayısı) ve bu metinlerin sorularını yeni metne göre günceller. Ayrıca bir kategori adındaki yazım hatasını düzeltir.
  - Metin kimlik önekleri: `d7b20000-` Kelime Gruplama 57, `c4a10000-` 48, `d9f30000-` 35, `d8f30000-` 24, diğer 103.
- **Koruma:** her satır yalnız beklenen eski değeri birebir taşıyorsa değişir; değilse atlanır ve sayılır.
- `apply.sql.gz` sha256 (açılmış): `1bf47db1f7a031d38ce9b7cc77239e7f4855ba5cfafcd0fa3e57c5ac64df9504`
- `rollback.sql.gz` sha256 (açılmış): `ccf03d4501a1263fe38354ee0a942a41a6d17eed0c6b90e0cf06a32159665bd0`
- Doğrulama (26.11.2026): canlı dışa aktarımda bu 267 metnin içeriği paketteki yeni içerikle birebir aynı.

Aynı gün `reading-question-repair/v2` de uygulandı (314 metin, 3.102 soru). İkisinin toplamı canlıda 18.11'de güncellenen
580 metindir. Güncel canlı içerik: `content-packs/live-snapshot/20261126/`.
