import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
const exerciseId = '72000000-0000-4000-8000-000000000001';
const typeId = '72000000-0000-4000-8000-000000000002';
for (const failure of [false, true]) {
  test(`free reading completion ${failure ? 'reports save failure' : 'saves once and labels untested tempo'}`, async ({ page }) => {
    let completed = 0; const actions = [], errors = [];
    const content = Array(100).fill('kelime').join(' ');
    const configuration = { engineType: 'free_reading', engineConfig: { readingPurpose:'practice', display: { fontSize: 'large', lineHeight: 2.2 }, timing: { minReadingTimeMs: 500, maxReadingTimeMs:0, suggestedMinReadingTimeMs:120000,suggestedMaxReadingTimeMs:180000 } } };
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
    await page.route('**/api/**', async route => {
      const request = route.request(), path = new URL(request.url()).pathname; let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify({ sub: 'test-user', exp: Math.floor(Date.now()/1000)+3600, role: 'Student' })).toString('base64url')}.fixture`, roles: ['Student'] };
      else if (path.endsWith(`/exercises/${exerciseId}`)) response = { id: exerciseId, exerciseTypeId: typeId, exerciseTypeName: 'FreeReading', title: 'Serbest Okuma', difficultyLevel: 2, configurationJson: JSON.stringify(configuration) };
      else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'FreeReading', displayName: 'Serbest Okuma', engineType: 'free_reading', isActive: true }] };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      else if (path.endsWith('/exercise-sessions/start')) response = { sessionId: 'owned-free', exerciseId, totalSteps: 100, status: 'Active', configuration, initialData: { content, wordCount: 100, readingTextTitle: 'Kaynak metin', questions: [] } };
      else if (path.endsWith('/validate')) { actions.push(request.postDataJSON()); response = { isValid: true, isCorrect: true }; }
      else if (path.endsWith('/exercise-sessions/owned-free/complete')) {
        completed++;
        if (failure) { await route.fulfill({ status: 503, json: { message: 'Test storage unavailable' } }); return; }
        response = { score: null, accuracy: null, xpGained:0, rawWPM: 200, wordsRead: 100, comprehensionScore: null, weightedKDP: null, measurementStatus: 'NotMeasured',
          detailedResults: { readingStartTime:'2026-10-11T10:00:00Z',readingEndTime:'2026-10-11T10:00:32Z',readingPausedMilliseconds:2000 } };
      }
      await route.fulfill({ json: response });
    });
    await page.goto(`/student/exercises/universal-player/${exerciseId}`);
    await page.locator('.start-button-large').click();
    await expect(page.locator('.reading-text-content')).toHaveClass(/font-large/);
    await expect(page.locator('.reading-text-content')).toHaveCSS('line-height', /\d+px/);
    await expect(page.locator('.free-reading-guidance')).toContainText('2–3 dakika');
    await page.getByRole('button', { name: 'Okumayı Bitir' }).click();
    await expect.poll(() => completed).toBe(1);
    expect(actions.filter(a => a.action === 'start_reading')).toHaveLength(1);
    expect(actions.filter(a => a.action === 'finish_reading')).toHaveLength(1);
    if (!failure) {
      await expect(page.getByText('Anlama ölçülmedi; tempo, okumayı bitirdiğiniz beyanına dayanır.')).toBeVisible();
      await expect(page.locator('.wpm-box')).toContainText('200');
      await expect(page.getByText('Sorusuz okuma pratiği tamamlandı.',{exact:false})).toBeVisible();
      await expect(page.locator('.comprehension-question')).toHaveCount(0);
      await expect(page.locator('.stat-card').filter({ hasText:'Aktif okuma süresi' })).toContainText('00:30');
    } else await expect(page.getByText(/kaydedilemedi/i).first()).toBeVisible();
    expect(errors).toEqual([]);
  });
}

for (const [role, available] of [['Admin',true],['Teacher',true],['Admin',false]]) {
  test(`${role} question-free preview ${available ? 'uses real matching text without writes' : 'reports unavailable content without demo'}`,async({page})=>{
    const row=JSON.parse(readFileSync(new URL('../../../content-packs/free-reading/v1/content.json',import.meta.url)))
      .find(row=>row.age==='child' && row.level===1);
    const config={ engineType:'free_reading',engineConfig:{ readingPurpose:'practice',content:{source:'reading_text',minWordCount:150,maxWordCount:200},
      timing:{minReadingTimeMs:0,maxReadingTimeMs:0,suggestedMinReadingTimeMs:120000,suggestedMaxReadingTimeMs:120000} } };
    const writes=[],errors=[];
    page.on('pageerror',error=>errors.push(error.message));
    await page.addInitScript(()=>localStorage.setItem('currentUser',JSON.stringify({id:'preview-user'})));
    await page.route('**/api/**',async route=>{
      const request=route.request(),path=new URL(request.url()).pathname;
      if(request.method()!=='GET' && !path.endsWith('/auth/refresh-token')) writes.push(path);
      let response={ items:[],totalCount:0 };
      if(path.endsWith('/auth/refresh-token')) response={id:'preview-user',roles:[role],token:`e30.${Buffer.from(JSON.stringify({sub:'preview-user',exp:Math.floor(Date.now()/1000)+3600,role})).toString('base64url')}.fixture`};
      else if(path.endsWith(`/exercises/${exerciseId}`)) response={id:exerciseId,exerciseTypeId:typeId,exerciseTypeName:'FreeReading',title:'Serbest Okuma',difficultyLevel:1,targetAgeGroupConfigurationId:row.ageGroupId,configurationJson:JSON.stringify(config)};
      else if(path.endsWith('/exercise-types')) response={items:[{id:typeId,name:'FreeReading',engineType:'free_reading',isActive:true}]};
      else if(path.includes('/profile/status')) response={hasAgeGroupConfiguration:true};
      else if(path.endsWith('/my-modules')) response={hasSpeedReading:true};
      else if(path.endsWith('/reading-texts')) response=available ? [{...row,difficultyLevel:1,exerciseId,targetAgeGroupConfigurationId:row.ageGroupId,isActive:true}] : [];
      else if(path.endsWith(`/reading-texts/${row.id}`)) response={...row,difficultyLevel:1,exerciseId,targetAgeGroupConfigurationId:row.ageGroupId,questions:[{id:'source-question',questionText:'Kaynak soru'}]};
      await route.fulfill({json:response});
    });
    await page.goto(`/student/exercises/universal-player/${exerciseId}?mode=preview`);
    if(available) {
      await page.locator('.start-button-large').click();
      await expect(page.locator('.reading-text-content')).toContainText(row.content.split('\n')[0]);
      await page.getByRole('button',{name:'Okumayı Bitir'}).click();
      await expect(page.getByText('Önizleme sonucu — kaydedilmedi.')).toBeVisible();
      await expect(page.locator('.comprehension-question')).toHaveCount(0);
      await page.screenshot({path:`../../artifacts/free-reading-ui/${role}-${test.info().project.name}.png`,fullPage:true});
    } else {
      await expect(page.locator('.error-state')).toContainText('uygun yayımlanmış metin yüklenemedi');
      await expect(page.locator('.loading-state')).toHaveCount(0);
    }
    expect(writes).toEqual([]); expect(errors).toEqual([]);
  });
}
