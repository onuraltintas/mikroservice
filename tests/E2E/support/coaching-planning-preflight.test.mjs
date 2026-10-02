import { test } from 'node:test';
import assert from 'node:assert/strict';
import { validatePlanningProcess } from './coaching-planning-preflight.mjs';

const path = 'D:/Calismalar/microservice/services/coaching-service/Coaching.API/bin/Debug/net10.0/Coaching.API.dll';
const connection = 'Host=127.0.0.1;Port=55441;Database=coaching_planning_e2e;Username=planning_test;Password=disposable-planning-only';
test('rejects an API without an explicit disposable database override', () => {
  assert.throws(() => validatePlanningProcess(`dotnet ${path}`));
  assert.throws(() => validatePlanningProcess(`dotnet ${path} --ConnectionStrings:DefaultConnection "Host=production"`));
});
test('accepts only the local API with the exact disposable database override', () => {
  assert.doesNotThrow(() => validatePlanningProcess(`dotnet ${path} --ConnectionStrings:DefaultConnection "${connection}"`));
  assert.throws(() => validatePlanningProcess(`dotnet unrelated.dll --ConnectionStrings:DefaultConnection "${connection}"`));
});
test('rejects later overrides and a decoy API path in an unrelated process', () => {
  assert.throws(() => validatePlanningProcess(`dotnet ${path} --ConnectionStrings:DefaultConnection "${connection}" --ConnectionStrings:DefaultConnection=Host=production`));
  assert.throws(() => validatePlanningProcess(`dotnet ${path} --ConnectionStrings:DefaultConnection "${connection}" ConnectionStrings:DefaultConnection=Host=production`));
  assert.throws(() => validatePlanningProcess(`dotnet other.dll --decoy ${path} --ConnectionStrings:DefaultConnection "${connection}"`));
});
