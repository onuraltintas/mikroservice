// Paket I4 (user decision 14.11.2026): a student may save a practice exam without the score of a result document.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const service = 'services/coaching-service/';

test('a result may be saved without a score, and a missing score is never read as zero', async () => {
  assert.match(await read(service + 'Coaching.Domain/Entities/Exam.cs'), /public decimal\? DeclaredScore => HasNoScore \? null : Score;/);
  assert.match(await read(service + 'Coaching.Application/StudyPlanning/IStudentExamService.cs'), /decimal\? Score, decimal MaxScore,[\s\S]*decimal\? Score, decimal MaxScore,/);
  const migration = await read(service + 'Coaching.Infrastructure/Data/Migrations/20261122090000_ExamResultHasNoScore.cs');
  assert.match(migration, /name: "has_no_score"[\s\S]*nullable: false,\s*defaultValue: false/);
  const report = await read(service + 'Coaching.Infrastructure/StudyPlanning/CoachingStudentStudyReportService.cs');
  assert.match(report, /exams\.Where\(x => !x\.HasNoScore\)\.Select\(x => new GoalScoreEvidence\(/);
  assert.match(report, /x\.MaxScore > 0 && !x\.HasNoScore/);
  const form = await read('clients/admin-panel/src/app/features/coaching-portal/pages/student-exam-results.component.html');
  assert.match(form, /data-testid="exam-score"/);
  assert.doesNotMatch(form, /name="score"[^>]*required/);
  assert.match(form, /Puan girilmedi/);
});
