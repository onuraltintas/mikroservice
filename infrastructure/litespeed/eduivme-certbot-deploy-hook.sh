#!/usr/bin/env bash
set -Eeuo pipefail

# Certbot invokes deploy hooks for every renewed lineage. Reload LiteSpeed only
# when an Eduİvme certificate changed (eduivme.com, or maxkoc.com since the alan adı planı 18.11.2026);
# unrelated sites keep their own hooks.
case "${RENEWED_LINEAGE:-}" in
  /etc/letsencrypt/live/eduivme.com|/etc/letsencrypt/live/maxkoc.com) ;;
  *) exit 0 ;;
esac

/usr/local/lsws/bin/lshttpd -t
/usr/local/lsws/bin/lswsctrl reload
