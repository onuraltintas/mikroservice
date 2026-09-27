import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const sourcePath = new URL('./OwnedSpeedReadingAssessment.cs', import.meta.url);
const source = readFileSync(sourcePath, 'utf8');

assert.match(
  source,
  /var assessmentExerciseCount = templateEntries\.Count > 0\s*\? templateEntries\.Count\s*:\s*ServerAssessmentExerciseCount;/s,
);
assert.match(
  source,
  /var selected = templateEntries\.Count > 0\s*\? candidatePool\s*\.Select\(candidate =>\s*\(candidate, ResolveAssessmentRole\(candidate\.TypeName, candidate\.EngineType\)\)\)\s*\.ToList\(\)\s*:\s*SelectFormCandidates/s,
);
assert.match(
  source,
  /expectedExerciseIds\.Count != attempt\.ExpectedExerciseCount/,
);
assert.doesNotMatch(
  source,
  /templateEntries\.Count > 0 && measurableCandidatePool\.Count != candidatePool\.Count/,
);
assert.doesNotMatch(
  source,
  /measurableCandidatePool = candidates\s*\.Where\(IsServerMeasuredCandidate\)\s*\.ToList\(\);/s,
);
assert.match(
  source,
  /if \(RequiresAssessmentReadingText\(role, candidate\.TypeName, candidate\.EngineType\)\)/,
);
assert.match(
  source,
  /var requiresQuestions = role == "comprehension";/,
);
