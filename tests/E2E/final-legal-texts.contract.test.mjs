import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

// Final legal texts (user decision 13.11.2026): the six pages published on 01.10.2026 for an adult test period become the texts in
// force. docs/legal/publish-final-legal-20261113.sql rewrites five of them without the test-period wording and unpublishes
// "Test katılım sınırları". User decisions of the same day: no minimum age (every age group may use the products) and no adult
// payer rule (the checkout declaration is removed too); the "not completed yet" sentences about transfer documents and the retention schedule are removed.
// The coaching sales documents (pre-information form, distance-sales contract) are approved as they are: no draft note any more.
// Not run on the server by this change: publishing needs the user's go-ahead.
const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const texts = source => new Map([...source.matchAll(/\('([a-z0-9-]+)','([^']*)',(?:replace\()?\$legal\$([\s\S]*?)\$legal\$/g)]
  .map(match => [match[1], { title: match[2], content: match[3] }]));
const md5 = text => createHash('md5').update(text, 'utf8').digest('hex');
const body = text => text.slice(text.indexOf('\n') + 1);

const sql = await read('docs/legal/publish-final-legal-20261113.sql');
const final = texts(sql);
const october = texts(await read('docs/legal/publish-test-legal-20261001.sql'));
const liveKvkk = texts(await read('docs/legal/publish-kvkk-coaching-education-20261107.sql')).get('kvkk').content;
const previous = slug => slug === 'kvkk' ? liveKvkk : october.get(slug).content;
const slugs = ['privacy', 'kvkk', 'coaching-terms', 'speed-reading-terms', 'cookies'];

test('five pages become final texts and the test participation page is unpublished', () => {
  assert.deepEqual([...final.keys()], slugs);
  assert.deepEqual(slugs.map(slug => final.get(slug).title), ['Gizlilik politikası', 'KVKK aydınlatma metni', 'Koçluk kullanım koşulları',
    'Hızlı Okuma kullanım koşulları', 'Çerez ve tarayıcı depolama politikası']);
  assert.match(sql, /SET "IsPublished"=false, "Version"="Version"\+1,[\s\S]*?WHERE "Slug"='test-participation' AND "IsPublished";/);
  // Unpublished, not archived: it can be published again in the admin panel.
  assert.doesNotMatch(sql, /"ArchivedAt"\s*=\s*now/);
});

test('no test-period wording is left', () => {
  for (const slug of slugs) {
    const { title, content } = final.get(slug);
    for (const gone of [/test/i, /18 yaş ve üzeri/, /örnek veri/, /sentetik/, /katılımcı/, /ödeme alınma(dan|yan)/, /henüz/, /tamamlanma/, /doğrulanmamış/,
      /uyum belgesi/])
      assert.doesNotMatch(`${title}\n${content}`, gone, `${slug}: ${gone}`);
    assert.match(content, /^__YAYIN_GÜNÜ__ — /, slug);
  }
});

test('age: no minimum age and no adult payer rule, in the texts or at the checkout', async () => {
  // User decisions 13.11.2026: every age group may use the products and the payer is not asked to be an adult.
  for (const slug of slugs)
    assert.doesNotMatch(final.get(slug).content, /\d+ yaş|yaşını doldurmuş|yaş ve üzeri|yaş beyanı|yasal temsilci/, slug);
  assert.match(final.get('coaching-terms').content, /Koçluk öğrencilere, velilere, öğretmenlere ve kurumlara yöneliktir\./);
  assert.match(final.get('speed-reading-terms').content, /Hızlı Okuma öğrencilere, velilere, öğretmenlere ve kurumlara yöneliktir\./);
  // The four payment screens no longer show the declaration and the servers no longer require it.
  for (const screen of ['clients/admin-panel/src/app/features/coaching-portal/pages/coaching-subscription.component.ts',
    'clients/admin-panel/src/app/features/coaching-portal/parent/parent-payments.component.ts',
    'clients/admin-panel/projects/staff-portal/src/app/features/coaching/coaching-teacher-subscription.component.ts',
    'clients/speed-reading/src/app/features/public/payment/payment.component.html',
    'clients/speed-reading/src/app/features/public/payment/payment.component.ts'])
    assert.doesNotMatch(await read(screen), /adultPayerDeclaration|18 yaş/, screen);
  for (const contract of ['services/coaching-service/Coaching.Application/Subscriptions/CoachingSubscriptionContracts.cs',
    'services/speed-reading-service/SpeedReading.Application/Subscription/SpeedReadingSubscriptionContracts.cs']) {
    const source = await read(contract);
    assert.doesNotMatch(source, /18 yaş/, contract);
    // An older client (the speed reading service worker keeps one for a while) may still send it: the property stays, unrequired.
    assert.match(source, /bool AdultPayerDeclaration = false/, contract);
  }
  for (const service of ['services/coaching-service/Coaching.Infrastructure/Management/CoachingSubscriptionService.cs',
    'services/speed-reading-service/SpeedReading.Infrastructure/Legacy/LegacySpeedReadingSubscription.cs']) {
    const source = await read(service);
    assert.doesNotMatch(source, /if \(!request\.AdultPayerDeclaration\) return null;/, service);
    // A declaration is recorded only when one was really sent; the columns and the old records stay.
    assert.match(source, /AdultPayerDeclarationVersion = request\.AdultPayerDeclaration \? 1 : null/, service);
  }
});

test('the texts describe the sale as the product makes it', async () => {
  const coaching = final.get('coaching-terms').content;
  assert.match(coaching, /banka havalesi\/EFT ile satın alınır/);
  assert.match(coaching, /ön bilgilendirme formu ve mesafeli satış sözleşmesi sunulur ve alıcının onayı alınır/);
  assert.match(coaching, /kendiliğinden yenilenmez/);
  assert.match(coaching, /ücretsiz deneme tanımlıysa hesap başına bir kez kullanılabilir ve kendiliğinden ücretli aboneliğe dönüşmez/);
  assert.match(final.get('speed-reading-terms').content, /banka havalesi\/EFT ile satın alınır;[^.]*\. Abonelik ödemenin onaylanmasıyla başlar; kendiliğinden yenilenmez/);
  // The payment data a bank-transfer purchase processes is named in the notice and the privacy policy.
  assert.match(final.get('kvkk').content, /havale\/EFT bildirimi \(ödeme referansı, gönderen adı ve yüklenirse dekont\), onaylanan ön bilgilendirme formu ile mesafeli satış sözleşmesi, cayma talebi ve fatura kayıtları/);
  assert.match(final.get('privacy').content, /havale\/EFT bildirimi, onaylanan sözleşme metinleri ve fatura kayıtları[^.]*; kart bilgisi alınmaz\./);
  // The cookie policy names the analytics consent the site really asks for (roadmap O11).
  const analytics = await read('clients/admin-panel/src/app/core/services/site-analytics.service.ts');
  const key = analytics.match(/const CONSENT_KEY = '([^']+)'/)[1];
  assert.ok(final.get('cookies').content.includes(`tercih tarayıcıda ${key} kaydında tutulur`));
  assert.match(final.get('cookies').content, /Google Analytics 4[^.]*yalnız çerez bandında "Kabul et" seçildiğinde kullanılır/);
});

