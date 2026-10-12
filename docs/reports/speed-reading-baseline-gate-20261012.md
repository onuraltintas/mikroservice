# Başlangıç seviye tespiti — aktif eğitim kilidi

## Kapsam

- Devam eden programda yeni veya yarım kalmış başlangıç sınavı açılmaz.
- Eski form kimliğiyle başlangıç egzersizi başlatma/devam etme isteği de reddedilir.
- Sınav açıkken program atanırsa sonuçlandırma öğrenci atama kilidi altında reddedilir. Profil, mevcut program ve geçmiş ölçümler değişmez.
- İlk program öncesi başlangıç sınavı ve yarım kalan sınava dönüş korunur.
- Program bitiminde eğitim sonrası ölçüm; önceki programların kalıcılık/transfer ölçümleri korunur.
- Doğrudan başlangıç sınavı URL'si kullanılabilirliği kontrol eder; kapalıysa açıklamayla dashboard'a döner. Eşzamanlı atama halinde API mesajı gösterilir; yanlış “egzersizleri tamamlayın” uyarısı verilmez.
- Bitmiş başlangıç sınavı geçmişi raporlar için korunur. Bu çalışma tekrar sınavı için yeni bir ürün akışı açmaz.

## Testler

- TDD: başlangıç/yeni form, eski form egzersizi ve faz planı için 4 başarısız reproducer; düzeltmeden sonra başarılı.
- Sonuçlandırma yarış reproducer'ı önce başarısız, atama kilidi altındaki koruma sonrasında başarılı.
- Yanlış kullanıcı mesajı için 2 başarısız Angular testi; düzeltmeden sonra başarılı.
- Backend birim: 1.261 geçti.
- Angular: 949 geçti.
- Hızlı Okuma entegrasyon: 195 geçti; gerçek PostgreSQL atama/sınav başlatma, eski form oturumu ve sonuçlandırma yarışlarını içerir.
- Masaüstü/mobil Playwright: 14 geçti. UI istek/ekran sözleşmeleri kontrollü API yanıtlarıyla sınanır; gerçek veritabanı kuralları PostgreSQL testlerinde ayrıca doğrulanır.
- API ve öğrenci frontend üretim çıktıları derlendi. Önceden var olan player SCSS boyut uyarısı kapsam dışıdır.
- İzole sahte Docker yayın testi: başarı, komut hatası, SIGINT ve SIGTERM geri dönüşü geçti; hiçbir gerçek veritabanı/servis değiştirilmez.
- Kod incelemesinde bulunan sonuçlandırma yarışı ve yanlış 400 mesajı giderildi.

## Yayın yöntemi

`infrastructure/scripts/deploy-speed-reading-baseline-gate.sh` yalnız Hızlı Okuma API/öğrenci frontend imajlarını değiştirir. Önce mevcut imaj referansları ve Hızlı Okuma veritabanı yedeği tutulur. Yeni imajda salt okunur program denetimi ve veri parmak izi karşılaştırması yapılır. Sağlık kontrolü başarısızsa eski iki imaja dönülür.

Migration, katalog/program veri güncellemesi veya kullanıcı sonuç silme yoktur. Identity, Koçluk ve ortak öğretmen/kurum paneli bu yayında değiştirilmez.

Canlı yayın sonucu aşağıya doğrulandıktan sonra eklenecektir.
