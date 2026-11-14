import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

// Newsletter consent texts in force (user decision 14.11.2026, option B): the two pages published on 05.10.2026 for the adult
// test period lose the test-period title, the "18 yaş ve üzeri test katılımcıları" line and the whole closing paragraph about
// the test period. Every other sentence stays word for word.
const sql = await readFile(new URL('../../docs/legal/publish-newsletter-consent-20261114.sql', import.meta.url), 'utf8');
const pages = new Map([...sql.matchAll(/\('([a-z-]+)','([^']*)',replace\(\$legal\$([\s\S]*?)\$legal\$, '__YAYIN_GÜNÜ__', :'publish_day'\),'([0-9a-f]{32})'\)/g)]
  .map(([, slug, title, content, md5]) => [slug, { title, content, md5 }]));

test('both consent pages are rewritten, and only those', () => {
  assert.deepEqual([...pages.keys()], ['coaching-newsletter-consent', 'speed-reading-newsletter-consent']);
  assert.equal(pages.get('coaching-newsletter-consent').title, 'Koçluk bülten onayı');
  assert.equal(pages.get('speed-reading-newsletter-consent').title, 'Hızlı Okuma bülten onayı');
});

test('no test-period, age or unfinished-obligation wording is left', () => {
  for (const { title, content } of pages.values()) {
    assert.doesNotMatch(`${title}\n${content}`, /test|yetişkin|18 yaş|İYS|tamamlanmalıdır|anlamına gelmez|çocuklara/i);
    assert.match(content, /^__YAYIN_GÜNÜ__ — .+ bülten onayı\n\nE-posta adresimin ONAL YAZILIM/);
  }
});

test('the consent itself is kept: optional, separate per product, revocable, not a general data consent', () => {
  for (const [slug, { content }] of pages) {
    assert.match(content, /isteğe bağlı olarak izin veriyorum\./);
    assert.match(content, /Bu izin hesap açma, giriş yapma veya hizmet kullanma şartı değildir\./);
    assert.match(content, slug.startsWith('coaching') ? /Hızlı Okuma bültenine otomatik abonelik sağlamaz/ : /Koçluk bültenine otomatik abonelik sağlamaz/);
    assert.match(content, /abonelikten çıkma bağlantısıyla veya info@onalotomasyon\.com adresine başvurarak geri alabilirim/);
    assert.match(content, /genel bir kişisel veri işleme veya yurt dışı aktarım rızası değildir\.\n\nİletişim: info@onalotomasyon\.com · 0505 707 20 05\.$/);
    assert.equal(content.split('\n\n').length, 5);
  }
});

test('the script guards the live text, keeps a revision and needs the publish day', () => {
  assert.match(sql, /md5\(p\."Content"\)=c\.previous_md5/);
  assert.match(sql, /INSERT INTO identity\."PlatformLegalPageRevisions"/);
  assert.match(sql, /"Version"=p\."Version"\+1/);
  assert.match(sql, /Set publish_day to the publish day/);
  assert.match(sql, /^BEGIN;[\s\S]*COMMIT;\s*$/m);
});
