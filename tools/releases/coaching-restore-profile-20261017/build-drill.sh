set -euo pipefail
cd /var/lib/eduivme/releases/coaching-restore-profile-20261017
[[ $(sha256sum release.tar.gz | cut -d' ' -f1) == 77ca92b275276500083b22f0375e7e6999afdbb22b0ca3fdfae3a77be2e4a71d ]]
tar -xzf release.tar.gz
bash deploy.sh prepare
bash deploy.sh build
bash deploy.sh drill
