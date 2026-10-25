import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentAutomaticPlanComponent } from './student-automatic-plan.component';

describe('StudentAutomaticPlanComponent', () => {
  async function setup() {
    const topic = { id: 'topic', name: 'Fractions', lessonName: 'Mathematics', unitName: 'Numbers', gradeNumber: 8, examCode: 'LGS', estimatedMinutes: null };
    const preview = { availabilityVersion: 3, timeZoneId: 'Europe/Istanbul', activeRevisionId: null, activeRevisionVersion: null,
      protectedTasks: [], schedule: { tasks: [{ topicId: 'topic', plannedDate: '2026-10-05', plannedMinutes: 30 }],
        unscheduledTopics: [{ topicId: 'topic', remainingMinutes: 15 }], availableMinutes: 30, scheduledMinutes: 30, unscheduledMinutes: 15, unusedMinutes: 0 } };
    const service = { getAvailability: vi.fn(() => of({ version: 3, timeZoneId: 'Europe/Istanbul', windows: [] })),
      searchTopics: vi.fn(() => of({ items: [topic], totalCount: 25, pageNumber: 1, pageSize: 20 })),
      previewAutomatic: vi.fn(() => of(preview)) };
    const saveAutomaticDraft = vi.fn(() => of({ id: 'draft', version: 0, title: 'My plan', status: 'Draft' as const, tasks: [] }));
    Object.assign(service, { saveAutomaticDraft });
    TestBed.configureTestingModule({ imports: [StudentAutomaticPlanComponent], providers: [provideRouter([]), { provide: CoachingStudyPlanningService, useValue: service }] });
    const fixture = TestBed.createComponent(StudentAutomaticPlanComponent);
    fixture.detectChanges(); await fixture.whenStable();
    return { component: fixture.componentInstance, fixture, service: { ...service, saveAutomaticDraft }, topic, preview };
  }
  it('requires explicit minutes for unknown duration and sends the loaded availability version', async () => {
    const { component, service, fixture } = await setup();
    component.searchTopics(); component.addTopic('unknown');
    expect(component.selected.length).toBe(0);
    component.addTopic('topic'); component.startDate = '2026-10-05';
    component.generate();
    expect(service.previewAutomatic).not.toHaveBeenCalled();
    component.selected[0].minutes = 45; component.changed(); component.generate();
    expect(service.previewAutomatic).toHaveBeenCalledWith({ startDate: '2026-10-05', days: 7, expectedAvailabilityVersion: 3, topics: [{ topicId: 'topic', requiredMinutes: 45 }] });
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Fractions');
    expect(fixture.nativeElement.textContent).toContain('15 dk');
    expect(fixture.nativeElement.textContent).toContain('Henüz kaydedilmedi');
    component.days = 14; component.changed();
    expect(component.preview()).toBeNull();
  });
  it('does not preview without hours and supports reload after failure', async () => {
    const { component, service } = await setup();
    service.getAvailability.mockReturnValueOnce(throwError(() => ({ status: 404 })));
    component.loadHours(); component.generate();
    expect(component.availability()).toBeNull();
    expect(service.previewAutomatic).not.toHaveBeenCalled();
    component.loadHours();
    expect(component.availability()?.version).toBe(3);
  });
  it('blocks parallel requests and requires fresh hours after a conflict', async () => {
    const { component, service } = await setup();
    component.searchTopics(); component.addTopic('topic'); component.selected[0].minutes = 30;
    component.startDate = '2026-10-05';
    const pending = new Subject<any>();
    service.previewAutomatic.mockReturnValueOnce(pending);
    component.generate(); component.generate(); component.removeTopic('topic');
    expect(service.previewAutomatic).toHaveBeenCalledTimes(1);
    expect(component.selected.length).toBe(1);
    pending.error({ status: 409 });
    component.generate();
    expect(service.previewAutomatic).toHaveBeenCalledTimes(1);
    component.loadHours(); component.generate();
    expect(service.previewAutomatic).toHaveBeenCalledTimes(2);
  });
  it('does not duplicate topics and validates dates and scheduling horizon', async () => {
    const { component, service } = await setup();
    component.searchTopics(); component.addTopic('topic'); component.addTopic('topic');
    expect(component.selected.length).toBe(1);
    component.selected[0].minutes = 30; component.startDate = '2026-02-30'; component.generate();
    component.startDate = '2026-10-05'; component.days = 91; component.generate();
    expect(service.previewAutomatic).not.toHaveBeenCalled();
    component.removeTopic('topic'); expect(component.selected.length).toBe(0);
  });
  it('saves only after confirmation and blocks repeat submissions and leaving during the write', async () => {
    const { component, service, preview } = await setup();
    component.searchTopics(); component.addTopic('topic'); component.selected[0].minutes = 30;
    component.startDate = '2026-10-05'; component.title = 'My plan'; component.generate();
    component.saveDraft(); expect(service.saveAutomaticDraft).not.toHaveBeenCalled();
    const pending = new Subject<any>(); service.saveAutomaticDraft.mockReturnValueOnce(pending);
    component.saveConfirmed = true; component.saveDraft(); component.saveDraft();
    expect(service.saveAutomaticDraft).toHaveBeenCalledWith({ title: 'My plan',
      preview: { startDate: '2026-10-05', days: 7, expectedAvailabilityVersion: 3, topics: [{ topicId: 'topic', requiredMinutes: 30 }] },
      expectedActiveRevisionId: preview.activeRevisionId, expectedActiveRevisionVersion: preview.activeRevisionVersion });
    expect(service.saveAutomaticDraft).toHaveBeenCalledTimes(1);
    expect(await component.canLeavePage()).toBe(false);
    pending.next({ id: 'draft', version: 0, title: 'My plan', status: 'Draft', tasks: [] }); pending.complete();
    expect(component.savedPlan()?.id).toBe('draft');
    expect(await component.canLeavePage()).toBe(true);
    component.saveDraft(); expect(service.saveAutomaticDraft).toHaveBeenCalledTimes(1);
  });
  it('requires a new confirmation for each regenerated preview', async () => {
    const { component, service } = await setup();
    component.searchTopics(); component.addTopic('topic'); component.selected[0].minutes = 30;
    component.startDate = '2026-10-05'; component.title = 'My plan'; component.generate();
    component.saveConfirmed = true; component.generate();
    expect(component.saveConfirmed).toBe(false);
    component.saveDraft(); expect(service.saveAutomaticDraft).not.toHaveBeenCalled();
  });
  it('sends the completed-evidence fingerprint with confirmation and shows the deducted scope',async()=>{
    const{component,service,fixture,preview}=await setup();
    component.preview.set({...preview,evidenceFingerprint:'completed-evidence',completedOutcomeMinutes:30});fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('30 dakikalık kapsamı kalan yükten düşüldü');
    component.title='Kalan kapsam';component.saveConfirmed=true;component.saveDraft();
    expect(service.saveAutomaticDraft).toHaveBeenCalledWith(expect.objectContaining({expectedEvidenceFingerprint:'completed-evidence'}));
  });
  it('does not offer or create an empty draft when all selected coverage is completed',async()=>{
    const{component,service,fixture,preview}=await setup();
    component.preview.set({...preview,completedOutcomeMinutes:60,schedule:{...preview.schedule,tasks:[],scheduledMinutes:0,unscheduledMinutes:0,unscheduledTopics:[]}});component.saveConfirmed=true;fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('planlanacak yeni veya devreden çalışma kalmadı');expect(fixture.nativeElement.textContent).not.toContain('Taslak olarak kaydet');
    component.saveDraft();expect(service.saveAutomaticDraft).not.toHaveBeenCalled();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
