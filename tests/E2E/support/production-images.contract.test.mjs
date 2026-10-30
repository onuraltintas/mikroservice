import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// Decision 30.10.2026: production images are built by GitHub Actions and pushed to GHCR; the server only pulls them.
// The image workflow must build exactly the images the production stack runs, from the same Dockerfiles.
const root = new URL('../../../', import.meta.url);
const read = file => readFileSync(new URL(file, root), 'utf8');
const workflow = read('.github/workflows/production-images.yml');
const pullScript = read('infrastructure/scripts/pull-release-images.sh');

// Every compose service whose image is ${PRODUCTION_IMAGE_REPOSITORY}/<name> and that has a build section.
function composeBuilds(...files) {
  const builds = new Map();
  for (const file of files) {
    const blocks = read(file).split(/\n(?=  [a-z0-9-]+:\s*\n)/);
    for (const block of blocks) {
      const image = /image:\s*\$\{PRODUCTION_IMAGE_REPOSITORY[^}]*\}\/([a-z0-9-]+):/.exec(block);
      const context = /\n\s+build:\s*\n\s+context:\s*(\S+)\s*\n\s+dockerfile:\s*(\S+)/.exec(block);
      if (image && context) builds.set(image[1], { context: context[1].replace(/^\.\/?$/, '.'), dockerfile: context[2] });
    }
  }
  return builds;
}

function workflowImages() {
  return [...workflow.matchAll(/-\s*\{\s*name:\s*([a-z0-9-]+),\s*context:\s*(\S+),\s*dockerfile:\s*([^\s,}]+)/g)]
    .map(match => ({ name: match[1], context: match[2].replace(/^\.\/?$/, '.'), dockerfile: match[3] }));
}

test('the image workflow builds every production image from the compose Dockerfile and context', () => {
  const compose = composeBuilds('docker-compose.yml', 'docker-compose.production.yml');
  const images = workflowImages();
  assert.deepEqual(images.map(image => image.name).sort(), [
    'admin-panel', 'api-gateway', 'coaching-service', 'identity-service', 'notification-service',
    'speed-reading-frontend', 'speed-reading-service', 'staff-portal']);
  for (const image of images) {
    assert.ok(compose.has(image.name), `${image.name} is not built by the production compose files`);
    assert.deepEqual({ context: image.context, dockerfile: image.dockerfile }, compose.get(image.name), image.name);
  }
});

test('images are built only for green CI runs on main and tagged with the 7-character commit id', () => {
  assert.match(workflow, /workflow_run:\s*\n\s*workflows:\s*\[\s*"EduPlatform CI"\s*\]/);
  assert.match(workflow, /branches:\s*\[\s*main\s*\]/);
  assert.match(workflow, /conclusion == 'success'/);
  assert.match(workflow, /packages:\s*write/);
  assert.match(workflow, /ghcr\.io/);
  assert.match(workflow, /cut -c1-7|::7\}|substring\(.*7\)/);
  assert.match(workflow, /RELEASE_TAG=/, 'the speed reading frontend needs its RELEASE_TAG build argument');
});

test('the server pull script fetches the same images, never builds and refuses an incomplete set', () => {
  for (const image of workflowImages()) assert.match(pullScript, new RegExp(`\\b${image.name}\\b`), image.name);
  assert.match(pullScript, /docker compose[^\n]*pull/);
  assert.doesNotMatch(pullScript, /\bbuild\b/);
  assert.match(pullScript, /set -eu/);
});
