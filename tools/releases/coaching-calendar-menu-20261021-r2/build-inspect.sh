set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
ps -eo pid,etime,pcpu,pmem,args | grep -E 'ng.js build|build-portals|esbuild' | grep -v grep | head -n 6
find source/admin-panel/dist -maxdepth 2 -type f -name 'index*' -printf '%TY-%Tm-%Td %TT %p\n' | head -n 5
du -sh source/admin-panel/node_modules
