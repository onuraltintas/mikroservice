# Koçluk — kartlar, renk ve tipografi

20.10.2026 · Canlı sürüm: `coaching-workspace-depth-20261020-r2`

Öğrenci ve öğretmen arayüzüne ölçülü bir görsel derinlik eklendi. Yeni davranış veya veri alanı eklenmedi.

## Görsel değişiklikler

- Beyaz kartlarda iki katmanlı, yumuşak gölgeler; koyu temada daha kontrollü gölge değerleri.
- Ana başlıklarda güçlü ağırlık, dengeli satır bölünmesi ve hafif harf aralığı düzenlemesi.
- Karşılama bölümlerinde yeşilden yüzeye geçen sakin renk geçişi.
- Öğrencinin günlük planında vurgulu üst kenar ve daha belirgin kart; kısayollarda krem, haftalık değerlendirmede mavi tonlu hafif zemin.
- Öğretmenin öğrenci kartları, sekmeleri ve takip sayaçlarında aynı yüzey kuralları; değerlendirme sayaçlarında seçici mavi/krem vurgular.
- Düğmelerde sınırlı gölge, mevcut odak stilleri ve hareket azaltma tercihi korunuyor.
- Takvim ders renkleri, tamamlanma durumları, merkezi toaster ve onay pencerelerinin davranışı değiştirilmedi.

## Kapsam

Uygulama değişikliği tek ortak SCSS dosyası ve iki uygulamanın bu dosyayı içe aktarmasından oluşuyor:

- `clients/admin-panel/src/app/features/coaching-portal/layout/coaching-workspace-depth.scss`
- `clients/admin-panel/src/styles.scss`
- `clients/admin-panel/projects/staff-portal/src/styles.scss`

Dağıtım mevcut canlı kaynakların kopyasından yapıldı. Paket doğrulaması, bu üç değişiklik dışında öğrenci/öğretmen kaynaklarının ve ortak bileşenlerin aynı kaldığını doğruladı. Yerel ağacın başka değişiklikleri dağıtılmadı. Veritabanı geçişi ve backend dağıtımı yapılmadı.

## Kontroller

- Öğrenci: 46 dosyada 310 test; öğretmen: 34 dosyada 281 test. Toplam **591 test yürütmesi geçti**.
- İki uygulamanın yerel ve sunucu production derlemeleri başarılı. Önceden mevcut paket/stil boyutu uyarıları sürüyor; bütçe sınırları yükseltilmedi.
- Kontrast kontrolü yeni yeşil, krem ve mavi zeminlerde ana metni ve ikincil metni de kapsayacak şekilde genişletildi. Açık/koyu temada 4.5:1 kontrolü başarılı.
- Canlı öğrenci ana sayfası ve öğretmen paneli açık/koyu temada incelendi; yeni gölge ve renk geçişleri hesaplanan stillerde doğrulandı.
- Öğrenci 320 piksel genişlikte `clientWidth=320`, `scrollWidth=320`; normal dar görünümde 521/521. Öğretmen 390 pikselde 375/375, 320 pikselde 305/305. Yatay sayfa taşması yok.
- Öğretmen takip merkezindeki mevcut üç kayıt ve sayaçlar erişilebilir; arama, filtre ve kart düzeni korunuyor.
- Öğrenci ve öğretmen tarayıcı konsolunda hata kaydı görülmedi. Tema tercihleri ve geçici ekran boyutları test sonunda geri alındı.
- Bu sürümün canlı testi görsel ve okuma kontrollerinden oluştu; plan, ödev veya profil kaydedilmedi.

## Dağıtım ve veri koruma

- Canlıya geçiş: `2026-10-20T17:34:58Z`.
- Paket SHA-256: `fc092cd0aa42deaca4db2031849fafbcee39a47a96a20e25d3d166ec5bbbc846`.
- Yalnız `admin-panel` ve `staff-portal` güncellendi. Diğer konteyner kimlikleri değişmedi.
- İki uygulamanın çalışma ortamı, kullanıcı, entrypoint, komut ve sağlık kontrolü ayarları korundu.
- Dağıtım öncesindeki Coaching, Identity ve Notification satırları dağıtım sonrası özetleriyle aynı. Teknik Inbox/Outbox/RefreshToken tabloları normal oturum işleyişi nedeniyle karşılaştırma dışında.
- JS/CSS dosyaları HTTP 200 ve doğru MIME türleriyle yanıt veriyor.
- Önceki görüntüler, sunucudaki sürüm klasörünün `rollback.compose.json` dosyasında kimlikleriyle korunuyor.

Kanıtlar: `artifacts/coaching-workspace-depth-20261020-r2/` altında `promotion.log`, `verification.log`, `teacher-light.png`, `teacher-dark.png`, `student-light.png`, `student-dark.png`.
