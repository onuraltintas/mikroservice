# Koçluk çalışma planı asistanı

Öğrenci sohbet ekranı, Koçlukta seçili dersler, kaydedilmiş uygunluk saatleri ve mevcut haftalık taslakla çalışır. Sohbet mesajları kalıcı saklanmaz. Mesaj ve sınırlı sohbet bağlamı, yapılandırılmış OpenAI uyumlu sağlayıcıya yanıt üretmek için gönderilir; API anahtarı yalnızca Coaching API sunucusunda tutulur.

Özelliği etkinleştirmek için Coaching API ortamına şu ayarları ekleyin:

```text
Coaching__StudyPlanAssistant__Enabled=true
Coaching__StudyPlanAssistant__Endpoint=https://api.openai.com/v1/chat/completions
Coaching__StudyPlanAssistant__ApiKey=<secret manager üzerinden>
Coaching__StudyPlanAssistant__Model=<hesapta kullanılabilen model>
```

Yerel varsayılan ayar kapalıdır. API çağrısı başarısız veya yapılandırma eksikse öğrenci mevcut haftalık takvimi kullanmaya devam edebilir. Asistanın önerisi önce inceleme ekranında gösterilir; öğrenci açıkça eklemeden haftalık taslak değişmez. Sonraki yıllık plan önizleme ve yayımlama adımları mevcut servisler tarafından yönetilir.
