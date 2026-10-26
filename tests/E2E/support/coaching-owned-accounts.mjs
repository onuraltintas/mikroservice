// Since 2026-10-16 Coaching owns its student/teacher profiles, institution memberships and
// teacher-student assignments (coaching.student_profiles, teacher_profiles,
// institution_memberships, teacher_student_assignments). Identity only says whether an
// account is active, has Coaching access and holds a Coaching-scoped role. Disposable E2E
// stacks seed both halves with these helpers.

const uuid = value => {
  if (!/^[0-9a-f-]{36}$/i.test(value)) throw new Error(`Not a UUID: ${value}`);
  return value;
};

/** Identity side: Coaching-scoped roles and an active Coaching product access per user. */
export function identityCoachingAccessSql(userIds, now) {
  const ids = userIds.map(id => `'${uuid(id)}'::uuid`).join(', ');
  return `
    UPDATE identity."UserRoles" SET "Product" = 'Coaching'
      WHERE "UserId" IN (${ids}) AND "Product" IS NULL;
    INSERT INTO identity."UserProductAccesses"
      ("Id", "UserId", "Product", "Source", "IsActive", "GrantedAt", "CreatedAt")
    SELECT gen_random_uuid(), user_id, 'Coaching', 'Migration', true, '${now}', '${now}'
      FROM (VALUES ${userIds.map(id => `('${uuid(id)}'::uuid)`).join(', ')}) AS users(user_id);`;
}

/**
 * Coaching side. students/teachers: [{ userId, institutionId? }];
 * assignments: [{ teacherUserId, studentUserId, institutionId? }].
 */
export function coachingOwnedProfilesSql({ students = [], teachers = [], assignments = [] }, now) {
  const nullable = id => (id ? `'${uuid(id)}'` : 'NULL');
  const statements = [];
  for (const { userId } of students) {
    statements.push(`INSERT INTO coaching.student_profiles
      ("Id", "UserId", "EducationStatus", "ShareProgressWithTeachers", "IsActive", "CreatedAt", "Version")
      VALUES (gen_random_uuid(), '${uuid(userId)}', 0, true, true, '${now}', 0);`);
  }
  for (const { userId } of teachers) {
    statements.push(`INSERT INTO coaching.teacher_profiles
      ("Id", "UserId", "SubjectsJson", "CanViewAllInstitutionStudents", "IsActive", "CreatedAt", "Version")
      VALUES (gen_random_uuid(), '${uuid(userId)}', '[]', false, true, '${now}', 0);`);
  }
  for (const [members, role] of [[students, 1], [teachers, 2]]) {
    for (const { userId, institutionId } of members.filter(member => member.institutionId)) {
      statements.push(`INSERT INTO coaching.institution_memberships
        ("Id", "UserId", "InstitutionId", "Role", "IsActive", "CreatedAt", "Version")
        VALUES (gen_random_uuid(), '${uuid(userId)}', '${uuid(institutionId)}', ${role}, true, '${now}', 0);`);
    }
  }
  for (const { teacherUserId, studentUserId, institutionId } of assignments) {
    statements.push(`INSERT INTO coaching.teacher_student_assignments
      ("Id", "TeacherUserId", "StudentUserId", "InstitutionId", "StartDate", "IsActive", "CreatedAt", "Version")
      VALUES (gen_random_uuid(), '${uuid(teacherUserId)}', '${uuid(studentUserId)}', ${nullable(institutionId)},
        '${now}', true, '${now}', 0);`);
  }
  return statements.join('\n');
}
