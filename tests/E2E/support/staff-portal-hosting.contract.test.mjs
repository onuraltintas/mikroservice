import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const root = new URL('../../../', import.meta.url);
const read = path => readFile(new URL(path, root), 'utf8');

test('staff portal is built and served as a separate production web service', async () => {
  const [compose, edge, dockerfile, nginx, appTemplate] = await Promise.all([
    read('docker-compose.production.yml'),
    read('infrastructure/caddy/Caddyfile.production.litespeed'),
    read('clients/admin-panel/Dockerfile.staff-portal'),
    read('clients/admin-panel/projects/staff-portal/nginx.conf'),
    read('clients/admin-panel/projects/staff-portal/src/app/app.component.html')
  ]);

  assert.match(compose, /^  staff-portal:\s*$/m);
  assert.match(compose, /\/staff-portal:\$\{RELEASE_TAG/);
  assert.match(edge, /handle_path \/staff\/\*/);
  assert.match(edge, /reverse_proxy staff-portal:80/);
  assert.match(dockerfile, /--project staff-portal --configuration production --base-href \/staff\//);
  assert.match(dockerfile, /dist\/staff-portal\/browser/);
  assert.match(nginx, /try_files \$uri \$uri\/ \/index\.html/);
  assert.match(appTemplate, /href="\/staff\/"/);
  assert.doesNotMatch(appTemplate, /href="\/"/);
});

test('old Coaching and Speed Reading staff URLs hand off to the unified portal', async () => {
  const [coachingEdge, speedReadingEdge] = await Promise.all([
    read('infrastructure/caddy/Caddyfile.production.litespeed'),
    read('infrastructure/caddy/Caddyfile.speed-reading.litespeed')
  ]);

  assert.equal(
    [...coachingEdge.matchAll(/redir @legacyCoachingStaff https:\/\/onuraltintas\.net\/staff\/\?product=coaching 302/g)].length,
    2
  );
  assert.match(speedReadingEdge, /onuraltintas\.net\/staff\/\?product=speed-reading/);
});
