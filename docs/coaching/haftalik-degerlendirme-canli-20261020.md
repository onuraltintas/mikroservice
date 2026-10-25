# Haftalık değerlendirme ve kararlar — canlı doğrulama

20 Ekim 2026. Coaching API, öğrenci portalı ve öğretmen portalı **coaching-weekly-review-20261020** sürümüne alındı. `20261020130142_WeeklyReviewsAndDecisions` geçişi uygulandı. Mevcut kayıtlar korundu.

## Dağıtım ve yedek provası

- Çalışan dört Coaching modülünün PDB kaynak özetleri ile 448 mevcut kaynak dosyası karşılaştırıldı. Altı değiştirilen dosya ve yedi yeni dosyanın tamamı haftalık değerlendirme kapsamındadır; beklenmeyen fark veya kaldırılan kaynak yok.
- Canlı imajların bağımlılıkları korundu; yalnız dört Coaching DLL/PDB çifti değiştirildi. Öğrenci ve öğretmen arayüzleri canlı kaynakların kopyasına altı ilgili dosya uygulanarak sunucuda izole derlendi. Çalışma alanındaki başka değişiklikler yayımlanmadı.
- Coaching, Identity ve Notification veritabanlarının yedekleri alındı. Eski üç imaj geri dönüş için sabitlendi. Coaching geçişinden hemen önce ikinci yedek alındı.
- Canlı Coaching yedeği ayrı bir prova veritabanına geri yüklendi. Yeni imajla geçiş, model tutarlılığı, öğrenci değerlendirmesi, öğretmen okuma/karar oluşturma/durum güncelleme, üç aşamalı geçmiş, eski sürüm isteğinin reddi ve öğrencide görünme akışı çalıştırıldı. Eski tabloların içerik özetleri birebir aynı kaldı. Prova veritabanı kaldırıldı.
- Canlı geçiş yalnız iki yeni tablo ve indekslerini ekledi; eski tablolar veya sütunlar değişmedi. Hata halinde önceki imajlara dönüp eklemeli şemayı ve yeni yazımları koruyan geri dönüş betiği hazır tutuldu; geri dönüş gerekmedi.
- API ve iki portalın hazır olma kontrolleri başarılı. İlgisiz servislerin konteyner kimlikleri aynı kaldı. Public HTML, JavaScript ve CSS dosyaları doğru HTTP durumları ve MIME türleriyle sunuldu; gecikmeli haftalık ekran her iki gerçek oturumda yüklendi.

## Gerçek öğrenci ve öğretmen oturumları

Mevcut test öğrencisi ve ona atanmış mevcut öğretmen kullanıldı. Yeni hesap/ilişki oluşturulmadı; önceki plan, ödev veya çalışma düzenlenmedi. Değerlendirme haftası **12–18 Ekim 2026**, karar haftası **19–25 Ekim 2026** seçildi.

| Senaryo | Canlı sonuç |
|---|---|
| Öğrenci formunu doldurma | İyi gidenler, zorlanılan noktalar, gelecek hafta odağı ve 4/5 değerlendirme girildi. |
| Sekmelerde form koruma | Kaydetmeden Hedefler sekmesine geçilip Çalışma raporu sekmesine dönüldü; alanlar ve puan aynı kaldı. |
| Öğrenci kaydı ve yeniden yükleme | Başarı mesajı görüldü, hafta sunucudan yeniden yüklendi; içerik korundu. |
| Öğretmende değerlendirmeyi okuma | Aynı haftada öğrencinin metinleri, 4/5 puanı ve kayıt zamanı görüldü. Öğretmene öğrenci değerlendirmesini değiştiren form sunulmadı. |
| Öğretmen karar oluşturma | `CANLI HAFTALIK TEST 20261020 · Paragraf tekrarı` başlıklı, açıklamalı ve 23 Ekim son tarihli karar oluşturuldu. |
| Öğrencide ilk karar | Hafta yenilendi; aynı başlık, açıklama, son tarih ve Planlandı durumu görüldü. Öğrenciye karar düzenleme düğmesi sunulmadı. |
| Devam ediyor | Öğretmen durumu güncelledi. Öğrenci sunucudan yenileyince Devam ediyor görüldü. |
| Tamamlandı | Öğretmen ikinci güncellemeyi kaydetti. Öğrencide Tamamlandı rozeti ve tamamlanma zamanı görüldü. |
| Kalıcı geçmiş | Öğrenci ve öğretmende Karar geçmişi açıldı: Planlandı → Devam ediyor → Tamamlandı, tarih/saat, açıklama ve son tarihleriyle birlikte göründü. |
| Son sunucu kontrolü | Değerlendirme sürümü 0, puan 4; karar sürümü 2, Completed ve üç geçmiş girdisi veritabanından doğrulandı. |

