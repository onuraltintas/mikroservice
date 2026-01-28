#!/bin/bash

# Sadece Docker altyapısını başlat
echo "🐳 Docker altyapısı başlatılıyor..."
docker compose up -d

echo ""
echo "✅ Docker servisleri başlatıldı!"
echo ""
echo "📍 Çalışan Servisler:"
docker compose ps
