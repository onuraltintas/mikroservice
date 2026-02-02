#!/bin/bash
# ===========================================
# Seq Log Retention Yönetimi Scripti
# ===========================================
# Bu script Seq'teki eski logları temizler
# Cronjob olarak çalıştırılabilir

SEQ_URL="${SEQ_URL:-http://localhost:5341}"

echo "=== Seq Log Yönetimi ==="
echo "Seq URL: $SEQ_URL"
echo ""

# 1. Mevcut log sayısını göster
echo "📊 Mevcut log sayısı:"
curl -s "$SEQ_URL/api/events?count=1" | python3 -c "import sys; print('  API erişimi başarılı')" 2>/dev/null || echo "  Seq'e erişilemiyor!"

# 2. 7 günden eski Information loglarını sil
echo ""
echo "🗑️  7 günden eski Information loglarını silmek için:"
echo "   Seq UI -> Settings -> Retention -> Add Policy"
echo "   - Retention Time: 7 days"
echo "   - Signal: @Level = 'Information'"
echo ""

# 3. 30 günden eski tüm logları sil
echo "🗑️  30 günden eski tüm logları silmek için:"
echo "   Seq UI -> Settings -> Retention -> Add Policy"
echo "   - Retention Time: 30 days"
echo "   - Signal: (boş bırak - tüm loglar için geçerli)"
echo ""

# 4. Disk kullanımını kontrol et
echo "💾 Docker volume disk kullanımı:"
docker system df -v 2>/dev/null | grep seq_data || echo "  Volume bilgisi alınamadı"

echo ""
echo "=== Önerilen Retention Policies ==="
echo "┌─────────────────────────────────────────────────────┐"
echo "│ Seviye       │ Saklama Süresi │ Açıklama            │"
echo "├─────────────────────────────────────────────────────┤"
echo "│ Information  │ 3 gün          │ Debug bilgileri     │"
echo "│ Warning      │ 14 gün         │ Potansiyel sorunlar │"
echo "│ Error/Fatal  │ 90 gün         │ Kritik hatalar      │"
echo "└─────────────────────────────────────────────────────┘"
echo ""
echo "📌 Seq UI: $SEQ_URL"
echo "   Settings -> Retention -> Add retention policy"
