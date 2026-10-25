import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { AdaptationReport, AdjustmentPreview, CoachingPracticeAdaptationService } from '../../../core/services/coaching-practice-adaptation.service';
import { PracticeAdaptationComponent } from './practice-adaptation.component';
import { practiceExamName } from '../../../core/models/practice-exam';

const sample: AdaptationReport = {
  exams: [{ examId: 'exam', title: 'Deneme 3', examDate: '2026-10-19', source: 'StudentReported', fingerprint: 'exam-v1', canEdit: true,
    input: { examCode: 'LGS', sections: [{ sectionCode: 'MAT', correct: 10, wrong: 5, empty: 5 }] },
    analysis: { testIdentity: 'Yayıncı 3', topics: [{ sectionCode: 'MAT', topicId: 'topic', correct: 1, wrong: 3, empty: 1, reason: 'Knowledge', topicName: 'Çarpanlar', lessonName: 'Matematik' }] } }],
  advice: [{ examCode: 'LGS', source: 'StudentReported', examIds: ['one', 'two', 'exam'], topics: [{ evidence: { sectionCode: 'MAT', topicId: 'topic', correct: 3, wrong: 9, empty: 3, reason: 'Knowledge', appearances: 3, suggestedMinutes: 60 }, lessonName: 'Matematik', topicName: 'Çarpanlar', alreadyPlanned: false }] }]
};
const preview: AdjustmentPreview = { fingerprint: 'plan-v1', planId: 'plan', expectedVersion: 2, canApply: true, teacherApprovalRequired: false,
  previousDeadline: '2027-06-01', proposedDeadline: '2027-06-01', timeZoneId: 'Europe/Istanbul', message: 'Mevcut çalışmalar korunur.',
  slots: [{ topicId: 'topic', lessonId: 'lesson', lessonName: 'Matematik', topicName: 'Çarpanlar', date: '2026-10-21', startMinute: 1080, minutes: 30, evidenceKey: 'key' }], unscheduled: [],
  impact: [{ lessonName: 'Matematik', previousFinish: '2027-05-01', proposedFinish: '2027-05-01', addedMinutes: 30 }] };
