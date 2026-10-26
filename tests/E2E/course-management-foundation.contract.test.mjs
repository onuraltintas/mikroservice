import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../../', import.meta.url));
const read = path => readFileSync(resolve(root, path), 'utf8');
const baseline = JSON.parse(read('tests/E2E/support/course-management-protected-baseline.json'));
const protectedProductRoots = [
  'services/coaching-service',
  'services/speed-reading-service',
  'clients/speed-reading',
  'clients/admin-panel/projects/staff-portal/src/app/features/coaching',
  'clients/admin-panel/projects/staff-portal/src/app/features/speed-reading',
];
const generatedArtifactDirectories = new Set(['.codex-staging-tmp', '.tmp']);
const isGeneratedArtifactDirectory = segment => generatedArtifactDirectories.has(segment)
  || /^\.[a-z0-9-]+-release-build-\d{8}(?:-r\d+)?$/i.test(segment);
function listProtectedProductPaths(repositoryRoot, roots) {
  const gitPaths = args => execFileSync('git', args, {
    cwd: repositoryRoot,
    encoding: 'utf8',
  }).split('\0').filter(Boolean);
  const untrackedPaths = gitPaths(['ls-files', '-z', '--others', '--exclude-standard', '--', ...roots])
    .filter(path => !path.split('/').slice(0, -1).some(isGeneratedArtifactDirectory));
  return [...new Set([
    ...gitPaths(['ls-files', '-z', '--', ...roots]),
    ...untrackedPaths,
  ])].filter(path => existsSync(resolve(repositoryRoot, path))).sort();
}
function protectedContentHash(content) {
  const canonical = content.includes(0) ? content : Buffer.from(content.toString('utf8').replace(/\r\n?/g, '\n'));
  return createHash('sha256').update(canonical).digest('hex');
}
const protectedFileHash = path => protectedContentHash(readFileSync(resolve(root, path)));

test('protected-file hashes normalize text line endings and preserve binary bytes', () => {
  assert.equal(protectedContentHash(Buffer.from('first\r\nsecond\r\n')),
    protectedContentHash(Buffer.from('first\nsecond\n')));
  assert.notEqual(protectedContentHash(Buffer.from([0, 13, 10])), protectedContentHash(Buffer.from([0, 10])));
});

test('CI migration artifact guard configures the EF factory and targets the latest migration', () => {
  const migrationDirectory = resolve(root, 'services/course-management-service/CourseManagement.Infrastructure/Migrations');
  const latestMigration = readdirSync(migrationDirectory)
    .filter(name => /^\d+_.+\.cs$/.test(name) && !name.endsWith('.Designer.cs'))
    .sort()
    .at(-1)
    ?.replace(/\.cs$/, '');
  assert.ok(latestMigration, 'The Course Management migration directory must include a migration.');

  const workflow = read('.github/workflows/ci.yml');
  const smokeJobStart = workflow.indexOf('\n  course-management-postgres-smoke:');
  const artifactStepStart = workflow.indexOf('      - name: Generate idempotent Course Management migration SQL', smokeJobStart);
  assert.ok(smokeJobStart >= 0 && artifactStepStart > smokeJobStart,
    'The migration artifact step must remain in the PostgreSQL smoke job.');
  assert.match(workflow.slice(smokeJobStart, artifactStepStart),
    /^    env:\r?\n      COURSE_MANAGEMENT_CONNECTION_STRING:\s*\S+/m,
    'The EF design-time factory requires a connection string in the smoke job environment.');
  assert.ok(workflow.includes(`grep -q '${latestMigration}' artifacts/course-management/course-management-idempotent.sql`));
});

