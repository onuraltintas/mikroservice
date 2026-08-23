#!/usr/bin/env bash
set -Eeuo pipefail

# Certbot invokes deploy hooks for every renewed lineage. Reload LiteSpeed only
# when the Eduİvme certificate changed; unrelated sites keep their own hooks.
if [[ "${RENEWED_LINEAGE:-}" != "/etc/letsencrypt/live/eduivme.com" ]]; then
  exit 0
fi

/usr/local/lsws/bin/lshttpd -t
/usr/local/lsws/bin/lswsctrl reload
