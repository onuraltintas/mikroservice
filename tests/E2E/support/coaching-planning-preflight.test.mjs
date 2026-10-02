import { test } from 'node:test';
import assert from 'node:assert/strict';
import { validatePlanningProcess } from './coaching-planning-preflight.mjs';

const path = 'D:/Calismalar/microservice/services/coaching-service/Coaching.API/bin/Debug/net10.0/Coaching.API.dll';
const connection = 'Host=127.0.0.1;Port=55441;Database=coaching_planning_e2e;Username=planning_test;Password=disposable-planning-only';
const identity = '--Services:IdentityService http://127.0.0.1:4600';
test('rejects an API without an explicit disposable database override', () => {
  assert.throws(() => validatePlanningProcess(`dotnet ${path}`));
  assert.throws(() => validatePlanningProcess(`dotnet ${path} --ConnectionStrings:DefaultConnection "Host=production"`));
});
test('accepts only the local API with the exact disposable database override', () => {
  assert.doesNotThrow(() => validatePlanningProcess(`dotnet ${path} --ConnectionStrings:DefaultConnection "${connection}" ${identity}`));
  assert.throws(() => validatePlanningProcess(`dotnet unrelated.dll --ConnectionStrings:DefaultConnection "${connection}"`));
});
test('rejects missing, external or duplicate Identity overrides', () => {
  const local = `dotnet ${path} --ConnectionStrings:DefaultConnection "${connection}"`;
  assert.throws(() => validatePlanningProcess(local));
  assert.throws(() => validatePlanningProcess(`${local} --Services:IdentityService https://production.example`));
  assert.throws(() => validatePlanningProcess(`${local} ${identity} --Services:IdentityService=https://production.example`));
});
test('rejects later overrides and a decoy API path in an unrelated process', () => {
  assert.throws(() => validatePlanningProcess(`dotnet ${path} --ConnectionStrings:DefaultConnection "${connection}" --ConnectionStrings:DefaultConnection=Host=production`));
  assert.throws(() => validatePlanningProcess(`dotnet ${path} --ConnectionStrings:DefaultConnection "${connection}" ConnectionStrings:DefaultConnection=Host=production`));
  assert.throws(() => validatePlanningProcess(`dotnet other.dll --decoy ${path} --ConnectionStrings:DefaultConnection "${connection}"`));
  assert.throws(() => validatePlanningProcess(`dotnet C:/another-checkout/services/coaching-service/Coaching.API/bin/Debug/net10.0/Coaching.API.dll --ConnectionStrings:DefaultConnection "${connection}"`));
});
