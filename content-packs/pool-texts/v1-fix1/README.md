# pool-texts/v1-fix1: dört soru düzeltmesi

27.11.2026 yayınından sonra yapılan içerik incelemesinde bulunan dört zayıf soru düzeltilir. Cevap harfleri, soru türü, Bloom düzeyi, sıra ve paragraf değişmez; yalnız soru kökü, açıklama ve seçenekler.

| Soru | Metin | Sorun | Düzeltme |
|---|---|---|---|
| `…2005` | Bay Havuç (Çocuk 1) | Mert'in neden güldüğü metinde yazmıyordu. | Soru, metinde yazan tepkiye çevrildi: gülmesi neyi gösterir. |
| `…1065` | Buzların Babaları (Çocuk) | Çeldiriciler okumadan elenecek kadar zayıftı. | Üç akla yatkın çeldirici; doğru şık metindeki "soğukta sıcak tutar" bilgisine bağlandı. |
| `…0410` | İlk Bağış (Genç Yetişkin) | Doğru şıkkın ifadesi bozuktu. | "İğnenin acıtmasından çekindiği için". |
| `…0196` | Kaçan Otobüs (Yetişkin) | Bir çeldirici anlamsızdı. | Metinle ilgili akla yatkın bir çeldirici. |

`fixes.json` yalnız yeni hâli tutar; eski hâl `../v1/questions.json`'dan okunur. `node generate.mjs` dört sorunun her birini ve bulunduğu metnin sekiz sorusunu soru kurallarından (`../../pool-questions/rules.mjs`) geçirir, paket genelindeki "okumadan tahmin" ölçüsünü hesaplar (en uzun %28,1, en kısa %22,1, örtüşme %31,9; sınır %35) ve `apply.sql` ile `rollback.sql` dosyalarını yazar.

## Uygulama
`pool-texts/v1`'den sonra, yalnız `speedreading_owned_db`:

```
docker exec -i postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d speedreading_owned_db' < content-packs/pool-texts/v1-fix1/apply.sql
```

- Beklenen: `pool-texts-v1-fix1: 4 question(s) fixed, 0 already done`. İkinci çalıştırmada `0 … 4 already done`.
- Bir soru yoksa ya da panelden elle değiştirilmişse `… missing or edited by hand; nothing changed` ile durur.
- Geri alma: `rollback.sql`, dört soruyu eski hâline döndürür (aynı korumayla). `pool-texts/v1` geri alınacaksa önce bu.

## Yerel deneme (27.11.2026, PostgreSQL 16, canlı anlık görüntü + dört havuz paketi)
Uygula → 4; yeniden → 0. Geri al → 4; yeniden → 0. Bir seçenek elle değiştirilince uygula hiçbir şey değiştirmeden durdu. Düzeltilince yeniden uygula → 4.