test('every paragraph the decisions do not touch is kept word for word', () => {
  // Paragraph index → why it may differ. Index 0 is the date line everywhere.
  const rewritten = { privacy: [2, 3, 4, 5, 6], kvkk: [2, 3, 5, 6, 7], 'coaching-terms': [2, 3, 4, 5], 'speed-reading-terms': [2, 4, 5], cookies: [1, 4, 5] };
  for (const slug of slugs) {
    const before = previous(slug).split('\n\n'); const after = final.get(slug).content.split('\n\n');
    assert.equal(after.length, before.length, `${slug}: paragraph count`);
    before.forEach((paragraph, index) => {
      if (index === 0) return;
      if (rewritten[slug].includes(index)) assert.notEqual(after[index], paragraph, `${slug} §${index} was meant to change`);
      else assert.equal(after[index], paragraph, `${slug} §${index} must stay`);
    });
  }
  // The long coaching wizard paragraph of the KVKK notice (version 3) is untouched.
  assert.ok(final.get('kvkk').content.includes(liveKvkk.split('\n\n')[4]));
  // Inside the rewritten paragraphs the sentences the decisions do not touch stay too.
  for (const kept of ['Hostinger VPS ve aynı sunucudaki yedekler işletmeci beyanıyla Litvanya\'dadır; Hostinger SMTP ve Google giriş/reCAPTCHA kullanılır.',
    'Abonelik iptali hesap silme değildir.', 'Pazarlama ve bülten izni ayrıca, isteğe bağlı olarak alınmalıdır.'])
    assert.ok(final.get('kvkk').content.includes(kept), kept);
  for (const kept of ['Otomatik, anında ve tüm yedekleri kapsayan silme garantisi verilmez.', 'Aboneliğin bitmesi hesabın silinmesi değildir.'])
    assert.ok(final.get('privacy').content.includes(kept), kept);
});

