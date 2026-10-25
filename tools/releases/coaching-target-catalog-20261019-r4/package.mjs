import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { join, relative, resolve } from 'node:path';
import assert from 'node:assert/strict';

const root = process.cwd();
const release = resolve(root, 'artifacts/coaching-target-catalog-20261019-r4');
const pack = join(release, 'package');
assert.ok(!existsSync(pack), 'A sealed release package must not be overwritten.');

const fullCatalogPath = resolve(root, 'data/target-catalog/2026/yks-results.json');
const ydtPath = resolve(root, 'data/target-catalog/2026/yks-reviewed-20261019/ydt-all-languages.json');
const mebiPath = resolve(root, 'data/target-catalog/2026/mebi-yks-program-catalog-verification.json');
const fullCatalog = JSON.parse(readFileSync(fullCatalogPath, 'utf8'));
const ydt = JSON.parse(readFileSync(ydtPath, 'utf8'));
const mebi = JSON.parse(readFileSync(mebiPath, 'utf8'));
assert.equal(fullCatalog.year, 2026);
assert.equal(fullCatalog.rows.length, 21493);
assert.equal(fullCatalog.documentHash, 'c2be63ffdc5ed113a31035c05ae5389b9987ddce07edb4f858cb7360ad730832');
assert.equal(fullCatalog.sourceUrl, 'https://yokatlas.yok.gov.tr/api/tercih-kilavuz/search');
assert.equal(new Set(fullCatalog.rows.map(row => row.externalCode)).size, 21493);
const scoreCounts = fullCatalog.rows.reduce((counts, row) => {
  const scoreType = ({ DIL: 'YDT', 'DİL': 'YDT', 'SÖZ': 'SOZ' })[row.scoreType] ?? row.scoreType;
  counts[scoreType] = (counts[scoreType] ?? 0) + 1;
  return counts;
}, {});
assert.deepEqual(scoreCounts, { TYT: 9254, SAY: 5744, EA: 3900, SOZ: 1914, YDT: 681 });
assert.equal(ydt.year, 2026);
assert.equal(ydt.stage, 'Main');
assert.equal(ydt.rows.length, 681);
assert.equal(ydt.documentHash, 'c907978c90fbe975dd5025332cd8464207a4e4ad9dc27f25a251dc55e5baeabc');
assert.equal(new Set(ydt.rows.map(row => row.externalCode)).size, 681);
assert.ok(ydt.rows.every(row => row.scoreType === 'YDT'));
assert.equal(ydt.sourceUrl, 'https://www.osym.gov.tr/2026-yks-yerlestirme-sonuclarina-iliskin-sayisal-bilgiler');
assert.equal(mebi.totalCount, 21493);
assert.equal(mebi.uniqueProgramCodes, 21493);
assert.equal(mebi.programCodeSha256, '79c39840cb9ec50053a5f5254630cfad4dce4e9120433c99d9010243a543f836');
assert.equal(mebi.identitySha256, 'd276f32bc09a47783535f7afcb026666ab2bf9094b757b64cf36d43c20c4995e');
assert.equal(mebi.identityComparison.matchedIdentityFields, 21493);
assert.deepEqual(mebi.scoreTypeCounts, { 'DİL': 681, EA: 3900, SAY: 5744, 'SÖZ': 1914, TYT: 9254 });
assert.deepEqual(mebi.programTypeCounts, { 'LİSANS': 12239, 'ÖNLİSANS': 9254 });
assert.equal(mebi.scoreYearConfirmed, false);

mkdirSync(join(pack, 'payload/coaching'), { recursive: true });
mkdirSync(join(pack, 'payload/admin'), { recursive: true });
mkdirSync(join(pack, 'payload/staff'), { recursive: true });
for (const name of ['Coaching.API', 'Coaching.Application', 'Coaching.Domain', 'Coaching.Infrastructure']) {
  cpSync(join(release, 'publish', name + '.dll'), join(pack, 'payload/coaching', name + '.dll'));
  const pdb = join(release, 'publish', name + '.pdb');
  if (existsSync(pdb)) cpSync(pdb, join(pack, 'payload/coaching', name + '.pdb'));
}
cpSync(join(release, 'operator'), join(pack, 'operator'), { recursive: true });
cpSync(join(root, 'clients/admin-panel/dist/admin-panel'), join(pack, 'payload/admin'), { recursive: true });
cpSync(join(root, 'clients/admin-panel/dist/staff-portal/browser'), join(pack, 'payload/staff'), { recursive: true });
mkdirSync(join(pack, 'catalog'), { recursive: true });
cpSync(fullCatalogPath, join(pack, 'catalog/yks-results.json'));
cpSync(ydtPath, join(pack, 'catalog/ydt-all-languages.json'));
cpSync(mebiPath, join(pack, 'catalog/mebi-yks-program-catalog-verification.json'));
cpSync(resolve(root, 'tools/releases/coaching-target-catalog-20261019-r4/deploy.sh'), join(pack, 'deploy.sh'));
cpSync(resolve(root, 'tools/releases/coaching-target-catalog-20261019-r4/verify.sql'), join(pack, 'verify.sql'));

writeFileSync(join(pack, 'coaching-service.Dockerfile'),
  'ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nARG SOURCE_REVISION\nCOPY payload/coaching/ /app/\nLABEL org.opencontainers.image.revision=$SOURCE_REVISION\n');
writeFileSync(join(pack, 'admin-panel.Dockerfile'),
  'ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nARG SOURCE_REVISION\nUSER root\nRUN rm -f /app/dist/admin-panel/browser/index.html /app/dist/admin-panel/browser/index.html.gz /app/dist/admin-panel/browser/index.html.br\nCOPY payload/admin/ /app/dist/admin-panel/\nRUN chown -R node:node /app/dist/admin-panel && chmod -R u=rwX,go=rX /app/dist/admin-panel\nUSER node\nLABEL org.opencontainers.image.revision=$SOURCE_REVISION\n');
writeFileSync(join(pack, 'staff-portal.Dockerfile'),
  'ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nARG SOURCE_REVISION\nUSER root\nRUN rm -f /usr/share/nginx/html/index.html /usr/share/nginx/html/index.html.gz /usr/share/nginx/html/index.html.br\nCOPY payload/staff/ /usr/share/nginx/html/\nRUN chmod -R u=rwX,go=rX /usr/share/nginx/html\nLABEL org.opencontainers.image.revision=$SOURCE_REVISION\n');

const files = directory => readdirSync(directory).flatMap(name => {
  const path = join(directory, name);
  return statSync(path).isDirectory() ? files(path) : [path];
});
assert.ok(files(pack).every(file => !relative(pack, file).split(/[\\/]/).some(name => name === '.env' || name.startsWith('.env.'))));
const sha = file => createHash('sha256').update(readFileSync(file)).digest('hex');
writeFileSync(join(pack, 'payload.sha256'), files(pack)
  .map(file => `${sha(file)}  ${relative(pack, file).replaceAll('\\', '/')}`).join('\n') + '\n');
const archive = join(release, 'release.tar.gz');
execFileSync('tar', ['-czf', archive, '-C', pack, '.']);
const result = {
  archive,
  sha256: sha(archive),
  programCatalogRows: fullCatalog.rows.length,
  ydtRows: ydt.rows.length,
  officialEvidenceHash: fullCatalog.documentHash,
  mebiIdentityHash: mebi.identitySha256
};
writeFileSync(join(release, 'package-info.json'), JSON.stringify(result, null, 2));
console.log(JSON.stringify(result));
