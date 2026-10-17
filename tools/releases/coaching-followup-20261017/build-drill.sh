set -euo pipefail
cd /var/lib/eduivme/releases/coaching-followup-20261017
[[ $(sha256sum release.tar.gz | cut -d' ' -f1) == f221c85033ef0c83f873c8cd55987b6c2089326594608bd6f6bd3091b78273bf ]]
tar -xzf release.tar.gz
bash deploy.sh prepare
bash deploy.sh build
bash deploy.sh drill
