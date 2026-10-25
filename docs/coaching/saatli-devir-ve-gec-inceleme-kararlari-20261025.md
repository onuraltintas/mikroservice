# Saatli görev devri ve geç incelemeden karar — 25 Ekim 2026

## 3. Saatli manuel çalışmaların planlar arasında devri

Önceki devam-kapsamı paketinde ortaklaştırılan `CoachingStudyContinuation.Copy` yolu ders bağlantısı olmayan saatli görevi `Schedule` ile kopyalar. Ders bağlantısı olan görevler `SetWeeklyPlacement` ile ders kimliğini ve adını da taşır. Böylece yalnız ders bağlantısı olmaması nedeniyle yıllık plan oluşturma hatası oluşmaz ve otomatik devam görevinde saat kaybolmaz.

Bu pakette PostgreSQL üzerinde ardışık yıllık → otomatik → manuel plan yayımlama testi eklendi. İlk kaynak görev, ders bağlantısı olmayan saatli ve sabit bir manuel çalışmadır. Test her devamda planlanan tarih/saat, başlık, süre, kaynak kitap, soru hedefi, sabitlik ve kök/önceki görev bağlantısını kontrol eder. İlk görev tüm alanlarıyla değişmeden kalır. İlgili üretim yolu önceki pakette düzeltilmiş olduğundan ikinci bir kopyalama uygulaması eklenmedi.

Saat seçilen yeni dönemde geçerliyse korunur; yeni dönem dışında kalan görevler mevcut yerleştirme kurallarıyla uygun saate taşınır. Sığmayan iş sessizce düşürülmez.

## 4. Geç incelenen haftadan geleceğe karar

Yeni karar oluştururken son tarihin kaynak değerlendirme haftasından hemen sonraki yedi gün içinde olması şartı kaldırıldı. Kaynak `ReviewWeekStart` korunur; son tarih sonraki haftanın başlangıcından itibaren ileri bir tarih olabilir. Eski istemcilerin orijinal takip haftasına tarihli kayıt oluşturması da uyumluluk için kabul edilmeye devam eder.

Arayüz yeni kararda sunucunun İstanbul tarihini kullanarak `max(sonraki pazartesi, bugün)` önerir ve eski haftadan kaynaklanan üst tarih sınırını kaldırır. Örneğin 5 Ekim haftası 25 Ekim'de incelendiğinde 28 Ekim son tarihli karar doğrudan oluşturulur. Oluşturup ardından yapay bir erteleme kaydı eklemek gerekmez. Güncel haftadan karar oluşturulursa en erken tarih gelecek pazartesidir.

Yeni kararın `InitialDueOn` alanı seçilen ilk tarihi alır. Kaynak değerlendirme metni, önceki kararların ilk/güncel tarihleri ve geçmişleri yeniden yazılmaz. Mevcut gecikmiş kararın tarihini değiştirmek için gerekçeli erteleme işlemi gerekliliği sürer; bu değişiklik mevcut kararların tarihlerini sessizce değiştirmek için kullanılamaz. Orijinal takip haftası dışındaki mevcut kararların tarih alanı da düzenleme ekranında kilitli kalır; başlık, not ve durum güncellenebilir.

## Veri koruma ve doğrulama

Bu paket şema değişikliği veya veri dönüştürme gerektirmez. Yeni karar oluşturma, sürüm kontrolü ve bildirim işlemi mevcut atomik kayıt yolunu kullanır. Testler eski değerlendirmenin ve eski kararın tüm kayıt görüntülerini karşılaştırır; kaynak hafta, ilk tarih, tarih geçmişi, tekrar isteğinin aynı sürümü döndürmesi, hatalı sürümün reddi, öğretmen yetkisi ve gerekçesiz tarih değişikliğinin engellenmesini de doğrular.

Testler geçici PostgreSQL veritabanlarında çalıştırıldı. Canlıya dağıtım ve canlı veri değişikliği yapılmadı. Test ve derleme günlükleri `artifacts/coaching-timed-handoff-late-review-20261025/` altındadır.

Son doğrulama sonuçları:

- PostgreSQL destekli sunucu regresyonu: 237 geçti, 0 hata, 0 atlanan.
- API/veri koruma regresyonu: 20 geçti.
- Öğrenci/admin arayüzü: 159 geçti.
- Öğretmen/personel arayüzü: 142 geçti.
- Toplam 558 test geçti; iki arayüz ortak haftalık değerlendirme senaryolarını kendi derleme bağlamlarında da çalıştırır.
- API Release: 0 hata, 0 uyarı. Öğrenci/admin ve personel üretim derlemeleri başarılı; mevcut paket/stil boyutu bütçesi uyarıları devam ediyor.
