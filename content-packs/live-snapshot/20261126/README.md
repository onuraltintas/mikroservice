# Canlı Hızlı Okuma içerik anlık görüntüsü (26.11.2026)

Canlı `speedreading_owned_db` veritabanından salt okunur dışa aktarım (sunucu Claude, 26.11.2026; arşiv sha256
`b4e058fb7e740991461e9f6953bca239e483ce1f48426a004aad888a68c89e0c`). Kişisel veri yok: oluşturan/güncelleyen hesap
kimlikleri, zaman damgaları, öğrenci ve oturum verileri çıkarıldı.

| Dosya | Satır | İçerik |
|---|---|---|
| `texts.jsonl.gz` | 831 | metin, md5, kelime sayısı, seviye, yaş grubu, egzersiz bağı, kategori |
| `questions.jsonl.gz` | 4.350 | bütün sorular (264'ü silinmiş) |
| `exercises.jsonl.gz` | 427 | egzersiz ayarları |
| `exercise_types.jsonl.gz`, `age_groups.jsonl.gz` | 22, 4 | türler ve yaş grupları |

**Neden:** 18.11.2026'daki iki içerik paketi (`reading-question-repair/v2`, `reading-content-v3`) depodaki paketlerin
`content.json` dosyalarını eskitti (268 metin farklı). Bundan sonraki içerik paketleri md5 korumasını ve seçimlerini bu
görüntüye göre yapar; eski paket dosyaları geçmiş kaydı olarak değişmeden durur.