test('the removed status sentences are exactly the ones the user decided on', () => {
  const removed = {
    kvkk: ['Yurt dışı aktarım belgeleri ve tüm alt işleyen/işleme ülkeleri henüz tamamlanmış değildir.', 'Bu aydınlatma eksik aktarım mekanizmasının yerine geçmez.',
      'Kategori bazlı kesin saklama/yedek imha takvimi henüz belirlenmemiştir.'],
    privacy: ['Yurt dışı aktarım belgeleri tamamlanmamıştır;', 'Veri türlerine ait kesin saklama ve yedek imha takvimi henüz belirlenmemiştir.']
  };
  for (const [slug, sentences] of Object.entries(removed)) for (const sentence of sentences) {
    assert.ok(previous(slug).includes(sentence), `${slug}: the published text had it`);
    assert.ok(!final.get(slug).content.includes(sentence), `${slug}: still there`);
  }
  // Nothing claims the opposite: the texts say where data is processed, not that a transfer mechanism is complete.
  for (const slug of ['kvkk', 'privacy']) {
    assert.match(final.get(slug).content, /Bu hizmetler nedeniyle veriler yurt dışında işlenir\./);
    assert.doesNotMatch(final.get(slug).content, /aktarım belgeleri|taahhütname|standart sözleşme/);
  }
});

test('it refuses unless the live pages are the published texts, compared by md5', () => {
  for (const slug of slugs) {
    const expected = slug === 'kvkk' ? md5(body(liveKvkk)) : md5(previous(slug));
    const row = sql.match(new RegExp(`\\('${slug}','[^']*',replace\\(\\$legal\\$[\\s\\S]*?\\$legal\\$, '__YAYIN_GÜNÜ__', :'publish_day'\\),'([0-9a-f]{32})',(true|false)\\)`));
    assert.ok(row, `${slug} row`);
    assert.equal(row[1], expected, `${slug}: md5 of the published text`);
    // Only the KVKK notice is compared without its date line (its live date line was set at publish time).
    assert.equal(row[2], slug === 'kvkk' ? 'true' : 'false');
  }
  assert.ok(sql.includes(`md5(p."Content")<>'${md5(october.get('test-participation').content)}'`));
  assert.match(sql, /RAISE EXCEPTION 'These pages differ from the published texts/);
  assert.match(sql, /RAISE EXCEPTION 'test-participation differs from the 1 Ekim 2026 text/);
});

test('the date line must be set to the publish day before it runs', () => {
  assert.match(sql, /^\\if :\{\?publish_day\}\n\\else\n\\set publish_day 'GG Ay YYYY'\n\\endif$/m);
  assert.equal(sql.match(/^\\set publish_day/gm).length, 1);
  assert.match(sql, /RAISE EXCEPTION 'Set publish_day/);
});

test('it is published like the earlier legal texts and changes only the texts', () => {
  assert.match(sql, /INSERT INTO identity\."PlatformLegalPageRevisions"/);
  assert.match(sql, /"Version"=p\."Version"\+1/);
  assert.match(sql, /^BEGIN;/m); assert.match(sql, /^COMMIT;/m);
  for (const forbidden of [/RegistrationLegalConsent/i, /Acceptances/i, /Erasure/i, /DELETE FROM/i, /"users"\s+SET/i])
    assert.doesNotMatch(sql, forbidden);
});

test('the sales documents are the approved texts: no draft note, same wording version', async () => {
  const documents = await read('services/coaching-service/Coaching.Infrastructure/Management/CoachingSalesDocuments.cs');
  assert.doesNotMatch(documents, /DRAFTS|a lawyer should review/);
  // The wording did not change, so the version buyers already accepted stays.
  assert.match(documents, /public const string Version = "2026-10-31";/);
  const admin = await read('clients/admin-panel/src/app/features/dashboard/pages/coaching-sales.ts');
  assert.doesNotMatch(admin, /taslaktır|avukata/);
  assert.match(admin, /data-testid="documents-note">[^<]*onaylanmış <strong>esas metinlerdir<\/strong>/);
});