test('CI retains the Course Management frontend, unit, and PostgreSQL acceptance gates', () => {
  const workflow = read('.github/workflows/ci.yml');
  const sourceSafetyStart = workflow.indexOf('\n  source-safety:');
  const frontendStart = workflow.indexOf('\n  frontend:', sourceSafetyStart);
  assert.ok(sourceSafetyStart >= 0 && frontendStart > sourceSafetyStart,
    'Protected-product contract checks must remain in the source-safety job.');
  const sourceSafetyJob = workflow.slice(sourceSafetyStart, frontendStart);
  assert.match(sourceSafetyJob, /node --test tests\/E2E\/course-management-foundation\.contract\.test\.mjs/,
    'The contract test must run in CI to guard the other required gates.');

  const speedReadingFrontendStart = workflow.indexOf('\n  speed-reading-frontend:', frontendStart);
  assert.ok(frontendStart >= 0 && speedReadingFrontendStart > frontendStart,
    'Course Management frontend checks must remain in the frontend job.');
  const frontendJob = workflow.slice(frontendStart, speedReadingFrontendStart);
  assert.match(frontendJob, /Build the separate Course Management student application/);
  assert.match(frontendJob, /npm run build --prefix \.\.\/course-management -- --configuration production/);
  assert.match(frontendJob, /node --test \.\.\/\.\.\/tests\/E2E\/course-management-panels\.smoke\.test\.mjs/);

  const unitJobStart = workflow.indexOf('\n  tests:');
  const postgresJobStart = workflow.indexOf('\n  course-management-postgres-smoke:', unitJobStart);
  assert.ok(unitJobStart >= 0 && postgresJobStart > unitJobStart,
    'Course Management unit tests must remain in the integration-tests job.');
  const unitJob = workflow.slice(unitJobStart, postgresJobStart);
  assert.match(unitJob, /dotnet test tests\/Unit\/CourseManagement\.Application\.UnitTests\/CourseManagement\.Application\.UnitTests\.csproj/);

  const nextJobStart = workflow.indexOf('\n  privacy-erasure-e2e:', postgresJobStart);
  assert.ok(postgresJobStart >= 0 && nextJobStart > postgresJobStart,
    'The PostgreSQL smoke job must remain a separate CI job.');
  const postgresJob = workflow.slice(postgresJobStart, nextJobStart);
  assert.match(postgresJob, /Verify isolated PostgreSQL migrations, API, backup and restore/);
  assert.match(postgresJob, /run: \.\/tests\/E2E\/course-management-migration\.smoke\.ps1/);
  assert.match(postgresJob, /Verify pinned Course Management ClamAV upload scanning/);
  assert.match(postgresJob, /run: \.\/tests\/E2E\/course-management-clamav\.smoke\.ps1/);
  assert.match(read('tests/E2E/course-management-clamav.smoke.ps1'), /clamav\/clamav:1\.5\.4/,
    'CI must scan uploads with the same pinned image used in the production overlay.');
  assert.match(postgresJob, /Verify Course Management OpenTelemetry delivery/);
  assert.match(postgresJob, /run: \.\/tests\/E2E\/course-management-otel\.smoke\.ps1/);
  assert.match(read('tests/E2E/course-management-otel.smoke.ps1'), /monitoring\/otel\/otel-collector\.yml/);
  assert.match(read('tests/E2E/course-management-otel.smoke.ps1'), /monitoring\/tempo\/tempo\.yml/);
  assert.match(read('tests/E2E/course-management-migration.smoke.ps1'),
    /course-management-subscription-access\.integration\.test\.mjs/,
    'The PostgreSQL smoke must exercise subscription read-only access and reactivation.');

  const containerBuildStart = workflow.indexOf('\n  container-build:');
  assert.ok(containerBuildStart >= 0, 'Course Management container image must be built in CI.');
  assert.match(workflow.slice(containerBuildStart, containerBuildStart + 500),
    /needs:\s*\[dotnet,\s*tests,\s*course-management-postgres-smoke,[^\]]*frontend/,
    'Container builds must wait for Course Management build, unit, browser and PostgreSQL gates.');
  assert.match(workflow,
    /- name: course-management\s+context: \.\s+dockerfile: services\/course-management-service\/Dockerfile/,
    'The gated container matrix must include the Course Management API image.');
});

