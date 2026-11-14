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

test('a plan task leaves its empty fields out of the answer', async () => {
  const contract = await read(service + 'Coaching.Application/StudyPlanning/IManualStudyPlanService.cs');
  const view = contract.slice(contract.indexOf('record ManualStudyTaskView('), contract.indexOf('record StudyTaskOutcomeView('));
  assert.equal(view.match(/JsonIgnore\(Condition = JsonIgnoreCondition\.WhenWritingNull\)/g).length, 21);
  assert.doesNotMatch(view, /(?<!WhenWritingNull\)\] )(?:Guid|int|string|DateTime)\? \w+/);
});

test('the exam editor fits two and three digit counts, and the record actions stand apart', async () => {
  const pages = 'clients/admin-panel/src/app/features/coaching-portal/pages/';
  const editor = await read(pages + 'practice-exam-editor.component.ts');
  assert.match(editor, /td input \{ width:4\.75rem; min-width:4\.75rem; padding:\.5rem \.25rem; text-align:center;/);
  assert.match(editor, /::-webkit-inner-spin-button \{ -webkit-appearance:none;/);
  assert.match(await read(pages + 'student-exam-results.component.html'), /class="actions record-actions"/);
  assert.match(await read(pages + 'student-study-plans.component.scss'), /\.record-actions \{ margin-top: 1\.5rem; padding-top: 1rem; border-top:/);
});
