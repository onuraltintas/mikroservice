set -euo pipefail
cd /var/lib/eduivme/releases/coaching-development-20261020
for entry in 'https://eduivme.com/coaching-portal/progress' 'https://onuraltintas.net/staff/' 'https://eduivme.com/staff/'; do
 file=$(mktemp)
 curl -fsSL --max-time 20 "$entry" > "$file"
 python3 - "$entry" "$file" <<'PY' > assets-to-check.txt
import sys
from html.parser import HTMLParser
from urllib.parse import urljoin
class Entries(HTMLParser):
 def __init__(self): super().__init__(); self.base=sys.argv[1]; self.assets=[]
 def handle_starttag(self,tag,attrs):
  attrs=dict(attrs)
  if tag=='base' and attrs.get('href'): self.base=urljoin(self.base,attrs['href'])
  if tag=='script' and attrs.get('src'): self.assets.append(attrs['src'])
  if tag=='link' and attrs.get('rel')=='stylesheet' and not attrs.get('href','').startswith('http'): self.assets.append(attrs['href'])
p=Entries();p.feed(open(sys.argv[2]).read());assert p.assets
for asset in p.assets: print(urljoin(p.base,asset))
PY
 rm -f "$file"
 while IFS= read -r asset; do
  result=$(curl -fsS --max-time 20 -o /dev/null -w '%{http_code} %{content_type}' "$asset")
  [[ "$result" == 200* ]]
  if [[ "$asset" == *.js ]]; then [[ "$result" == *javascript* ]]; fi
  if [[ "$asset" == *.css ]]; then [[ "$result" == *text/css* ]]; fi
  printf '%s %s\n' "$result" "$asset"
 done < assets-to-check.txt
done
