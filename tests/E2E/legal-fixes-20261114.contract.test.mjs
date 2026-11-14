import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

// Legal check of 14.11.2026 (claude/legal-check-2026-11-14.md): the findings the texts can settle (Y1, Y5, Y6, Y7, Y8) and the
// 10-year financial retention the user decided the same day. docs/legal/publish-legal-fixes-20261114.sql rewrites four pages on
// top of the texts of publish-final-legal-20261113.sql; every other sentence stays word for word.
const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const pages = (source, md5) => new Map([...source.matchAll(md5
  ? /\('([a-z0-9-]+)','([^']*)',replace\(\$legal\$([\s\S]*?)\$legal\$, '__YAYIN_GÜNÜ__', :'publish_day'\),'([0-9a-f]{32})'\)/g
  : /\('([a-z0-9-]+)','([^']*)',replace\(\$legal\$([\s\S]*?)\$legal\$, '__YAYIN_GÜNÜ__', :'publish_day'\)/g)]
  .map(([, slug, title, content, hash]) => [slug, { title, content, hash }]));
const body = content => content.slice(content.indexOf('\n') + 1);
const sql = await read('docs/legal/publish-legal-fixes-20261114.sql');
const fixed = pages(sql, true);
const before = pages(await read('docs/legal/publish-final-legal-20261113.sql'), false);
const sentences = text => text.split(/(?<=[.:;])\s+/);

test('four pages are rewritten on top of the published texts, guarded by their md5', () => {
  assert.deepEqual([...fixed.keys()], ['privacy', 'kvkk', 'speed-reading-terms', 'cookies']);
  for (const [slug, page] of fixed) {
    assert.equal(page.title, before.get(slug).title, slug);
    assert.equal(page.hash, createHash('md5').update(body(before.get(slug).content)).digest('hex'), slug);
    assert.equal(page.content.split('\n')[0], before.get(slug).content.split('\n')[0], slug);
    assert.equal(page.content.split('\n\n').length, before.get(slug).content.split('\n\n').length, `${slug} paragraphs`);
  }
});

test('only the decided sentences change', () => {
  const changed = { privacy: 2, kvkk: 4, 'speed-reading-terms': 2, cookies: 4 };
  for (const [slug, page] of fixed) {
    const old = new Set(sentences(before.get(slug).content));
    const added = sentences(page.content).filter(sentence => !old.has(sentence));
    assert.ok(added.length >= changed[slug] && added.length <= changed[slug] + 4, `${slug}: ${added.length} new sentences`);
  }
});

test('KVKK: statement wording, the analytics service and the 10-year financial retention', () => {
  const kvkk = fixed.get('kvkk').content;
  assert.doesNotMatch(kvkk, /saklanmalıdır|alınmalıdır|artık kayıt formunda/);
  assert.match(kvkk, /Veriler amaç ve yasal gereklilikle sınırlı olarak saklanır\./);
  assert.match(kvkk, /Pazarlama ve bülten izni ayrıca ve isteğe bağlı olarak alınır\./);
  assert.match(kvkk, /çerez bandında "Kabul et" seçilirse ziyaret ölçümü için Google Analytics kullanılır\. Bu hizmetler nedeniyle veriler yurt dışında işlenir\./);
  assert.match(kvkk, /fatura kayıtları 10 yıl daha saklanır; süre dolunca abonelik ve ödeme kayıtlarındaki ad, e-posta ve notlar silinir/);
  assert.match(fixed.get('privacy').content, /Google Analytics kullanılır\./);
  assert.match(fixed.get('privacy').content, /10 yıl daha saklanır/);
});

test('the retention the texts state is the retention the services apply', async () => {
  const coaching = await read('services/coaching-service/Coaching.Domain/Entities/CoachingPrivacyEntities.cs');
  const speed = await read('services/speed-reading-service/SpeedReading.Infrastructure/Persistence/SpeedReadingErasureExecution.cs');
  for (const source of [coaching, speed]) assert.match(source, /public const int FinancialRetentionYears = 10;/);
});

test('cookies: the inventory matches the code', async () => {
  const cookies = fixed.get('cookies').content;
  assert.doesNotMatch(cookies, /vocab_progress/);
  const names = ['eduplatform_refresh', 'eduplatform_course_management_refresh', 'eduplatform_staff_active_product', 'staff.coaching.institution',
    'coaching.diplomaGrade', 'coaching.celebration.dismissed', 'eduivme.utm.v1', 'eduivme.cookie-consent.v1', 'rememberedEmail'];
  for (const name of names) assert.ok(cookies.includes(name), name);
  assert.match(cookies, /Koçluk tanıtım, giriş, kayıt ve yasal metin sayfalarında ziyaretler Google Analytics 4 ile ölçülebilir/);
  const code = (await Promise.all([
    'services/identity-service/Identity.API/Security/RefreshTokenCookiePolicy.cs',
    'clients/admin-panel/projects/staff-portal/src/app/auth/staff-auth.service.ts',
    'clients/admin-panel/projects/staff-portal/src/app/features/coaching/coaching-institution-selection.ts',
    'clients/admin-panel/src/app/features/coaching-portal/pages/student-score-estimate.component.ts',
    'clients/admin-panel/shared/coaching-motivation.component.ts',
    'clients/admin-panel/src/app/core/services/site-analytics.service.ts',
    'clients/speed-reading/src/app/features/auth/login/login.component.ts'
  ].map(read))).join('\n');
  for (const name of names) assert.ok(code.includes(name), `code ${name}`);
  assert.match(code, /TRACKED_PATH = \/\^\\\/\(\?:\$\|coaching\(\?:\\\/\|\$\)\|legal\\\/\|auth\\\/\(\?:login\|register\)/);
});

test('Hızlı Okuma terms describe the purchase as the service now runs it', async () => {
  const terms = fixed.get('speed-reading-terms').content;
  assert.match(terms, /ön bilgilendirme formu ve mesafeli satış sözleşmesi sunulur ve alıcının onayı alınır\./);
  assert.match(terms, /Cayma ve iade, mesafeli satış sözleşmesindeki koşullarla ve kanuni haklar saklı kalarak uygulanır\./);
  const subscription = await read('services/speed-reading-service/SpeedReading.Infrastructure/Legacy/LegacySpeedReadingSubscription.cs');
  assert.match(subscription, /"Sales\.DocumentsNotAccepted"/);
});

test('the script keeps a revision, raises the version and needs the publish day', () => {
  assert.match(sql, /INSERT INTO identity\."PlatformLegalPageRevisions"/);
  assert.match(sql, /"Version"=p\."Version"\+1/);
  assert.match(sql, /Set publish_day to the publish day/);
  assert.match(sql, /^BEGIN;[\s\S]*COMMIT;\s*$/m);
  assert.doesNotMatch(sql, /test dönemi|yetişkin|18 yaş/i);
});
