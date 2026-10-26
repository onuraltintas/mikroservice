import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CoachingStudyPlanningService, GoalTarget } from '../../../core/services/coaching-study-planning.service';
import { StudentGoalTargetComponent } from './student-goal-target.component';

describe('StudentGoalTargetComponent', () => {
  it('validates and saves a net target in the selected student scope',async ()=>{
    const {component,fixture,service}=await setup();fixture.componentRef.setInput('staffStudentId','student');fixture.detectChanges(); await fixture.whenStable();await component.open();component.openNet();
    component.netExamCode='LGS';component.targetNet=21;component.saveNet();expect(service.saveGoalNetTarget).not.toHaveBeenCalled();
    component.targetNet=15;component.saveNet();expect(service.saveGoalNetTarget).toHaveBeenCalledWith('goal',3,'LGS',15,'student');
    expect(component.target()?.netTarget?.targetNet).toBe(15);
  });
  it('cancels old target loads when the selected student changes', async () => {
    const { component, fixture, service } = await setup();
    const pending = new Subject<GoalTarget>();
    service.getGoalTarget.mockReturnValueOnce(pending);
    fixture.componentRef.setInput('staffStudentId', 'old'); fixture.detectChanges(); await fixture.whenStable(); await component.open();
    fixture.componentRef.setInput('staffStudentId', 'new'); fixture.detectChanges(); await fixture.whenStable();
    expect(pending.observed).toBe(false);
    pending.next({ goalId: 'goal', version: 5, targetSchoolId: null, targetUniversityProgramId: null, canEdit: true });
    expect(component.target()).toBeNull(); component.clear();
    expect(service.saveGoalTarget).not.toHaveBeenCalled();
  });
  it('sends teacher target reads and writes in the selected student scope', async () => {
    const { component, fixture, service } = await setup();
    fixture.componentRef.setInput('staffStudentId', 'student');
    await component.open();
    expect(service.getGoalTarget).toHaveBeenCalledWith('goal', 'student');
    component.chooseVerified('school');
    expect(service.saveGoalTarget).toHaveBeenCalledWith('goal', 3, null, 'school', 'student');
    component.score = 450; component.maxScore = 500; component.examType = 'LGS'; component.saveScore();
    expect(service.saveGoalScoreTarget).toHaveBeenCalledWith('goal', 4, 450, 500, 'LGS', 'student');
  });
  it('shows the linked catalog name and warns when its record is inactive', async () => {
    const { component, fixture } = await setup();
    await component.open();
    component.target.set({ goalId: 'goal', version: 3, targetSchoolId: 'school', targetUniversityProgramId: null,
      canEdit: true, catalogTarget: { name: 'Science School', detail: 'Ankara / Center', isActive: false } });
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Science School');
    expect(fixture.nativeElement.textContent).toContain('Ankara / Center');
    expect(fixture.nativeElement.textContent).toContain('pasif');
  });
  async function setup(canEdit = true) {
    const target: GoalTarget = { goalId: 'goal', version: 3, targetSchoolId: null, targetUniversityProgramId: null, canEdit };
    const service = {
      getGoalTarget: vi.fn(() => of(target)),
      saveGoalScoreTarget: vi.fn(() => of({ ...target, version: 4 })),
      practiceProfiles: vi.fn(()=>of([{code:'LGS',name:'LGS',wrongAnswerDivisor:3,sections:[{code:'TUR',name:'Türkçe',questionCount:20}]}])),
      saveGoalNetTarget: vi.fn(()=>of({...target,version:4,netTarget:{examCode:'LGS',targetNet:15}})),
      saveGoalTarget: vi.fn(() => of({ ...target, version: 4, targetSchoolId: 'school' }))
    };
    TestBed.configureTestingModule({ imports: [StudentGoalTargetComponent], providers: [{ provide: CoachingStudyPlanningService, useValue: service }] });
    const fixture = TestBed.createComponent(StudentGoalTargetComponent);
    fixture.componentRef.setInput('goalId', 'goal');
    fixture.detectChanges(); await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, service };
  }
  it('loads lazily and saves the loaded version', async () => {
    const { component, service, fixture } = await setup();
    expect(service.getGoalTarget).not.toHaveBeenCalled();
    await component.open();
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('app-target-admission-search')).not.toBeNull();
    component.chooseVerified('school');
    expect(service.saveGoalTarget).toHaveBeenCalledWith('goal', 3, null, 'school');
    expect(component.target()?.version).toBe(4);
  });
  it('validates score configuration, saves its version and clears explicitly', async () => {
    const { component, service } = await setup();
    await component.open(); component.score = 400; component.maxScore = 500; component.examType = 'LGS';
    component.saveScore();
    expect(service.saveGoalScoreTarget).toHaveBeenCalledWith('goal', 3, 400, 500, 'LGS');
    component.clearScore();
    expect(service.saveGoalScoreTarget).toHaveBeenLastCalledWith('goal', 4, null, null, null);
    component.score = 501; component.maxScore = 500; component.saveScore();
    expect(service.saveGoalScoreTarget).toHaveBeenCalledTimes(2);
    expect(component.error()).toBeTruthy();
    component.score = 12.345; component.maxScore = 100; component.saveScore();
    component.score = 12; component.maxScore = 100.001; component.saveScore();
    expect(service.saveGoalScoreTarget).toHaveBeenCalledTimes(2);
  });
  it('protects score writes for teacher goals, stale versions and pending saves', async () => {
    const { component, service } = await setup(false);
    await component.open(); component.clearScore();
    expect(service.saveGoalScoreTarget).not.toHaveBeenCalled();
    component.target.set({ ...component.target()!, canEdit: true });
    service.saveGoalScoreTarget.mockReturnValueOnce(throwError(() => ({ status: 409 })));
    component.clearScore(); component.clearScore();
    expect(service.saveGoalScoreTarget).toHaveBeenCalledTimes(1);
    expect(component.stale()).toBe(true);
    await component.open(); component.target.set({ ...component.target()!, canEdit: true });
    const pending = new Subject<any>(); service.saveGoalScoreTarget.mockReturnValueOnce(pending);
    component.clearScore(); component.clearScore();
    expect(service.saveGoalScoreTarget).toHaveBeenCalledTimes(2);
    expect(await component.canLeavePage()).toBe(false);
    pending.complete(); expect(await component.canLeavePage()).toBe(true);
  });
  it('does not search or write teacher-created targets', async () => {
    const { component, service, fixture } = await setup(false);
    await component.open(); component.chooseVerified('school'); component.clear();
    expect(service.saveGoalTarget).not.toHaveBeenCalled();
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('öğretmen');
  });
  it('blocks writes on load failure and lets the student retry', async () => {
    const { component, service } = await setup();
    service.getGoalTarget.mockReturnValueOnce(throwError(() => ({ status: 503 })));
    await component.open(); component.clear();
    expect(component.target()).toBeNull();
    expect(service.saveGoalTarget).not.toHaveBeenCalled();
    await component.open();
    expect(component.target()).not.toBeNull();
  });
  it('keeps old data on 409 and disables writes until a reload', async () => {
    const { component, service } = await setup();
    await component.open();
    service.saveGoalTarget.mockReturnValueOnce(throwError(() => ({ status: 409 })));
    component.chooseVerified('school'); component.chooseVerified('school');
    expect(service.saveGoalTarget).toHaveBeenCalledTimes(1);
    expect(component.target()?.version).toBe(3);
    expect(component.error()).toContain('değişti');
    await component.open(); component.chooseVerified('school');
    expect(service.saveGoalTarget).toHaveBeenCalledTimes(2);
  });
  it('supports program filters and clears links explicitly', async () => {
    const { component, service } = await setup();
    await component.open(); component.changeKind('program'); component.chooseVerified('program');
    expect(service.saveGoalTarget).toHaveBeenCalledWith('goal', 3, 'program', null);
    component.clear();
    expect(service.saveGoalTarget).toHaveBeenCalledWith('goal', 4, null, null);
  });
  it('blocks duplicate saves and navigation while saving', async () => {
    const { component, service } = await setup();
    const pending = new Subject<any>();
    await component.open();
    service.saveGoalTarget.mockReturnValueOnce(pending);
    component.chooseVerified('school'); component.chooseVerified('school'); await component.open();
    expect(service.saveGoalTarget).toHaveBeenCalledTimes(1);
    expect(await component.canLeavePage()).toBe(false);
    pending.error({ status: 500 });
    expect(await component.canLeavePage()).toBe(true);
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
