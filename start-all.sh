#!/bin/bash

# Mikroservis Projesini Başlatma Scripti
# Bu script tüm servisleri sırayla başlatır

echo "🚀 Mikroservis Projesi Başlatılıyor..."
echo ""

# 1. Docker altyapısını kontrol et
echo "📦 Docker container'ları kontrol ediliyor..."
cd /home/onur/Projects/mikroservice
docker compose ps

echo ""
echo "✅ Altyapı servisleri çalışıyor!"
echo ""

# 2. .NET Servisleri başlat (arka planda)
echo "🔧 .NET Servisleri başlatılıyor..."

# Identity Service
echo "  → Identity Service başlatılıyor (Port: 5001)..."
cd /home/onur/Projects/mikroservice/services/identity-service/Identity.API
dotnet run &
IDENTITY_PID=$!

sleep 3

# Coaching Service
echo "  → Coaching Service başlatılıyor (Port: 5002)..."
cd /home/onur/Projects/mikroservice/services/coaching-service/Coaching.API
dotnet run &
COACHING_PID=$!

sleep 3

# Notification Service
echo "  → Notification Service başlatılıyor (Port: 5003)..."
cd /home/onur/Projects/mikroservice/services/notification-service/Notification.API
dotnet run &
NOTIFICATION_PID=$!

sleep 3

# API Gateway
echo "  → API Gateway başlatılıyor (Port: 5000)..."
cd /home/onur/Projects/mikroservice/services/api-gateway
dotnet run &
GATEWAY_PID=$!

sleep 3

echo ""
echo "✅ .NET Servisleri başlatıldı!"
echo ""

# 3. Angular Frontend başlat
echo "🅰️  Angular Frontend başlatılıyor (Port: 4200)..."
cd /home/onur/Projects/mikroservice/clients/admin-panel
npm run start &
ANGULAR_PID=$!

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "🎉 TÜM SERVİSLER BAŞLATILDI!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "📍 Servis URL'leri:"
echo "   • API Gateway:         http://localhost:5000"
echo "   • Identity Service:    http://localhost:5001"
echo "   • Coaching Service:    http://localhost:5002"
echo "   • Notification Service: http://localhost:5003"
echo "   • Angular Frontend:    http://localhost:4200"
echo ""
echo "📍 Altyapı URL'leri:"
echo "   • RabbitMQ Management: http://localhost:15672 (eduplatform/rabbitmq_secret_2024)"
echo "   • MailCatcher Web UI:  http://localhost:1080"
echo "   • PostgreSQL:          localhost:5433 (eduplatform/eduplatform_secret_2024)"
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "⚠️  Servisleri durdurmak için: Ctrl+C"
echo ""

# Tüm process'leri bekle
wait
