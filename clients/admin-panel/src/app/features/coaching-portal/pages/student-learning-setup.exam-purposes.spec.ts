import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { StudentLearningSetupComponent } from './student-learning-setup.component';
import { CoachingLearningSetupService, LearningCatalog, LearningSetupUpdate, LearningSetupView } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';

// KM1 (01.11.2026): AGS (+ÖABT), YDS and YÖKDİL preparation in "Çalışma amacım", with the target session where an exam has several.
describe('StudentLearningSetupComponent exam purposes', () => {
  const sessions = [
    { id: 'ags-2027', examCode: 'AGS', examName: 'Akademi Giriş Sınavı', date: '2027-05-08', label: '2027', applicationStart: '2027-03-19', applicationEnd: '2027-03-29' },
    { id: 'yds-1', examCode: 'YDS', examName: 'YDS', date: '2027-03-28', label: '2027/1', applicationStart: '2027-02-03', applicationEnd: '2027-02-11' },
    { id: 'yds-2', examCode: 'YDS', examName: 'YDS', date: '2027-11-21', label: '2027/2', applicationStart: null, applicationEnd: null }
  ];
  const empty: LearningSetupView = { context: null, nextStep: 'Purpose', isReady: false, missingFields: ['Purpose'], warnings: [], examSessions: sessions };
  async function setup(view = empty) {
    const service = { get: vi.fn(() => of(view)), save: vi.fn((_request: LearningSetupUpdate) => of(view)),
      catalog: vi.fn(() => of({ curriculum: null, lessons: [], topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] } as LearningCatalog)) };
    TestBed.configureTestingModule({ imports: [StudentLearningSetupComponent], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
      { provide: CoachingLearningSetupService, useValue: service }, { provide: CoachingStudyPlanningService, useValue: { getTopicOutcomes: vi.fn() } }] });
    const fixture = TestBed.createComponent(StudentLearningSetupComponent); fixture.detectChanges(); await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, service };
  }
  const render = async (fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) => { fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); };

  it('offers AGS / ÖABT, YDS and YÖKDİL preparation next to LGS and YKS', async () => {
    const { fixture } = await setup();
    const options = [...fixture.nativeElement.querySelectorAll('select[name="purpose"] option')].map((x: HTMLOptionElement) => x.textContent?.trim());
    expect(options).toEqual(['Okul derslerimi desteklemek', 'LGS hazırlığı', 'YKS hazırlığı', 'AGS / ÖABT hazırlığı', 'YDS hazırlığı',
      'YÖKDİL hazırlığı', 'Genel çalışma', 'Henüz karar vermedim']);
  });

  it('lets a YDS student pick one of the sessions and saves English as the language', async () => {
    const { component, fixture, service } = await setup();
    component.purpose = 'Yds'; component.changed(); await render(fixture);
    expect(component.sessionChoices().map(x => x.id)).toEqual(['yds-1', 'yds-2']);
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select[name="examSession"]');
    expect([...select.options].map(x => x.textContent?.trim())).toEqual(['Henüz seçmedim', '2027/1 · 28.03.2027 · başvuru 03.02–11.02.2027', '2027/2 · 21.11.2027']);
    expect(fixture.nativeElement.textContent).toContain('Sınav dili: İngilizce');
    component.selectSession('yds-2');
    expect(component.targetExamYear).toBe(2027);
    component.savePurpose();
    expect(service.save).toHaveBeenCalledWith(expect.objectContaining({ purpose: 'Yds', targetExamYear: 2027, targetExamSessionId: 'yds-2',
      primaryScoreType: null, foreignLanguage: 'English' }));
  });

  it('asks YÖKDİL students for their field before saving', async () => {
    const { component, fixture, service } = await setup();
    component.purpose = 'Yokdil'; component.targetExamYear = 2027; component.changed(); await render(fixture);
    const fields = [...fixture.nativeElement.querySelectorAll('select[name="yokdilField"] option')].map((x: HTMLOptionElement) => x.textContent?.trim());
    expect(fields).toEqual(['Seç', 'Fen Bilimleri', 'Sağlık Bilimleri', 'Sosyal Bilimler']);
    component.savePurpose(); expect(service.save).not.toHaveBeenCalled();
    component.yokdilField = 'YOKDIL_SAG'; component.savePurpose();
    expect(service.save).toHaveBeenCalledWith(expect.objectContaining({ purpose: 'Yokdil', primaryScoreType: 'YOKDIL_SAG', foreignLanguage: 'English', targetExamSessionId: null }));
  });

  it('saves AGS with or without an ÖABT field and shows no session choice for a single session', async () => {
    const { component, fixture, service } = await setup();
    component.purpose = 'Ags'; component.targetExamYear = 2027; component.changed(); await render(fixture);
    expect(fixture.nativeElement.querySelector('select[name="examSession"]')).toBeNull();
    const fields = [...fixture.nativeElement.querySelectorAll('select[name="oabtField"] option')].map((x: HTMLOptionElement) => x.textContent?.trim());
    expect(fields.length).toBe(18);
    expect(fields[0]).toBe('ÖABT’ye girmeyeceğim');
    expect(fields).toContain('Özel Eğitim');
    component.savePurpose();
    expect(service.save).toHaveBeenLastCalledWith(expect.objectContaining({ purpose: 'Ags', primaryScoreType: null, foreignLanguage: null }));
    component.oabtField = 'OABT_SINIF'; component.savePurpose();
    expect(service.save).toHaveBeenLastCalledWith(expect.objectContaining({ purpose: 'Ags', primaryScoreType: 'OABT_SINIF' }));
  });

  it('prefills a saved YDS session and keeps LGS/YKS requests unchanged', async () => {
    const context = { id: 'ctx', version: 2, academicYearStart: 2026, purpose: 'Yds' as const, targetExamYear: 2027, targetExamSessionId: 'yds-1',
      primaryScoreType: null, foreignLanguage: 'English', curriculumVersionId: null, coursePreferences: [], topicStates: [], startingStatusReviewed: false };
    const { component, service } = await setup({ ...empty, context });
    expect(component.targetExamSessionId).toBe('yds-1');
    component.purpose = 'Yks'; component.primaryScoreType = 'SAY'; component.savePurpose();
    expect(service.save).toHaveBeenCalledWith(expect.objectContaining({ purpose: 'Yks', primaryScoreType: 'SAY', foreignLanguage: null, targetExamSessionId: null }));
  });
});
