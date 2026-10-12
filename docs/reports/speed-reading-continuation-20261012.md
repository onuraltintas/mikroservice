# Hızlı Okuma: program sonrası devam ve takip ölçümleri

## Davranış

- Tamamlanmış her programın kalıcılık ve transfer ölçümleri program kimliğiyle açılır. Yeni aktif eğitim bunları gizlemez ve eski sonuçlar yeni programı tamamlamaz.
- Eğitim sonrası ölçümün sabitlenmiş seviye kataloğu kullanılır. Hız ve anlama seviyelerinden düşük olan ölçülen seviyedir; V3 eğitim seviyesi 1–5 aralığına sınırlandırılır. Program seçimi yaş grubunun açık `placementLevel` alanına göre yapılır. Eski puan aralığı yalnız eski şablon uyumluluğudur.
- Öneri, ham hız/anlama değerlerini ve varsa başlangıca göre değişimi açıklar. Eğitim başarı oranı bilgi olarak gösterilir; ölçümü yapay olarak yükseltmez. Bu ürün kuralıdır, bilimsel olarak doğrulanmış otomatik karar modeli olduğu iddia edilmez.
- Bağımsız öğrenci öneriyi açıkça onaylar; isterse uyarıyı işaretleyerek tam bir üst seviyeyi seçer. En yüksek seviyede üst seçenek yoktur. Aktif eğitim tamamlanmadan ikinci eğitim başlatılamaz.
- Yönetilen öğrenci seçimini kalıcı onay talebi olarak gönderir. Yetkili öğretmen/kurum yalnız bu güncel seçimi onaylar; başka öğrenciye, farklı programa veya eski ölçüme ait istek reddedilir.
- Ölçülen, önerilen ve seçilen eğitim seviyeleri raporda ayrı gösterilir. Önceki programın takip sonucu, arada yeni eğitim başlamışsa yalnız eski eğitime atfedilmemesi gerektiği uyarısını içerir.
- Tamamlanmış başlangıç ölçümünü yeniden hesaplamak veya başlangıç ölçümünü tekrar atlamak, geçmişi olan kullanıcıda yeni program başlatmaz ve güncel profili sıfırlamaz.

## Veri ve yetki

Yeni tablo: `speed_reading.program_continuations`. Kaynak/yeni ilerleme, ölçüm, seçilen şablon, önerilen/ölçülen seviye, politika sürümü, uyarı onayı ve zamanlar tutulur. Gereksiz bir ikinci onaylayan kişi kimliği kopyası tutulmaz. Öğrenci verisi silindiğinde kendi karar kayıtları FK ebeveynlerinden önce silinir.

`Permissions.SpeedReading.StudentProgramApprove`, rapor okumadan ve genel katalog düzenlemeden ayrıdır. Teacher, InstitutionAdmin ve InstitutionOwner rolüne atanır; öğrenci/kurum kapsamı ayrıca sunucuda doğrulanır. Var olan JWT'ler bu yeni yetkiyi oturum yenilenene kadar içermez. Identity servisi yeniden başlatılmadan tek yetki/rol ataması kontrollü SQL ile eklenebilir; seed kodu sonraki Identity sürümleri için de güncellendi.

## Doğrulama

- Backend birim testleri: 1.254 geçti.
- Speed Reading Angular testleri: 939 geçti.
- Son ilgili geniş entegrasyon seçimi: 194 geçti; son model/başlangıç tekrar kontrolü ayrıca 6 testte geçti.
- Gerçek PostgreSQL: yeni migration ileri/geri, karar kaydı, iki kullanıcı kapsamı, eski takip formu, eşzamanlı profil güncelleme, başlangıç yeniden hesaplama/atlama kontrolü.
- Staff öneri bileşeni: 2 test geçti.
- Masaüstü/mobil tarayıcı: 8 test geçti. Tarayıcı testleri istek/ekran sözleşmelerini kontrollü API yanıtlarıyla sınar; PostgreSQL testleri gerçek veritabanı davranışını ayrıca doğrular.
- Üç üretim çıktısı derlendi. Mevcut öğrenci player/staff SCSS boyut uyarıları yayın hatası değil; bu çalışmada kapsam dışı kaldı.
- Yayın kurtarma testi: sahte Docker/veritabanı ile başarı, komut hatası, SIGINT ve SIGTERM yolları geçti. Hata/kesintide yalnız hedef üç servisin eski imajları açılır.

## Yayın

Yayın yöntemi: `infrastructure/scripts/deploy-speed-reading-continuation.sh`.
Yalnız Speed Reading API/öğrenci frontend ve ortak staff frontend yeniden oluşturulur. Hızlı Okuma ve Identity DB yedekleri, geçici ayrı veritabanlarında migration/yetki provası, mevcut Hızlı Okuma tablolarının satır/hashi karşılaştırması ve sağlık kontrolü zorunludur. Başarısız yayında eski uygulama imajları geri gelir; yeni boş tablo kalır, veri geçmişi düşürülmez. Koçluk DB/API ve Identity çalışma imajı değiştirilmez.

Canlı yayın sonucu doğrulama tamamlandıktan sonra eklenecektir.
