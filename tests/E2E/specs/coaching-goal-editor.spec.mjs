import { test, expect } from '@playwright/test';

// E7 (#30): Hedefim (/coaching-portal/growth/goals) edits the target with the first-login wizard's target step. The production UI
// runs against isolated API fixtures (as coaching-learning-setup.spec.mjs); the target rules themselves are covered by the
// Coaching API and PostgreSQL tests.
const student = 'c3f5a1d2-7e4b-4c1a-9d2e-5b6a7c8d9e01';
const token = () => {
  const encode = x => Buffer.from(JSON.stringify(x)).toString('base64url');
  return `${encode({ alg: 'HS256' })}.${encode({ sub: student, role: 'Student', platform_product: 'coaching', given_name: 'Test', family_name: 'Öğrenci', exp: Math.floor(Date.now() / 1000) + 900 })}.fixture`;
};
const school = { kind: 'school', targetId: 'school-1', name: 'Ankara Fen Lisesi', detail: 'ANKARA / ÇANKAYA', minimumScore: 480.5, scoreYear: 2025, scoreType: 'LGS', scores: [], canEdit: true };
const answers = target => ({ birthDate: '2012-05-01', homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya',
  educationStatus: 'Student', gradeLevel: 8, graduationYear: null, schoolName: null, purpose: 'Lgs', primaryScoreType: null, foreignLanguage: null,
  targetExamSessionId: null, targetExamYear: 2027, diplomaGrade: null, diplomaGradeEstimated: false, target, targetLocked: false });
const view = target => ({ status: 'Completed', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6, 7], completedAt: '2026-11-01T00:00:00Z', academicYearStart: 2026,
  examSessions: [], allowedPurposes: ['Lgs'], acceptedInvitations: [], answers: answers(target) });

async function fixtures(context) {
  let target = school; const saves = []; const calls = { estimate: 0, goalTarget: 0, primary: 0 };
  await context.route('**/api/**', async route => {
    const request = route.request(); const url = new URL(request.url()); const path = url.pathname;
    const respond = data => route.fulfill({ json: { success: true, data } });
    if (path === '/api/auth/refresh-token') return route.fulfill({ json: { accessToken: token(), tokenType: 'Bearer', expiresInMinutes: 15 } });
    if (path.endsWith('/students/me/onboarding/steps/target')) {
      const body = request.postDataJSON(); saves.push(body);
      target = body.kind === 'score' ? { kind: 'score', targetId: null, name: 'LGS', detail: null, minimumScore: null, scoreYear: null, scoreType: 'LGS', scores: body.scores, canEdit: true } : null;
      return respond(view(target));
    }
    if (path.endsWith('/students/me/onboarding')) return respond(view(target));
    if (path.endsWith('/students/me/score-estimate')) {
      calls.estimate++;
      return route.fulfill({ json: { estimates: [], notice: null, notComputed: [], languageScores: [], goal: null, scoreTargets: [],
        target: target ? { targetName: target.kind === 'score' ? 'LGS' : target.name, scoreType: 'LGS', targetScore: target.kind === 'score' ? target.scores[0].score : target.minimumScore,
          targetRank: null, targetPercentile: null, scoreGap: null, netGap: null, rankGap: null, reached: false, kind: target.kind, scoreYear: target.scoreYear, status: null } : null } });
    }
    if (path.endsWith('/study-planning/primary-goal')) { calls.primary++; return respond({ goalId: 'goal-1', version: 1 }); }
    if (path.endsWith('/study-planning/goals/goal-1/target')) {
      calls.goalTarget++;
      return respond({ goalId: 'goal-1', version: calls.goalTarget, targetSchoolId: null, targetUniversityProgramId: null, canEdit: true, netTargets: [{ examCode: 'LGS', targetNet: 72.5 }] });
    }
    if (path.endsWith('/study-planning/setup')) return respond({ context: { purpose: 'Lgs', primaryScoreType: null }, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [] });
    if (path.endsWith('/exams/profiles')) return route.fulfill({ json: [] });
    return respond([]);
  });
  return { saves, calls };
}

test('Hedefim shows the current target first, changes it with the wizard steps and reloads the summary and net targets', async ({ page, context }) => {
  const { saves, calls } = await fixtures(context);
  await page.goto('/coaching-portal/growth/goals');
  const editor = page.getByRole('region', { name: 'Hedefini düzenle' });
  await expect(editor.getByText('Ankara Fen Lisesi · taban 480,5')).toBeVisible();
  await expect(page.getByText('72,5 net')).toBeVisible();
  const before = { estimate: calls.estimate, goalTarget: calls.goalTarget };

  await editor.getByRole('button', { name: /^Değiştir/ }).click();
  await editor.getByRole('button', { name: 'Hedef puan gireceğim', exact: true }).click();
  await editor.getByRole('spinbutton').fill('460');
  await editor.getByRole('button', { name: 'Devam', exact: true }).click();

  await expect(editor.getByText('Hedefin kaydedildi.')).toBeVisible();
  await expect(editor.getByText('LGS 460')).toBeVisible();
  expect(saves).toEqual([{ kind: 'score', targetId: null, scores: [{ scoreType: 'LGS', score: 460 }] }]);
  await expect(page.getByText('Hedef puanın 460')).toBeVisible();
  await expect.poll(() => calls.estimate).toBeGreaterThan(before.estimate);
  await expect.poll(() => calls.goalTarget).toBeGreaterThan(before.goalTarget);
  await expect(page).toHaveURL(/\/coaching-portal\/growth\/goals$/);
});

test('"Vazgeç" keeps the saved target and sends nothing', async ({ page, context }) => {
  const { saves } = await fixtures(context);
  await page.goto('/coaching-portal/growth/goals');
  const editor = page.getByRole('region', { name: 'Hedefini düzenle' });
  await editor.getByRole('button', { name: /^Değiştir/ }).click();
  await editor.getByRole('button', { name: 'Hedef puan gireceğim', exact: true }).click();
  await editor.getByRole('button', { name: 'Vazgeç', exact: true }).click();
  await expect(editor.getByText('Ankara Fen Lisesi · taban 480,5')).toBeVisible();
  await expect(editor.getByRole('spinbutton')).toHaveCount(0);
  expect(saves).toEqual([]);
});
