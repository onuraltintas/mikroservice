// The calls the student shell makes on every page (as in coaching-learning-setup.spec.mjs): the first-login wizard is finished,
// so the onboarding guard lets an 8th-grade LGS student into the panel. A spec's own fixture answers everything else.
export const completedOnboarding = { status: 'Completed', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6, 7], completedAt: '2026-11-01T00:00:00Z', academicYearStart: 2026,
  examSessions: [], allowedPurposes: ['Lgs'], acceptedInvitations: [],
  answers: { birthDate: '2012-05-01', homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya', educationStatus: 'Student',
    gradeLevel: 8, graduationYear: null, schoolName: null, purpose: null, primaryScoreType: null, foreignLanguage: null, targetExamSessionId: null,
    targetExamYear: null, diplomaGrade: null, diplomaGradeEstimated: false, target: null, targetLocked: false } };

/** Answers a shell call and returns its promise, or null when the path is not one of them. */
export function studentShell(route, path) {
  const respond = data => route.fulfill({ json: { success: true, data } });
  if (path.endsWith('/students/me/onboarding')) return respond(completedOnboarding);
  if (path.endsWith('/profiles/me/navigation')) return respond({ showAssignments: true, showSessions: true, showFees: true });
  if (path.endsWith('/messages/unread-count')) return route.fulfill({ json: { count: 0 } });
  return null;
}
