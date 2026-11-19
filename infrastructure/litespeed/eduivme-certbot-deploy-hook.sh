#!/usr/bin/env bash
set -Eeuo pipefail

# Certbot invokes deploy hooks for every renewed lineage. Reload LiteSpeed only
# when an Eduİvme certificate changed (eduivme.com, maxkoc.com since 18.11.2026, maxkampus.com since 19.11.2026);
# unrelated sites keep their own hooks.
case "${RENEWED_LINEAGE:-}" in
  /etc/letsencrypt/live/eduivme.com|/etc/letsencrypt/live/maxkoc.com|/etc/letsencrypt/live/maxkampus.com) ;;
  *) exit 0 ;;
esac

/usr/local/lsws/bin/lshttpd -t
/usr/local/lsws/bin/lswsctrl reload
