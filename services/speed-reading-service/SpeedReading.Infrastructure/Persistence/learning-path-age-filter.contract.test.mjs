import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('./OwnedSpeedReadingLearningPaths.cs', import.meta.url), 'utf8');
const generation = source.split('private async Task<int> CreatePersonalizedPathAsync')[1]
  ?.split('public async Task CompletePersonalizedPathItemAsync')[0] ?? '';
assert.match(generation, /AgeGroupConfigurationId/);
assert.match(generation, /item\.TargetAgeGroupId == null/);