async function setup() {
  const service = { get: vi.fn(() => of(structuredClone(sample))), topics: vi.fn(() => of([{ id: 'topic', lessonId: 'lesson', lessonName: 'Matematik', name: 'Çarpanlar', sectionCode: 'MAT' }])),
    save: vi.fn((..._args: unknown[]) => of(sample.exams[0])), preview: vi.fn(() => of(structuredClone(preview))), apply: vi.fn((..._args: unknown[]) => of({})) };
  TestBed.configureTestingModule({ imports: [PracticeAdaptationComponent], providers: [{ provide: CoachingPracticeAdaptationService, useValue: service }] });
  const fixture = TestBed.createComponent(PracticeAdaptationComponent); fixture.componentRef.setInput('today', '2026-10-20'); fixture.detectChanges(); await fixture.whenStable();
  return { fixture, component: fixture.componentInstance, service };
}
describe('PracticeAdaptationComponent', () => {
  for (const code of ['LGS','TYT','AYT_SAY','AYT_EA','AYT_SOZ','YDT_EN']) {
    for (const role of ['student','teacher']) {
      it(`preserves ${code} ${role} scope through analysis, three-exam advice, calendar and confirmed apply`,async ()=>{
        const {fixture,component,service}=await setup();
        const studentId=role==='teacher'?'assigned-student':null;
        fixture.componentRef.setInput('staffStudentId',studentId);fixture.detectChanges(); await fixture.whenStable();
        const report=structuredClone(sample);report.exams[0].input.examCode=code;report.advice[0].examCode=code;
        report.exams[0].sectionNames={MAT:'Matematik'};
        if(role==='teacher') { report.exams[0].source='TeacherRecorded';report.advice[0].source='TeacherRecorded'; }
        service.get.mockReturnValue(of(report));await component.load();
        const exam=component.data()!.exams[0];await component.open(exam);component.analysis.topics[0].lessonName=undefined;
        component.dirty=true;fixture.detectChanges(); await fixture.whenStable();
        expect(fixture.nativeElement.textContent).toContain(practiceExamName(code));
        expect(fixture.nativeElement.textContent).toContain('Matematik / Çarpanlar');
        component.saveAnalysis();expect(service.save.mock.calls[0][0]).toBe(studentId);
        expect(service.topics).toHaveBeenLastCalledWith(code,'MAT');
        const advice=component.data()!.advice[0];expect(advice.examIds).toHaveLength(3);
        await component.choose(advice);component.apply();expect(service.apply).not.toHaveBeenCalled();
        component.preview();fixture.detectChanges(); await fixture.whenStable();expect(fixture.nativeElement.querySelector('app-study-plan-calendar')).toBeTruthy();
        component.apply();expect(service.apply).not.toHaveBeenCalled();
        component.approved=true;component.apply();
        expect(service.apply).toHaveBeenCalledExactlyOnceWith(studentId,'plan-v1',expect.objectContaining({examCode:code,source:advice.source}),true);
      });
    }
  }
  it('shows evidence, names and unspecified counts without treating net as a topic diagnosis', async () => {
    const { fixture, component } = await setup(); await component.open(sample.exams[0]); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Çarpanlar'); expect(component.remaining('MAT', 'wrong')).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('konusu belirtilmemiş'); expect(component.validAnalysis()).toBe(true);
    component.analysis.topics[0].wrong = 6; expect(component.validAnalysis()).toBe(false);
  });
  it('saves analysis for the selected student and preserves the form on server rejection', async () => {
    const { component, fixture, service } = await setup(); fixture.componentRef.setInput('staffStudentId', 'student'); fixture.detectChanges(); await fixture.whenStable();
    await component.open(sample.exams[0]); component.dirty = true;
    service.save.mockReturnValueOnce(throwError(() => ({ status: 409 }))); component.saveAnalysis();
    expect(component.editing()).not.toBeNull(); expect(component.dirty).toBe(true); expect(component.error()).toContain('Sonuç değişti');
    expect(service.save.mock.calls[0][0]).toBe('student');
  });
  it('requires a calendar preview and explicit approval and sends the exact reviewed request once', async () => {
    const { component, service, fixture } = await setup(); const pending = new Subject<any>(); service.apply.mockReturnValueOnce(pending);
    await component.choose(sample.advice[0]); component.apply(); expect(service.apply).not.toHaveBeenCalled();
    component.preview(); fixture.detectChanges(); await fixture.whenStable(); expect(fixture.nativeElement.querySelector('app-study-plan-calendar')).not.toBeNull(); expect(component.calendar()?.impact[0].proposedFinish).toBe('2027-05-01');
    component.apply(); expect(service.apply).not.toHaveBeenCalled();
    component.approved = true; component.apply(); component.apply(); expect(service.apply).toHaveBeenCalledTimes(1);
    expect(await component.canLeavePage()).toBe(false);
    expect(service.apply.mock.calls[0][2]).toEqual({ examCode: 'LGS', source: 'StudentReported', endDate: '2026-11-03', allowDeadlineExtension: false, topics: [{ topicId: 'topic', minutes: 60 }] });
    pending.next({}); pending.complete(); await vi.waitFor(()=>expect(service.get).toHaveBeenCalledTimes(2)); expect(component.calendar()).toBeNull();
  });
  it('invalidates approval when duration changes or a stale preview is rejected', async () => {
    const { component, service } = await setup(); await component.choose(sample.advice[0]); component.preview(); component.approved = true;
    component.setMinutes('topic', 30); expect(component.calendar()).toBeNull(); expect(component.approved).toBe(false);
    component.preview(); component.approved = true; service.apply.mockReturnValueOnce(throwError(() => ({ status: 409 })));
    component.apply(); expect(component.calendar()).toBeNull(); expect(component.error()).toContain('yeniden önizle');
    component.approved = true; component.apply(); expect(service.apply).toHaveBeenCalledTimes(1);
  });
  it('never applies partial capacity, teacher-required previews or read-only operations', async () => {
    const { component, service } = await setup(); await component.choose(sample.advice[0]);
    service.preview.mockReturnValueOnce(of({ ...preview, canApply: false, unscheduled: [{ topicId: 'topic', minutes: 30 }] }));
    component.preview(); component.approved = true; component.apply(); expect(service.apply).not.toHaveBeenCalled();
    component.calendar.set({ ...preview, teacherApprovalRequired: true, canApply: false }); component.apply(); expect(service.apply).not.toHaveBeenCalled();
    component.readOnly = true; await component.open({ ...sample.exams[0], canEdit: false }); component.saveAnalysis(); expect(service.save).not.toHaveBeenCalled();
    component.calendar.set(preview); component.apply(); expect(service.apply).not.toHaveBeenCalled();
  });
  it('cancels old student and topic reads and keeps scopes isolated', async () => {
    const { component, fixture, service } = await setup(); const pending = new Subject<AdaptationReport>(); service.get.mockReturnValueOnce(pending);
    await component.load(); fixture.componentRef.setInput('staffStudentId', 'other'); fixture.detectChanges(); await fixture.whenStable(); expect(pending.observed).toBe(false);
    expect(service.get).toHaveBeenLastCalledWith('other');
    const topics = new Subject<any[]>(); service.topics.mockReturnValueOnce(topics); await component.open(sample.exams[0]);
    await component.close(); expect(topics.observed).toBe(false);
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
