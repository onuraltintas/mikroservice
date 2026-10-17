set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-calendar-move-20261017-r2
html=$(curl -fsS --max-time 10 https://onuraltintas.net/staff/)
[[ "$html" == *'<base href="/staff/">'* ]]
main=$(printf '%s' "$html" | sed 's/></>\n</g' | sed -n 's/.*<script src="\([^"]*\)".*/\1/p' | tail -1)
style=$(printf '%s' "$html" | sed 's/></>\n</g' | sed -n 's/.*href="\([^" ]*\.css\)".*/\1/p' | head -1)
[[ "$main" =~ ^main-[A-Z0-9]+\.js$ && "$style" =~ ^styles-[A-Z0-9]+\.css$ ]]
for file in "$main" "$style"; do
 result=$(curl -fsS --max-time 10 -o /dev/null -w '%{http_code} %{content_type}' "https://onuraltintas.net/staff/$file")
 printf '%s %s\n' "$result" "$file"
 [[ "$result" == '200 '* ]]
 if [[ "$file" == *.css ]]; then [[ "$result" == *text/css* ]]; else [[ "$result" == *javascript* ]]; fi
done > public-assets.txt
for origin in https://eduivme.com https://onuraltintas.net; do
 status=$(curl -sS --max-time 10 -o /dev/null -w '%{http_code}' "$origin/api/coaching/teachers/me/study-overview?fromDate=2026-10-01&toDate=2026-10-31")
 printf 'anonymous-overview %s %s\n' "$origin" "$status"; [[ "$status" == 401 ]]
done
date -u +%FT%TZ > public-assets-verified.txt
cat public-assets.txt