Canlı iş akışında yeni hata saptanmadı. Bu sonuç sınanan akışları kapsar; tüm olası koşullar için hatasızlık garantisi değildir. Öğretmen değişiklikleri, öğrencinin haftayı yeniden yüklemesiyle doğrulandı; bu pakette anlık push güncellemesi veya yeni bildirim türü eklenmedi.

## Mevcut kayıtların korunması

Dağıtım öncesinde ve canlı testler sonrasında Coaching, Identity ve Notification veritabanlarındaki eski satırların içerik özetleri karşılaştırıldı. **Önceden mevcut tüm korunan satırlar içerikleriyle aynı kaldı.** Taşıma altyapısının Inbox/Outbox durumları ve oturum yenileme belirteçleri bu karşılaştırmanın dışındadır. Yeni geçiş kaydı ile yalnız yeni haftalık test kayıtları eklendi.

| Kayıt | Son durum |
|---|---|
| Aktif LGS planı | `a4cea155-5485-4db6-9f52-1ec4df8080c3`, sürüm 7, bitiş 1 Haziran 2027; aynı |
| Plan sayısı | 44; aynı |
| Çalışma sayısı | 62.016; aynı |
| Tamamlanan çalışma | 1; aynı |
| Kazanım bağlantısı | 61.979; aynı |
| Önceki ödev değerlendirmesi | 95/100, Graded; aynı |
| Yeni haftalık test kayıtları | 1 öğrenci değerlendirmesi ve 1 tamamlanan öğretmen kararı, geçmişiyle korundu |

API ve iki portalın dağıtım sonrası son günlüklerinde hata/istisna satırı **0**. Önceki imajlar ve veritabanı yedekleri sunucuda korundu.

## Testler ve kanıtlar

Yerel kabul sonuçları: 56 sunucu/gerçek PostgreSQL regresyon testi, 97 öğrenci ve 85 öğretmen arayüz testi başarılı. Sunucudaki iki üretim derlemesi de başarılı; öğretmen başlangıç paketi 991,24 kB ile mevcut 1 MB hata sınırının altında. Önceden bulunan stil/bütçe uyarıları devam ediyor. Bu dağıtımda uygulama kodunu değiştirmeyi gerektiren ek bir hata bulunmadı.

Kanıt dizini: `artifacts/coaching-weekly-live-20261020/`.

- `source-check-final.log`: canlı kaynaklardan izin verilen farklar ve yeni dosyalar.
- `final-verification.log`: gerçek kayıt sürümleri, durumlar, eski plan/çalışma/kazanım sayıları, imaj kimlikleri ve hata günlükleri kontrolü.
- `student-completed-decision.png`: canlı öğrencide tamamlandı durumu ve üç aşamalı geçmiş; dar ekran görünümü.
- `teacher-completed-decision.png`: canlı öğretmende değerlendirme, tamamlandı kararı ve takip işlemleri.
- `public-verification.tar.gz`: yedek provası, dağıtım, varlık erişimi ve veri koruma kanıtları; parola, bağlantı bilgisi veya veritabanı yedeği içermez.
