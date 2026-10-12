import { test, expect } from '@playwright/test';

async function setup(page, completed, managed = false) {
  const writes = [], comparisons = [];
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'continuation-test' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url()), path = url.pathname;
    let response = [];
    if (path.endsWith('/auth/refresh-token')) response = { id: 'continuation-test', roles: ['Student'],
      token: `e30.${Buffer.from(JSON.stringify({ sub: 'continuation-test', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url')}.fixture` };
    else if (request.method() === 'POST') {
      writes.push({ path, body: request.postDataJSON() });
      if (path.endsWith('/assessment/attempts')) response = { id: 'old-retention', programProgressId: 'old-program' };
      else response = { success: true };
    } else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    else if (path.endsWith('/assessment/status')) response = { hasCompleted: true };
    else if (path.endsWith('/progress/programs')) response = [{ templateName: 'Program', currentDay: 1, currentWeek: 1,
      totalDays: 28, totalWeeks: 4, totalExercisesCompleted: 182, completedDate: completed ? '2026-10-01T00:00:00Z' : null }];
    else if (path.endsWith('/next-recommendation')) response = { templateId: 'recommended', templateName: 'Önerilen seviye 3',
      sourceProgressId: 'old-program', assessmentAttemptId: 'post', requiresStaffApproval: managed, totalDays: 28,
      recommendedTrainingLevel: 3, measuredLevel: 3, averageWpm: 150, comprehension: 55, trainingSuccessRate: 65,
      higherOption: { templateId: 'upper', templateName: 'Alternatif seviye 4', level: 4, totalDays: 28 } };
    else if (path.endsWith('/assessment/follow-ups')) response = [{ programProgressId: 'old-program', programName: 'Önceki program',
      completedAt: '2026-10-01T00:00:00Z', additionalTraining: true, plan: { nextPhase: 3, phases: [] } }];
    else if (path.endsWith('/assessment/phase-plan')) response = { nextPhase: 3, phases: [{ phase: 3, status: 2 }] };
    else if (path.endsWith('/assessment/comparison')) {
      comparisons.push(url.searchParams.get('programProgressId'));
      response = { additionalTraining: true, attempts: [{ attemptId: 'post', phase: 2, startedAt: '2026-10-01T00:00:00Z',
        averageWpm: 150, averageComprehension: 55 }] };
    } else if (path.endsWith('/assessment/exercises')) response = { exercises: [] };
    else if (path.endsWith('/student-program/my-programs')) response = [{ progressId: 'old-program', templateName: 'Önceki program',
      assignedDate: '2026-09-01T00:00:00Z', measuredLevel: 3, recommendedTrainingLevel: 3, selectedTrainingLevel: 4, selectionPolicyVersion: 'v1' }];
    else if (path.includes('/gamification/user')) response = { currentStreak: 1, currentLevel: 3, totalXP: 0 };
    await route.fulfill({ json: response });
  });
  return { writes, comparisons };
}

test('completed independent student can explicitly choose one level above', async ({ page }) => {
  const { writes } = await setup(page, true);
  await page.goto('/student/dashboard');
  const button = page.getByRole('button', { name: 'Bir üst seviyeyi seç ve başla', exact: true });
  await expect(button).toBeVisible();
  await expect(button).toBeDisabled();
  await page.getByRole('checkbox', { name: 'Uyarıyı okudum; bir üst seviyeyi seçmek istiyorum.' }).check();
  await button.click();
  await expect.poll(() => writes.some(item => item.path.endsWith('/confirm-next'))).toBe(true);
  expect(writes.find(item => item.path.endsWith('/confirm-next')).body).toEqual({ templateId: 'upper', sourceProgressId: 'old-program', assessmentAttemptId: 'post', overrideAcknowledged: true });
});

test('managed student requests staff approval instead of starting a program', async ({ page }) => {
  const { writes } = await setup(page, true, true);
  await page.goto('/student/dashboard');
  await page.getByRole('checkbox', { name: 'Uyarıyı okudum; bir üst seviyeyi seçmek istiyorum.' }).check();
  await page.getByRole('button', { name: 'Üst seviye için onay iste', exact: true }).click();
  await expect.poll(() => writes.some(item => item.path.endsWith('/request-next'))).toBe(true);
  expect(writes.some(item => item.path.endsWith('/confirm-next'))).toBe(false);
});

test('historical followup remains available during current training and uses its own cycle', async ({ page }) => {
  const { writes } = await setup(page, false);
  await page.goto('/student/dashboard');
  await expect(page.getByRole('heading', { name: 'Bekleyen takip ölçümleri', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Kalıcılık ölçümüne devam et', exact: true }).click();
  await expect.poll(() => writes.some(item => item.path.endsWith('/assessment/attempts'))).toBe(true);
  expect(writes.find(item => item.path.endsWith('/assessment/attempts')).body.programProgressId).toBe('old-program');
  expect(new URL(page.url()).searchParams.get('programProgressId')).toBe('old-program');
});

test('historical report separates measured and selected levels and warns about additional training', async ({ page }) => {
  const { comparisons } = await setup(page, false);
  await page.goto('/student/reports?programProgressId=old-program');
  await expect(page.getByText(/Seçilen eğitim seviyesi: 4/)).toBeVisible();
  await expect(page.getByText(/Takip ölçümleri yalnız eski programın etkisini göstermez/)).toBeVisible();
  expect(comparisons).toContain('old-program');
});
