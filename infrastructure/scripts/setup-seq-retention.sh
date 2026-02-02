#!/bin/bash
# ===========================================
# Seq Retention Policy Kurulum Scripti
# ===========================================
# Production deployment sırasında çağrılır
# Idempotent - mevcut policy varsa tekrar eklemez

SEQ_URL="${SEQ_URL:-http://localhost:5341}"

echo "🔄 Seq retention policy ayarlanıyor..."

# Seq'in hazır olmasını bekle
for i in {1..30}; do
    if curl -s "$SEQ_URL/api" > /dev/null 2>&1; then
        break
    fi
    sleep 2
done

# Mevcut policy sayısını kontrol et
EXISTING=$(curl -s "$SEQ_URL/api/retentionpolicies" 2>/dev/null | grep -c "retentionpolicy" || echo "0")

if [ "$EXISTING" != "0" ]; then
    echo "✅ Retention policy zaten mevcut. Atlanıyor."
    exit 0
fi

# 30 günlük genel retention policy ekle
curl -s -X POST "$SEQ_URL/api/retentionpolicies" \
    -H "Content-Type: application/json" \
    -d '{"RetentionTime":"30.00:00:00"}' > /dev/null 2>&1

echo "✅ 30 günlük retention policy eklendi."
