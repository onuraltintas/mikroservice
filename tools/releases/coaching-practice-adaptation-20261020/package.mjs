import { mkdirSync, cpSync, readdirSync, readFileSync, writeFileSync, statSync, existsSync } from 'node:fs';
import { resolve, join, relative } from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
const root = process.cwd(), release = resolve('artifacts/coaching-practice-adaptation-live-20261020');
const pack = join(release, 'package');
if (existsSync(pack)) throw new Error('Do not overwrite a sealed package.');
mkdirSync(join(pack, 'payload/coaching'), { recursive: true });
mkdirSync(join(pack, 'source/admin-panel'), { recursive: true });
for (const assembly of ['Coaching.API', 'Coaching.Application', 'Coaching.Domain', 'Coaching.Infrastructure'])
  for (const extension of ['dll', 'pdb']) cpSync(join(release, 'publish', `${assembly}.${extension}`), join(pack, 'payload/coaching', `${assembly}.${extension}`));
const client = resolve('clients/admin-panel');
for (const name of ['src', 'projects', 'shared', 'public', 'angular.json', 'package.json', 'package-lock.json', 'tsconfig.json', 'tsconfig.app.json', 'tsconfig.spec.json', '.postcssrc.json'])
  if (existsSync(join(client, name))) cpSync(join(client, name), join(pack, 'source/admin-panel', name), { recursive: true, filter: p => !p.split(/[\\/]/).some(n => n === '.env' || n.startsWith('.env.') || ['node_modules','dist','.angular'].includes(n)) });
cpSync(resolve('clients/shared'), join(pack, 'source/shared'), { recursive: true });
for (const name of ['deploy.sh', 'build-portals.cjs']) cpSync(resolve('tools/releases/coaching-practice-adaptation-20261020', name), join(pack, name));
writeFileSync(join(pack, 'coaching-service.Dockerfile'), 'ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nARG SOURCE_REVISION\nCOPY --chmod=644 payload/coaching/ /app/\nLABEL org.opencontainers.image.revision=$SOURCE_REVISION\n');
writeFileSync(join(pack, 'admin-panel.Dockerfile'), 'ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nARG SOURCE_REVISION\nUSER root\nRUN rm -f /app/dist/admin-panel/browser/index.html /app/dist/admin-panel/browser/index.html.gz /app/dist/admin-panel/browser/index.html.br\nCOPY payload/admin/ /app/dist/admin-panel/\nRUN chown -R node:node /app/dist/admin-panel && chmod -R u=rwX,go=rX /app/dist/admin-panel\nUSER node\nLABEL org.opencontainers.image.revision=$SOURCE_REVISION\n');
writeFileSync(join(pack, 'staff-portal.Dockerfile'), 'ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nARG SOURCE_REVISION\nUSER root\nRUN rm -f /usr/share/nginx/html/index.html /usr/share/nginx/html/index.html.gz /usr/share/nginx/html/index.html.br\nCOPY payload/staff/ /usr/share/nginx/html/\nRUN chmod -R u=rwX,go=rX /usr/share/nginx/html\nLABEL org.opencontainers.image.revision=$SOURCE_REVISION\n');
const files = p => readdirSync(p).flatMap(n => statSync(join(p,n)).isDirectory() ? files(join(p,n)) : [join(p,n)]);
const hash = p => createHash('sha256').update(readFileSync(p)).digest('hex');
const payload = files(pack);
if (payload.some(p => /(^|[\\/])\.env([.\\/]|$)/.test(p))) throw new Error('Secret configuration must not be packaged.');
writeFileSync(join(pack, 'payload.sha256'), payload.map(p => `${hash(p)}  ${relative(pack,p).replaceAll('\\','/')}`).join('\n')+'\n');
execFileSync('tar', ['-czf', join(release,'release.tar.gz'), '-C', pack, '.']);
console.log(JSON.stringify({ archive:join(release,'release.tar.gz'), files:payload.length, sha256:hash(join(release,'release.tar.gz')) }));
