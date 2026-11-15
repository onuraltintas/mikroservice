import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

// User decision 15.11.2026: a student with an active teacher makes or changes her own study plan and weekly schedule only
// when the teacher allows it (off by default); a student without a teacher plans freely; the institution only reads.
const read = path => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');
const planning = 'services/coaching-service/Coaching.Infrastructure/StudyPlanning/';

test('the switch is off by default and matters only with an active teacher', () => {
  assert.match(read('services/coaching-service/Coaching.Domain/Profiles/CoachingProfiles.cs'), /public bool CanPlanOwnStudy \{ get; private set; \}\n/);
  const migration = read('services/coaching-service/Coaching.Infrastructure/Data/Migrations/20261124090000_StudentCanPlanOwnStudy.cs');
  assert.match(migration, /name: "CanPlanOwnStudy",[\s\S]*?table: "student_profiles",[\s\S]*?defaultValue: false/);
  const permission = read(planning + 'StudentPlanningPermission.cs');
  assert.match(permission, /public bool StudentMayPlan => !HasTeacher \|\| AllowedByTeacher;/);
  assert.match(permission, /CoachingTeacherStudentAssignments[\s\S]*?x\.StudentUserId == studentId && x\.IsActive/);
});

test('every plan-changing request of the student is checked; reporting what was studied is not', () => {
  const plans = read(planning + 'CoachingManualStudyPlanService.cs');
  assert.match(plans, /else if \(teacherWrite && changesPlan\) await StudentPlanningPermission\.RequireStudentMayPlanAsync\(db, student, cancellationToken\);/);
  assert.match(plans, /ct,targetStudent,true,changesPlan:false\);/, 'remaining scope is a report');
  assert.match(plans, /CreateAutomaticDraftAsync[\s\S]{0,400}?RequireStudentMayPlanAsync/);
  assert.match(read(planning + 'CoachingWeeklyStudyDraftService.cs'), /public async Task<WeeklyDraftView> SaveAsync[\s\S]{0,400}?RequireStudentMayPlanAsync\(db,student,ct\)/);
  assert.match(read(planning + 'CoachingWeeklyOutcomePlanService.cs'), /public async Task<WeeklyOutcomePlanCreated> CreateAsync[\s\S]{0,300}?RequireStudentMayPlanAsync\(db,student,ct\)/);
});

test('only the teacher switches it; the institution reads; the student is told', () => {
  const staff = read('services/coaching-service/Coaching.API/Controllers/CoachingStaffStudyController.cs');
  assert.match(staff, /\[HttpGet\("planning-permission"\)\]/);
  assert.match(staff, /\[HttpPut\("planning-permission"\),Authorize\(Roles="Teacher"\)/);
  assert.match(staff, /SaveForStudentAsync\(studentId,request,ct\);[\s\S]{0,200}?WeeklyScheduleSavedByTeacherAsync\(studentId,ct\)/);
  const service = read(planning + 'StudentPlanningPermission.cs');
  assert.match(service, /SetForStudentAsync[\s\S]*?RequireTeacherWriteAsync\(studentId, ct\)[\s\S]*?NotifyAsync\(studentId, allowed \? StudentPlansNotice : TeacherPlansNotice, ct\)/);
  assert.match(read('services/coaching-service/Coaching.API/Controllers/WeeklyStudyDraftController.cs'), /\[HttpGet\("~\/api\/coaching\/study-planning\/permission"\)\]/);
});

test('the student pages close plan editing and the teacher page carries the switch', () => {
  const routes = read('clients/admin-panel/src/app/features/coaching-portal/coaching-portal.routes.ts');
  assert.match(routes, /path: 'weekly', canActivate: \[studentMayPlan\]/);
  assert.match(routes, /path: 'wizard', canActivate: \[studentMayPlan\]/);
  assert.match(read('clients/admin-panel/src/app/features/coaching-portal/layout/student-hub.component.ts'), /data-testid="teacher-plans-notice"/);
  assert.match(read('clients/admin-panel/projects/staff-portal/src/app/features/coaching/coaching-staff-study.component.ts'), /Öğrenci kendi planını yapabilsin/);
});
