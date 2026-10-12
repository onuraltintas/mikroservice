import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const dir = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(dir, '../../..');
const release = path.join(root, 'artifacts/curriculum-v3-20261012');
const read = name => fs.readFileSync(path.join(dir, name), 'utf8').replace(/\r\n/g, '\n');
const body = sql => sql.replace(/^(BEGIN;|COMMIT;)\r?$/gm, '');
fs.mkdirSync(release, { recursive: true });
fs.writeFileSync(path.join(release, 'apply-all.sql'), 'BEGIN;\n' + ['short-content.sql', 'apply.sql', 'repair-unstarted-assessment-slots.sql'].map(name => body(read(name))).join('\n') + '\nCOMMIT;\n');
for (const name of ['inventory-protected.sql', 'inventory-all.sql', 'verify.sql']) fs.writeFileSync(path.join(release, name), read(name));
fs.writeFileSync(path.join(release, 'rollback.sql'), 'BEGIN;\n' + ['rollback.sql', 'rollback-short-content.sql'].map(name => body(read(name))).join('\n') + '\nCOMMIT;\n');
fs.writeFileSync(path.join(release, 'deploy.sh'), fs.readFileSync(path.join(root, 'infrastructure/scripts/deploy-speed-reading-curriculum-v3.sh'), 'utf8').replace(/\r\n/g, '\n'));
fs.writeFileSync(path.join(release, 'api.Dockerfile'), `FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG SOURCE_REVISION
WORKDIR /app
COPY api/ ./
LABEL org.opencontainers.image.revision="$SOURCE_REVISION"
ENTRYPOINT ["dotnet", "SpeedReading.API.dll"]
`);
fs.writeFileSync(path.join(release, 'web.Dockerfile'), `FROM nginx:1.27.5-alpine
ARG SOURCE_REVISION
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY previous/browser/ /usr/share/nginx/html/
COPY web/browser/ /usr/share/nginx/html/
RUN printf '\\n// curriculum-v3-20261012: %s\\n' "$SOURCE_REVISION" >> /usr/share/nginx/html/ngsw-worker.js
LABEL org.opencontainers.image.revision="$SOURCE_REVISION"
EXPOSE 80
HEALTHCHECK --interval=10s --timeout=5s --retries=6 CMD wget --spider --no-verbose http://127.0.0.1/ || exit 1
`);
fs.copyFileSync(path.join(root, 'clients/speed-reading/nginx.conf'), path.join(release, 'nginx.conf'));
fs.writeFileSync(path.join(release, 'production.override.yml'), `services:
  speed-reading-service:
    image: eduivme/speed-reading-service:curriculum-v3-20261012
    pull_policy: never
  speed-reading-frontend:
    image: eduivme/speed-reading-frontend:curriculum-v3-20261012
    pull_policy: never
`);
fs.writeFileSync(path.join(release, 'source-revision.txt'), execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }));
const files = [];
function walk(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (entry.name === 'tests' || entry.name === 'payload.sha256') continue;
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) walk(file);
    else files.push(file);
  }
}
walk(release);
fs.writeFileSync(path.join(release, 'payload.sha256'), files.sort().map(file => `${crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')}  ${path.relative(release, file).replaceAll('\\', '/')}`).join('\n') + '\n');
console.log(`Sealed ${files.length} files; no credentials or user data packaged.`);
