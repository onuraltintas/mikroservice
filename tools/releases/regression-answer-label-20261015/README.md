# Regresyon cevap gösterimi · 15.10.2026

Kaynak `a760f9fc7c8ecb072718749ebd76d8a67a187f27`. [Yayın ve test raporu](../../../docs/reports/regression-answer-label-20261015.md).

Akış `prepare → build → promote → validate`. Yalnız öğrenci ön yüzü değişir. Mevcut tam imaj sınırı ve tüm diğer konteyner kimlikleri doğrulanır. Tam önceki imaja sağlık kontrolüyle geri dönüş hazırlanır. Aday 141 dosyanın tamamı `frontend.sha256` ile doğrulanır. Eski hashli JS dosyaları korunur.

`payload.sha256` özgün arşiv manifestidir. Büyük frontend payload'ı, frozen compose ve ortam sırları Git'e eklenmez. Özgün sunucu dizini `/var/lib/eduivme/releases/regression-answer-label-20261015`. Yayınlandıktan sonra `promote` tekrar çalıştırılmaz. `observe.sh` yalnız durum, HTTP, restart, hata ve CAPTCHA korumasını okur.