test('CI distributed privacy erasure validates the Identity review summary used before Course Management deletion', () => {
  const workflow = read('.github/workflows/ci.yml');
  const jobStart = workflow.indexOf('\n  privacy-erasure-e2e:');
  const nextJob = workflow.indexOf('\n  migration-drift:', jobStart);
  assert.ok(jobStart >= 0 && nextJob > jobStart, 'The distributed privacy job must remain present.');
  const job = workflow.slice(jobStart, nextJob);
  assert.match(job, /E2E_COURSE_MANAGEMENT_ENABLED:\s*'true'/);
  assert.match(job, /docker-compose\.course-management\.yml/);
  assert.match(job, /course-management-service --migrate-only/);
  assert.match(job, /node --test .*privacy-erasure\.docker\.test\.mjs/);

  const e2e = read('tests/E2E/privacy-erasure.docker.test.mjs');
  assert.match(e2e, /Account assessment uses the complete Identity review summary for hold decisions/);
  assert.match(e2e, /all four Account-scope assessment results/);
  assert.match(e2e, /LegalHoldStatusVerified"=true/);

  const identityController = read('services/identity-service/Identity.API/Controllers/InternalDataSubjectRequestsController.cs');
  assert.match(identityController, /erasure-readiness/);
  assert.match(identityController, /InternalServiceKey/);
  const readinessProvider = read('services/course-management-service/CourseManagement.Infrastructure/Privacy/IdentityCourseManagementErasureReadinessProvider.cs');
  assert.match(readinessProvider, /HasBlockingLegalHold/);
  assert.match(readinessProvider, /IsReadyForErasure/);
  assert.match(readinessProvider, /CourseManagementLegalHoldStatus.Unknown/);
  const developmentOverlay = read('docker-compose.course-management.yml');
  const productionOverlay = read('docker-compose.course-management.production.yml');
  for (const overlay of [developmentOverlay, productionOverlay]) {
    assert.match(overlay, /INTERNAL_SERVICE_API_KEY/);
    assert.match(overlay, /Services__IdentityService:\s*http:\/\/identity-service:8080/);
  }

  const messagingTest = read('tests/Integration/CourseManagement.IntegrationTests/CourseManagementErasureRabbitMqPostgresTests.cs');
  assert.match(messagingTest, /assessmentCompletion\.EventId\.Should\(\)\.NotBe\(requestId\)/);
  assert.match(messagingTest, /executionCompletion\.EventId\.Should\(\)\.NotBe\(requestId\)/);
});

test('protected product file inventory has no additions or removals', () => {
  const currentPaths = listProtectedProductPaths(root, protectedProductRoots);
  const baselinePaths = Object.keys(baseline.files)
    .filter(path => protectedProductRoots.some(rootPath => path.startsWith(`${rootPath}/`)))
    .sort();

  const baselinePathSet = new Set(baselinePaths);
  const currentPathSet = new Set(currentPaths);
  assert.deepEqual({
    added: currentPaths.filter(path => !baselinePathSet.has(path)),
    removed: baselinePaths.filter(path => !currentPathSet.has(path)),
  }, { added: [], removed: [] },
  'Existing Coaching and Speed Reading product paths are frozen; do not add or remove files.');
});

test('protected product file inventory includes untracked files', () => {
  const temporaryRoot = mkdtempSync(join(root, '.course-protection-'));
  try {
    execFileSync('git', ['init', '--quiet'], { cwd: temporaryRoot });
    const protectedDirectory = join(temporaryRoot, 'clients', 'speed-reading');
    mkdirSync(protectedDirectory, { recursive: true });
    writeFileSync(join(protectedDirectory, 'tracked.ts'), 'export {};\r\n');
    execFileSync('git', ['add', '--', 'clients/speed-reading/tracked.ts'], { cwd: temporaryRoot });
    writeFileSync(join(protectedDirectory, 'untracked.ts'), 'export {};\n');
    for (const generatedDirectory of [
      '.codex-staging-tmp',
      '.tmp',
      '.auth-timeout-release-build-20261013-r1',
    ]) {
      const generatedPath = join(protectedDirectory, generatedDirectory);
      mkdirSync(generatedPath);
      writeFileSync(join(generatedPath, 'cache-file'), 'generated');
    }

    const untrackedFixturePaths = execFileSync('git', ['ls-files', '--others', '--exclude-standard', '--',
      'clients/speed-reading'], { cwd: temporaryRoot, encoding: 'utf8' }).split(/\r?\n/).filter(Boolean).sort();
    assert.deepEqual(untrackedFixturePaths, [
      'clients/speed-reading/.auth-timeout-release-build-20261013-r1/cache-file',
      'clients/speed-reading/.codex-staging-tmp/cache-file',
      'clients/speed-reading/.tmp/cache-file',
      'clients/speed-reading/untracked.ts',
    ]);
    assert.deepEqual(listProtectedProductPaths(temporaryRoot, ['clients/speed-reading']), [
      'clients/speed-reading/tracked.ts',
      'clients/speed-reading/untracked.ts',
    ]);
    rmSync(join(protectedDirectory, 'tracked.ts'));
    assert.deepEqual(listProtectedProductPaths(temporaryRoot, ['clients/speed-reading']), [
      'clients/speed-reading/untracked.ts',
    ], 'Deleted tracked files must be reported as removals, not hashed as existing files.');
    writeFileSync(join(protectedDirectory, 'özel.ts'), 'export {};\n');
    assert.deepEqual(listProtectedProductPaths(temporaryRoot, ['clients/speed-reading']), [
      'clients/speed-reading/untracked.ts',
      'clients/speed-reading/özel.ts',
    ], 'Unicode filenames must not disappear through Git path quoting.');
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true });
  }
});

test('existing product implementation files remain unchanged apart from line endings', () => {
  for (const [path, hash] of Object.entries(baseline.files)) {
    const actual = protectedFileHash(path);
    assert.equal(actual, hash, `Protected product file changed: ${path}`);
  }
});

