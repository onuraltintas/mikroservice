# Admin / öğretmen eğitim programları

1 Ekim 2026 — yerel geliştirme; henüz canlıya alınmadı.

## Kullanım

- Öğrenci rolü olmayan Admin, SystemAdmin veya Teacher hesabı öğrenci paneline girdiğinde `/student/training-programs` açılır.
- Ayrı **Eğitim Programları** sekmesinde aktif ve silinmemiş eğitim programları listelenir. Seviye tespit şablonları eğitim olarak başlatılmaz; pasif içerik yayımlanmaz.
- Kullanıcı istediği programa açıkça kaydolur. Seviye tespiti, öğrenci aboneliği veya sonraki program önerisi bu özel akışın önkoşulu değildir.
- Aynı anda bir aktif program vardır. Tekrarlanan başlatma ilerlemeyi sıfırlamaz; farklı programa geçmek için aktif program tamamlanır.
- Günün bütün egzersizleri bitince sonraki gün hemen açılır. Gün veya egzersiz atlanmaz; yalnız takvim beklemesi kaldırılır.
- Egzersiz kataloğu önizlemesi hâlâ yereldir. Eğitim programından açılan oturum ise sunucuda kaydedilir.
- Program bitince katalogdan aynı veya farklı program başlatılabilir. Geçmiş ilerleme kayıtları silinmez.
- Okuma materyali seçilen eğitim programının yaş grubundan alınır.

## Güvenlik ve veri

- İstemcinin gönderdiği kullanıcı kimliği veya takvim-bypass parametresi kabul edilmez. Kullanıcı kimliği oturumdan, eğitim modu sunucunun oluşturduğu ilerleme kaydından gelir.
- Yeni `IsStaffTraining` alanı eski kayıtlarda false; yalnız özel, rol kontrollü kayıt endpoint'i true üretir.
- Hesaba Student rolü eklenirse veya admin/öğretmen yetkisi kaldırılırsa aktif özel eğitim kaydı kişisel eğitim endpoint'lerinde kullanılamaz.
- Öğrenci+öğretmen veya öğrenci+admin hesapları bu ayrıcalıklı akışa giremez; normal öğrenci kuralları uygulanır.
- Kurum yöneticisi veya Editor rolü tek başına yeterli değildir.
- `20261001020000_AddStaffTrainingMode` migration'ı yalnız Hızlı Okuma veritabanına ek bir boolean sütun getirir. Koçluk ve Identity şemaları değişmez.

## Doğrulama

- Rol matrisi, çift rollü hesabın endpoint'ten reddi, ilk kayıt, idempotent tekrar, okuma materyali yaşı ve normal öğrenci takvim kuralı için testler var.
- Gerçek PostgreSQL'de migration SQL ileri/geri çalıştırıldı; iki günü aynı gün bitirme, normal öğrencinin ikinci günde kilitlenmesi, program bitişi ve geçmişi koruyarak yeniden kayıt doğrulandı.
- Arayüzde katalog yükleme, açık kayıt isteği, aktif eğitime yönlendirme, menü, preview/kalıcı oturum ayrımı ve bitiş CTA test edildi.
- İlgili entegrasyon grubu: 39 geçti. Öğrenci frontend tam süiti: 377 geçti. Node takvim sözleşme testleri: 2 geçti.
- API Release: 0 hata, 0 uyarı. Frontend production derlemesi başarılı; önceden mevcut player SCSS bütçe uyarısı sürüyor.
- Canlı tarayıcı uçtan uca testi yapılmadı; canlı migration/deploy uygulanmadı. Kod kapsamı yüzdesi ölçülmedi.

## Yayın öncesi

Hızlı Okuma yedeği, migration sırası, geri dönüş politikası ve gerçek admin/öğretmen hesabıyla tarayıcı kabul testi gereklidir. Önceki öğrenci program döngüsü migration'ı da bu sürümün bağımlılığıdır. Uygulama yayımlanmadan kullanıcı bilgilendirilecek.
