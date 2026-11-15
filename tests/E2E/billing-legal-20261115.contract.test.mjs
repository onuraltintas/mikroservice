import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

// Billing stage 10, user decision of 15.11.2026: the bank statement uploaded to match transfers carries the sender's name;
// the KVKK notice and the privacy policy say so. docs/legal/publish-billing-legal-20261115.sql adds those sentences on top
// of the texts of publish-legal-fixes-20261114.sql; every other sentence stays word for word.
const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const pages = sql => Object.fromEntries([...sql.matchAll(/\('([a-z-]+)','[^']*',replace\(\$legal\$([\s\S]*?)\$legal\$, '__YAYIN_GÜNÜ__', :'publish_day'\),'([0-9a-f]{32})'\)/g)]
  .map(match => [match[1], { text: match[2], previous: match[3] }]));
const below = text => text.slice(text.indexOf('\n') + 1);
const sql = await read('docs/legal/publish-billing-legal-20261115.sql');
const after = pages(sql);
const before = pages(await read('docs/legal/publish-legal-fixes-20261114.sql'));

test('the script rewrites the KVKK notice and the privacy policy on top of the 14.11.2026 texts', () => {
  assert.deepEqual(Object.keys(after).sort(), ['kvkk', 'privacy']);
  for (const slug of Object.keys(after))
    assert.equal(after[slug].previous, createHash('md5').update(below(before[slug].text)).digest('hex'), slug);
  assert.match(sql, /Run AFTER publish-legal-fixes-20261114\.sql/);
});

test('only the bank statement sentences are added', () => {
  const added = {
    kvkk: [
      / Havale\/EFT ödemelerinin siparişle eşleştirilmesi için şirketin banka hesap dökümü sisteme yüklenir; dökümdeki hareketin tarihi, tutarı, açıklaması ve gönderenin adı işlenir\. Ödemeyi abone dışında biri \(örneğin velisi\) yaptıysa onun adı da yalnız bu amaçla işlenir; bu bilgi kişinin kendisinden değil, bankanın şirkete verdiği hesap dökümünden elde edilir\./,
      / Banka dökümündeki gönderen adı, ödemenin doğru siparişle eşleştirilmesi için m\.5\/2\(c\), mali kayıt yükümlülüğü için m\.5\/2\(ç\) kapsamında; ödemeyi abone dışında biri yaptıysa ödemenin doğru kişiye işlenmesindeki meşru menfaat kapsamında m\.5\/2\(f\) ile işlenir\./,
      /; siparişle eşleşen banka hareketindeki gönderen adı ve açıklama da aynı sürenin sonunda silinir/,
      / Hiçbir siparişle eşleşmeyen banka hareketlerindeki gönderen adı ve açıklama, hareket tarihinden 1 yıl sonra silinir\./,
    ],
    privacy: [
      / Havale\/EFT ödemelerini siparişle eşleştirmek için şirketin banka hesap dökümündeki hareketin tarihi, tutarı, açıklaması ve gönderenin adı da işlenir; ödemeyi abone dışında biri yaptıysa onun adı yalnız bu amaçla işlenir\./,
    ],
  };
  for (const [slug, sentences] of Object.entries(added)) {
    let text = after[slug].text;
    for (const sentence of sentences) {
      assert.match(text, sentence, slug);
      text = text.replace(sentence, '');
    }
    assert.equal(text, before[slug].text, slug);
  }
});

test('the texts keep the decisions of 13.11.2026: no minimum age and no adult payer rule', () => {
  for (const { text } of Object.values(after)) assert.doesNotMatch(text, /18 yaş|reşit|yetişkin/i);
});

test('what the notice says about clearing matches the code', async () => {
  const lifecycle = await read('services/billing-service/Billing.Infrastructure/BillingLifecycle.cs');
  assert.match(lifecycle, /BankTransactions\.Where\(item => item\.OrderId != null[\s\S]*?SetProperty\(item => item\.Sender, \(string\?\)null\)/);
  assert.match(lifecycle, /item\.OrderId == null && item\.BookedAt <= before[\s\S]*?SetProperty\(item => item\.Sender, \(string\?\)null\)/);
  assert.match(await read('services/billing-service/Billing.Domain/Extras.cs'), /UnmatchedBankLineYears = 1;/);
  assert.match(await read('services/billing-service/Billing.API/OrderEndpoints.cs'), /ClearUnmatchedBankLinesAsync\(stoppingToken\)/);
});