test('existing gateway routes and destinations retain their configuration', () => {
  const proxy = JSON.parse(read('services/api-gateway/appsettings.json')).ReverseProxy;
  for (const [name, route] of Object.entries(baseline.routes)) assert.deepEqual(proxy.Routes[name], route);
  for (const [name, cluster] of Object.entries(baseline.clusters)) assert.deepEqual(proxy.Clusters[name], cluster);
});

test('new product has a separate gateway destination and namespaced API route', () => {
  const proxy = JSON.parse(read('services/api-gateway/appsettings.json')).ReverseProxy;
  assert.equal(proxy.Routes['course-management-route']?.Match.Path, '/api/course-management/{**catch-all}');
  assert.equal(proxy.Routes['course-management-route']?.ClusterId, 'course-management-cluster');
  assert.equal(proxy.Clusters['course-management-cluster']?.Destinations.destination1.Address, 'http://localhost:5005');
  assert.equal(proxy.Routes['course-management-notifications-route']?.Match.Path, '/api/course-management/notifications/{**catch-all}');
  assert.equal(proxy.Routes['course-management-notifications-route']?.ClusterId, 'notification-cluster');
  assert.equal(proxy.Routes['course-management-notifications-root-route']?.Match.Path, '/api/course-management/notifications');
});

test('student application is separate and does not import another product', () => {
  const config = JSON.parse(read('clients/course-management/angular.json'));
  assert.ok(config.projects['course-management-student']);
  const source = read('clients/course-management/src/app/app.component.ts');
  assert.doesNotMatch(source, /admin-panel|speed-reading|coaching-portal/);
});

test('Course Management production overlay owns a pinned malware scanner', () => {
  const overlay = read('docker-compose.course-management.production.yml');
  assert.match(overlay, /course-management-clamav:\r?\n\s+image: clamav\/clamav:1\.5\.4/,
    'Course Management must use its own security-patched scanner image.');
  assert.match(overlay, /CourseManagement__AssignmentAttachments__ClamAvHost: course-management-clamav/,
    'The Course Management API must use its dedicated scanner.');
  assert.match(overlay, /course-management-clamav:\r?\n\s+condition: service_healthy/,
    'The Course Management API must wait for its dedicated scanner to become healthy.');
  assert.match(overlay, /course-management-clamav:[\s\S]*?restart: unless-stopped/,
    'The dedicated production scanner must restart after a container failure.');
  assert.doesNotMatch(overlay, /\n\s+clamav:\r?\n\s+condition:/,
    'The Course Management overlay must not depend on the shared scanner used by other products.');
});

test('Course Management attachment cleanup persistence failures trigger an immediate critical alert', () => {
  const rules = read('monitoring/prometheus/rules/eduplatform.yml');
  const alertMarker = '      - alert: CourseManagementAttachmentCleanupQueuePersistenceFailed';
  const alertStart = rules.indexOf(alertMarker);
  assert.ok(alertStart >= 0, 'The cleanup queue persistence failure must have a Prometheus alert.');
  const nextAlertStart = rules.indexOf('\n      - alert:', alertStart + alertMarker.length);
  const alert = rules.slice(alertStart, nextAlertStart < 0 ? rules.length : nextAlertStart);
  assert.match(alert, /^[ \t]*expr:\s*course_management_attachment_cleanup_queue_failures_total\s*>\s*0\s*$/m);
  assert.match(alert, /^[ \t]*severity:\s*critical\s*$/m);
  assert.doesNotMatch(alert, /^[ \t]*for:/m, 'No pending duration may delay this orphan-risk alert.');
  assert.match(read('.github/workflows/ci.yml'),
    /prom\/prometheus:v3\.5\.0 test rules \/etc\/prometheus\/tests\/course-management-alerts\.test\.yml/,
    'CI must exercise the alert firing behavior with Prometheus rule tests.');
  const telemetrySmoke = read('tests/E2E/course-management-otel.smoke.ps1');
  assert.match(telemetrySmoke, /prom\/prometheus:v3\.5\.0/,
    'The telemetry smoke must run the pinned Prometheus server, not only its scrape exporter.');
  assert.match(telemetrySmoke, /api\/v1\/query/,
    'The telemetry smoke must query metrics after Prometheus scrapes them.');
  assert.match(telemetrySmoke, /up\{job="eduplatform-otel"\}/,
    'The telemetry smoke must verify the real Prometheus scrape is healthy.');
  assert.match(telemetrySmoke, /count\(\{service_name="EduPlatform\.CourseManagement"\}\)/,
    'The telemetry smoke must query Course Management series from Prometheus itself.');
});
