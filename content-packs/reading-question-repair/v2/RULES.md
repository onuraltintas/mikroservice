# Hızlı Okuma soru düzeltme kuralları (v2, 18.11.2026)

Okuduğunu anlama soruları öğrencilere gösteriliyor. Eylül'de otomatik bir onarım iki şeyi bozdu:
1. Bazı yanlış şıkların sonuna kalıp eklendi ("(metin bağlamında)", "Metin bu görüşü olayın temel nedeni olarak sunar." gibi). Batch dosyasında bu kalıplar zaten silinmiş olarak verilir; `padded_options_to_rewrite` hangi şıkların kalıp taşıdığını söyler.
2. Açıklamalar kalıp cümleyle dolduruldu (`explanation_is_template: true`).

## Görev (her `needs_work: true` soru için)
- **Açıklama:** `explanation_is_template` true ise yeni bir açıklama yaz. false ise mevcut açıklama iyiyse dokunma; kalıp ya da yanlışsa yenisini yaz.
- **Kalıplı şık:** `padded_options_to_rewrite` içindeki her şıkkı yeniden yaz: metne göre açıkça yanlış ama inandırıcı, diğer şıklarla aynı dil yapısında, doğal bir şık.
- **Uzunluk kuralı:** Doğru şık, dört şık içinde TEK BAŞINA en uzun olmamalı. Gerekirse bir yanlış şıkkı doğal biçimde uzat ya da yeniden yaz. Hedef: en az bir yanlış şık doğru şık kadar ya da daha uzun olsun; şıklar birbirine yakın uzunlukta olsun.
- **Asla değiştirme:** soru metni, doğru şıkkın metni, cevap anahtarı (`correct`). Doğru şıkkı yeniden yazma.

## Açıklama yazım kuralları
- Türkçe, 1–2 kısa cümle (en fazla ~300 karakter), yaş grubuna uygun kelimeler.
- Metne dayanır: metnin hangi bilgisine, hangi bölümüne dayandığını söyler ("Metnin başında…", "Son paragrafta…", "Ayşe'nin söylediğine göre…").
- Doğru şıkkı kelimesi kelimesine tekrar etmekle yetinmez; NEDEN doğru olduğunu söyler. Uygunsa en çekici yanlışın neden yanlış olduğunu tek yan cümleyle ekler.
- Şık harfi (A, B, C, D) ve "doğru cevap … şıkkıdır" kalıbı KULLANMA.
- Öğrenciye hitap gerekirse "sen" dili; çoğunlukla nötr anlatım yeterli.
- Kalıp ve tekrar eden cümle YASAK. Her açıklama o soruya özgü olmalı. Özellikle yasak ifadeler: "Yanıt, metindeki ilgili bilgi…", "bilgisini doğrudan verir", "doğru çıkarım “…” olur", "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında", "Metin bu görüşü…".
- Çıkarım/yorum sorusunda: metindeki ipuçlarını söyle ve çıkarımı kur.

## Yanlış şık yazım kuralları
- Metne göre kesinlikle yanlış olmalı (kısmen doğru ya da tartışmalı olmamalı).
- İnandırıcı olmalı: metindeki bir ayrıntıyı çarpıtan, yanlış kişiye/yere/nedene bağlayan ya da metinde olmayan ama akla yatkın bir seçenek.
- Meta dil kullanma ("metinde belirtildiği gibi", "bağlamında", "olarak sunar").
- "Hepsi", "hiçbiri" kullanma. Diğer şıklarla aynı biçim (cümle/öbek, nokta kullanımı).
- Dört şık birbirinden farklı olmalı.

## Sorunlu soru
Soru metinden cevaplanamıyorsa, cevap anahtarı yanlışsa, iki şık doğruysa ya da soru anlamsızsa: o soruyu paket dosyasına KOYMA; `flags` dosyasına yaz (id, kısa neden, varsa önerilen düzeltme).

## Metin yazım hataları
Batch'teki metinlerde (content) gördüğün yazım hatalarını, yarım kelimeleri, "---" gibi kalıntıları `typos` dosyasına yaz: {"text_id", "wrong": "<metindeki tam ifade>", "right": "<düzeltilmiş ifade>", "note"}. Metni yeniden yazma; yalnız kesin hataları listele (üslup tercihi değil).

## Çıktı dosyaları (batch NN için)
- `out/pack-NN.jsonl` — her satır: {"id": "<soru id>", "explanation": "...", "option_a": "..." (yalnız değişen şıklar, küçük harf anahtar), "note": "isteğe bağlı kısa not"}.
  - Kalıplı şıkkı yeniden yazmadıysan bile, kalıbı temizlenmiş halini (batch'teki metin) o şık için yazmalısın; aksi halde canlıdaki kalıplı metin kalır. Kısacası `padded_options_to_rewrite` içindeki her şık pakette bulunmalı.
- `out/flags-NN.jsonl`, `out/typos-NN.jsonl` (boş olabilir; dosyayı yine oluştur).

## Kontrol
Bitirince çalıştır ve hata kalmayana kadar düzelt:
```
python3 -I /home/claude/mikroservice/tools/reading-content-repair/validate_rewrite_pack.py /home/claude/rc/reading-questions-20261118.jsonl /home/claude/rc/out/pack-NN.jsonl --report /home/claude/rc/out/report-NN.json
```
Ek olarak: batch'teki `needs_work: true` sorularının hepsi ya pakette ya flags dosyasında olmalı.

## Pilot sonrası ekler
- Açıklamaların başlangıçlarını çeşitlendir; hepsi "Metne göre" ile başlamasın.
- Doğru şıkta yazım hatası görürsen doğru şıkkı DEĞİŞTİRME; pakette o sorunun "note" alanına "doğru şıkta yazım: <yanlış> → <doğru>" yaz.
- Soru metninde yazım hatası görürsen aynı şekilde "note" alanına "soru metninde yazım: <yanlış> → <doğru>" yaz.

## İkinci tur (batch 18–21)
Bu turdaki `needs_work` soruların çoğunda açıklama zaten iyi; sorun yalnız doğru şıkkın TEK BAŞINA en uzun olması (öğrenci okumadan tahmin edebiliyor).
- `explanation_is_template: false` ise açıklamaya dokunma (yanlışsa yeniden yaz ve "note" alanına neden yaz). Pakete yalnız değiştirdiğin şıkları yaz; açıklama alanı gerekmez.
- Bir ya da iki yanlış şıkkı doğal biçimde uzat ya da yeniden yaz: metne göre açıkça yanlış, inandırıcı, aynı biçimde. Doğru şıkkı kısaltma ya da değiştirme.
- `explanation_is_template: true` ise birinci turdaki gibi yeni açıklama yaz.
