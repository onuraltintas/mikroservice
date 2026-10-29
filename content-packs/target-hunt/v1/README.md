# Hedef Avı — v1

Karar 29.10.2026: N-back (`Focus`) dikkat egzersizi programlarda **Hedef Avı** ile değiştirilir. Bu paket yeni `TargetHunt` egzersiz türünü (motor `target_hunt`, kategori `attention-and-eye-movement`) ve dört yaş grubunda beşer, toplam 20 normal eğitim egzersizini ekler. Mevcut hiçbir kayıt değiştirilmez veya silinmez; N-back egzersizleri katalogda kalır. Programların yeni egzersizi kullanması ayrı pakettir: [`programs/v5`](../../programs/v5/README.md).

## Egzersiz

Ekranda satır satır harfler veya kısa kelimeler görünür. Öğrenci süre bitmeden hedefin geçtiği her yere dokunur; benzer görünen çeldiricilere dokunmaz. Hazır ekranında hedefli bir örnek satır gösterilir. Her dokunuş hemen yeşil (doğru) veya kırmızı (yanlış) işaretlenir.

- Yerleşimi sunucu üretir; her dokunuşu sunucu doğrular, süreyi sunucu tutar (duraklama hariç).
- Tüm hedefler bulununca, süre dolunca veya öğrenci "Bitirdim" deyince biter.
- Puan: bulunan hedef yüzdesi − yanlış dokunuş başına 5 puan (toplam ceza en fazla 30). Hata analiziyle aynı kural.
- Okuma hızı ve anlama ölçmez. Seviye tespitinde kullanılmaz.

## Ayarlar

Her hücre `hedef / çeldiriciler / satır×sütun / hedef sayısı / süre (sn)`. Bunlar ürün başlangıç ayarlarıdır, doğrulanmış yaş normları değildir. Bir yaş grubunda seviye arttıkça öğe sayısı azalmaz, öğe başına süre artmaz.

| Yaş grubu | S1 | S2 | S3 | S4 | S5 |
|---|---|---|---|---|---|
| Çocuk 9–12 | b / a e o / 5×8 / 10 / 60 | b / d o e / 6×8 / 12 / 70 | b / d p / 6×10 / 15 / 80 | b / d p q / 7×10 / 17 / 90 | ve / ev ne ye / 7×10 / 17 / 90 |
| Genç 13–16 | b / d o e / 6×10 / 15 / 75 | b / d p / 7×10 / 17 / 85 | b / d p q / 8×10 / 20 / 95 | ve / ev ne ye / 8×10 / 20 / 90 | kar / kra rak kır / 8×10 / 20 / 90 |
| Genç yetişkin 17–21 | b / d p / 7×10 / 17 / 75 | b / d p q / 8×10 / 20 / 85 | ve / ev ne ye / 8×10 / 20 / 80 | kar / kra rak kır / 9×10 / 22 / 90 | bir / dir bil rib / 10×10 / 25 / 95 |
| Yetişkin 22+ | b / d p / 7×10 / 17 / 75 | b / d p q / 8×10 / 20 / 85 | ve / ev ne ye / 8×10 / 20 / 80 | kar / kra rak kır / 9×10 / 22 / 90 | bir / dir bil rib / 10×10 / 25 / 95 |

Günlük süre tahmini egzersizin süre sınırından hesaplanır (yukarı yuvarlanmış dakika).

## Üretme ve doğrulama

Depo kökünden:

```sh
node content-packs/target-hunt/v1/generate.mjs
node --test content-packs/target-hunt/v1/pack.test.mjs
TARGET_HUNT_TEST_DATABASE_URL=postgres://… node --test tests/E2E/target-hunt-catalog.postgres.test.mjs
```

PostgreSQL testi verilen sunucuda geçici bir veritabanı açıp siler.

## Yayın sırası

Canlıya bu çalışmada uygulanmadı; ayrı yayın onayı gerekir.

1. Hedef Avı motorunu içeren API ve öğrenci uygulaması yayımlanır.
2. Hızlı Okuma veritabanı yedeği alınır; `psql -v ON_ERROR_STOP=1 -f apply.sql` çalıştırılır. Tek transaction'dır ve tekrar çalıştırılabilir. Kategori eksikse, aynı adla başka motorlu bir tür varsa veya kayıtlar paketten farklılaşmışsa hiçbir şey değiştirmeden durur.
3. Ardından `programs/v5` uygulanır.

Geri dönüş: programlar `programs/v5` rollback'iyle eski hâline döner. Bu paketin eklediği tür ve egzersizler zararsızdır; programlar kullanmadığı sürece görünür bir etkileri olmaz. Silmek gerekirse yedekten dönülür.
