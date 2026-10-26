import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const root = new URL('../../../', import.meta.url);
const read = path => readFile(new URL(path, root), 'utf8');

// Every runtime image runs without root. Volumes created by earlier root images are
// handed to UID 1654 by one-shot *-volume-permissions services before the APIs start.
const apis = [
  { name: 'coaching', dockerfile: 'services/coaching-service/Dockerfile',
    volumes: ['coaching_data_protection', 'coaching_attachments', 'coaching_cms_media'] },
  { name: 'speed-reading', dockerfile: 'services/speed-reading-service/Dockerfile',
    volumes: ['speed_reading_data_protection', 'speed_reading_media'] }
];

function block(compose, service) {
  const start = compose.indexOf(`\n  ${service}:\n`);
  assert.notEqual(start, -1, `${service} is defined`);
  const next = compose.slice(start + 1).search(/\n  [a-z0-9-]+:\n/);
  return next === -1 ? compose.slice(start) : compose.slice(start, start + 1 + next);
}

for (const api of apis) {
  test(`${api.name} API image runs as UID 1654 and owns its volume mount points`, async () => {
    const dockerfile = await read(api.dockerfile);
    const final = dockerfile.slice(dockerfile.lastIndexOf('FROM '));
    assert.match(final, /^USER \$APP_UID$/m);
    assert.match(final, /chown -R "\$APP_UID" \/var\/lib\/eduplatform/);
    assert.ok(final.indexOf('USER $APP_UID') < final.indexOf('ENTRYPOINT'), 'USER precedes ENTRYPOINT');
  });

  test(`${api.name} volumes are handed to UID 1654 before the API starts`, async () => {
    const compose = await read('docker-compose.yml');
    const permissions = block(compose, `${api.name}-volume-permissions`);
    assert.match(permissions, /user: "0:0"/);
    assert.match(permissions, /entrypoint: \["chown", "-R", "1654:1654", "\/var\/lib\/eduplatform"\]/);
    assert.match(permissions, /network_mode: none/);
    for (const volume of api.volumes) assert.match(permissions, new RegExp(`- ${volume}:/var/lib/eduplatform/`));
    const service = block(compose, `${api.name}-service`);
    assert.match(service, new RegExp(`${api.name}-volume-permissions:\\n\\s+condition: service_completed_successfully`));
  });
}

test('production checks secret readability for the non-root APIs and runs every ownership fix', async () => {
  const production = await read('docker-compose.production.yml');
  for (const service of ['coaching-service', 'speed-reading-service'])
    assert.match(block(production, service), /runtime-secrets-preflight:\n\s+condition: service_completed_successfully/);
  assert.match(block(production, 'speed-reading-volume-permissions'), /profiles: !reset \[\]/);
  const staging = await read('docker-compose.staging.yml');
  assert.match(block(staging, 'coaching-volume-permissions'), /eduivme-staging\/coaching-service/);
  assert.match(block(staging, 'speed-reading-volume-permissions'), /profiles: !reset \[\]/);
});

test('static web images use unprivileged nginx on port 8080 behind the edges', async () => {
  const [srDockerfile, staffDockerfile, srNginx, staffNginx, srEdge, mainEdge, compose, production, staging] = await Promise.all([
    read('clients/speed-reading/Dockerfile'), read('clients/admin-panel/Dockerfile.staff-portal'),
    read('clients/speed-reading/nginx.conf'), read('clients/admin-panel/projects/staff-portal/nginx.conf'),
    read('infrastructure/caddy/Caddyfile.speed-reading.litespeed'), read('infrastructure/caddy/Caddyfile.production.litespeed'),
    read('docker-compose.yml'), read('docker-compose.production.yml'), read('docker-compose.staging.yml')
  ]);
  for (const dockerfile of [srDockerfile, staffDockerfile]) {
    const runtime = dockerfile.slice(dockerfile.lastIndexOf('FROM '));
    assert.match(runtime, /^FROM nginxinc\/nginx-unprivileged:[\d.]+-alpine AS runtime$/m);
    assert.doesNotMatch(runtime.slice(runtime.lastIndexOf('USER')), /^USER root$/m, 'image ends unprivileged');
    assert.match(runtime, /^EXPOSE 8080$/m);
    assert.match(runtime, /http:\/\/127\.0\.0\.1:8080\//);
  }
  for (const nginx of [srNginx, staffNginx]) assert.match(nginx, /^\s+listen 8080;$/m);
  assert.match(srEdge, /reverse_proxy speed-reading-frontend:8080\b/);
  assert.match(mainEdge, /reverse_proxy staff-portal:8080\b/);
  for (const file of [compose, production, staging])
    assert.match(block(file, 'speed-reading-frontend'), /expose:\n\s+- "8080"/);
  const staff = block(production, 'staff-portal');
  assert.match(staff, /expose:\n\s+- "8080"/);
  assert.match(staff, /http:\/\/127\.0\.0\.1:8080\/health\/live/);
});
