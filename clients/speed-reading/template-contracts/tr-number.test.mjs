import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import ts from '../node_modules/typescript/lib/typescript.js';

const source = readFileSync(new URL('../../shared/utils/tr-number.ts', import.meta.url), 'utf8');
const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
const { trNumber, trMoney, trPercent } = await import('data:text/javascript;base64,' + Buffer.from(js).toString('base64'));

test('Turkish separators without locale data', () => {
  assert.equal(trMoney(0), '0,00 TL');
  assert.equal(trMoney(749), '749,00 TL');
  assert.equal(trMoney(1234567.5), '1.234.567,50 TL');
  assert.equal(trNumber(87.5, 1), '87,5');
  assert.equal(trNumber(85.714, 2), '85,71');
  assert.equal(trNumber(312), '312');
  assert.equal(trNumber(-2.5, 1), '−2,5');
  assert.equal(trNumber(null), '—');
});

test('one rounding rule: half away from zero, also for values stored as 9.335', () => {
  assert.equal(trNumber(9.335, 2), '9,34');
  assert.equal(trNumber(1.005, 2), '1,01');
  assert.equal(trNumber(104.47), '104');
});

test('the percent sign comes first', () => {
  assert.equal(trPercent(86), '%86');
  assert.equal(trPercent(87.5, 1), '%87,5');
  assert.equal(trPercent(100), '%100');
  assert.equal(trPercent(undefined), '—');
});
