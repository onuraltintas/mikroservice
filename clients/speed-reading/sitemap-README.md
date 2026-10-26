# Sitemap sahipliği

Canlı edge yapılandırması `/sitemap.xml` isteğini backend'in `/api/speed-reading/sitemap.xml` endpoint'ine yönlendirir. Dinamik içerik için yetkili kaynak `SpeedReading.API/Controllers/SitemapController.cs` dosyasıdır; edge eşlemesi `infrastructure/caddy/Caddyfile.speed-reading.litespeed` içinde bulunur.

`generate-sitemap.js` elle çalıştırılabilen statik yardımcıdır:

```powershell
node generate-sitemap.js
```

Çıktı `public/sitemap.xml` dosyasıdır. Betik mevcut sabit sayfa listesini kullanır; CMS/blog için dinamik veri çekme bölümü henüz yalnız açıklama/örnektir. Bu nedenle canlı backend sitemap'inin bütün dinamik içeriğini ürettiği varsayılmamalıdır. Betik varsayılan build/CI komutuna bağlı değildir.

Statik dosya üretmeden önce `BASE_URL` ve sabit route listesini kontrol edin. Aynı sitemap için farklı kaynakları otomatik çalıştırmayın; canlı edge'in backend yönlendirmesini koruyun.
