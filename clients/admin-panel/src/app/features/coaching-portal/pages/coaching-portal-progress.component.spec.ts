import { DeferBlockBehavior } from '@angular/core/testing';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingPracticeAdaptationService } from '../../../core/services/coaching-practice-adaptation.service';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { BehaviorSubject } from 'rxjs';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { CoachingPortalService, ExamResult, Goal } from '../../../core/services/coaching-portal.service';
import { CoachingPortalProgressComponent, tomorrowInTurkey } from './coaching-portal-progress.component';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentGoalTargetComponent } from './student-goal-target.component';
import { By } from '@angular/platform-browser';
import { CoachingPracticeProgressService } from '../../../core/services/coaching-practice-progress.service';
import { PracticeProgressComponent } from './practice-progress.component';
import { CoachingWeeklyReviewService } from '../../../core/services/coaching-weekly-review.service';
import { DeferBlockState } from '@angular/core/testing';

describe('CoachingPortalProgressComponent', () => {
  const params = new BehaviorSubject(convertToParamMap({}));
  const profile = signal<UserProfile | null>(null);
  let service: {
    getStudentProgress: ReturnType<typeof vi.fn>;
    getStudentGoals: ReturnType<typeof vi.fn>;
    getStudentExamResults: ReturnType<typeof vi.fn>;
    createStudentGoal: ReturnType<typeof vi.fn>;
    updateGoalProgress: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    params.next(convertToParamMap({}));
    profile.set(user('Student'));
    service = {
      getStudentProgress: vi.fn(() => of({
        studentId: 'user-1', totalAssignments: 0, submittedAssignments: 0, gradedAssignments: 0,
        totalExams: 0, totalGoals: 0, completedGoals: 0, averageGoalProgress: 0,
        totalSessions: 0, upcomingSessions: 0, attendedSessions: 0
      })),
      getStudentGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
      getStudentExamResults: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
      createStudentGoal: vi.fn(() => of({ goalId: 'goal-1' })),
      updateGoalProgress: vi.fn(() => of({ message: 'ok' }))
    };

    await TestBed.configureTestingModule({deferBlockBehavior: DeferBlockBehavior.Manual,
      imports: [CoachingPortalProgressComponent],
      providers: [{provide:ActivatedRoute,useValue:{queryParamMap:params}}, {provide:CoachingPracticeAdaptationService,useValue:{get:vi.fn(()=>of({exams:[],advice:[]}))}},
        { provide: CoachingWeeklyReviewService, useValue: {get:vi.fn(()=>of({weekStart:'2026-10-12',today:'2026-10-20',reflection:null,summary:{scheduledTasks:0,completedTasks:0,plannedMinutes:0,actualMinutes:null},decisions:[],totalDecisions:0,decisionPage:1,canReflect:true,canDecide:false}))} },
        { provide: CoachingPracticeProgressService, useValue: {get: vi.fn(()=>of({fromDate:'2026-10-01',toDate:'2026-10-20',trends:[],goals:[],recovery:[]}))} },
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: CoachingStudyPlanningService, useValue: {} }
      ]
    }).compileComponents();
  });

  it('opens valid weekly query links in the work tab and blocks unsafe destinations and guarded edits',async()=>{
    params.next(convertToParamMap({week:'2026-10-12',decision:'a4cea155-5485-4db6-9f52-1ec4df8080c3'}));
    const fixture=TestBed.createComponent(CoachingPortalProgressComponent);fixture.detectChanges(); await fixture.whenStable();const c=fixture.componentInstance;
    expect(c.weeklyTarget()?.weekStart).toBe('2026-10-12');
    const child=fixture.debugElement.query(By.directive(PracticeProgressComponent)).componentInstance as PracticeProgressComponent;
    expect(child.activeTab()).toBe('work');const blocks=await fixture.getDeferBlocks();await blocks[1].render(DeferBlockState.Complete);fixture.detectChanges(); await fixture.whenStable();
    expect(TestBed.inject(CoachingWeeklyReviewService).get).toHaveBeenCalledWith(null,'2026-10-12',1,'a4cea155-5485-4db6-9f52-1ec4df8080c3');
    vi.spyOn(c,'canLeavePage').mockResolvedValue(false);
    params.next(convertToParamMap({week:'2026-10-05'}));expect(c.weeklyTarget()?.weekStart).toBe('2026-10-12');await vi.waitFor(()=>expect(c.errorMessage()).toContain('korunuyor'));
    params.next(convertToParamMap({week:'2026-10-13',decision:'../bad'}));expect(c.errorMessage()).toContain('geçersiz');
  });

  it('keeps exam targets out of habit goals without deleting the records', async () => {
    service.getStudentGoals.mockReturnValue(of({items:[{id:'goal-1',title:'School goal',category:'ExamPreparation',progress:0,isCompleted:false}],pageNumber:1,pageSize:100,totalCount:1}));
    const fixture=TestBed.createComponent(CoachingPortalProgressComponent);fixture.detectChanges();await fixture.whenStable();
    expect(fixture.componentInstance.goals()).toHaveLength(1);
    expect(fixture.componentInstance.habitGoals()).toHaveLength(0);
    expect(fixture.debugElement.query(By.directive(StudentGoalTargetComponent))).toBeNull();
  });

  it('keeps a goal form intact across tabs even when the separate practice report fails',async ()=>{
    const practiceService=TestBed.inject(CoachingPracticeProgressService);
    vi.mocked(practiceService.get).mockReturnValue(throwError(()=>({status:500})));
    const fixture=TestBed.createComponent(CoachingPortalProgressComponent);fixture.detectChanges(); await fixture.whenStable();
    fixture.componentInstance.toggleGoalForm();fixture.detectChanges(); await fixture.whenStable();
    const input=fixture.nativeElement.querySelector('[formControlName="title"]') as HTMLInputElement;
    input.value='Kaybolmaması gereken hedef';input.dispatchEvent(new Event('input'));fixture.detectChanges(); await fixture.whenStable();
    const tabs=Array.from(fixture.nativeElement.querySelectorAll('[role="tab"]')) as HTMLButtonElement[];
    tabs[1].click();fixture.detectChanges(); await fixture.whenStable();expect(input.closest('[role="tabpanel"]')?.hasAttribute('hidden')).toBe(true);
    tabs[0].click();fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[formControlName="title"]')).toBe(input);
    expect(input.value).toBe('Kaybolmaması gereken hedef');expect(fixture.componentInstance.goalForm.controls.title.value).toBe(input.value);
    expect(fixture.nativeElement.textContent).toContain('Rapor yüklenemedi');
  });

  it('does not invoke the leave confirmation while rendering a dirty topic analysis', async () => {
    service.getStudentGoals.mockReturnValue(of({ items: [{ id: 'goal-1', title: 'Weekly habit', category: 'StudyHabits', progress: 0, isCompleted: false }], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 }));
    const fixture = TestBed.createComponent(CoachingPortalProgressComponent);
    fixture.detectChanges(); await fixture.whenStable();
    const practice = fixture.debugElement.query(By.directive(PracticeProgressComponent)).componentInstance;
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    Object.assign(practice, { adaptation: { canRefresh: () => false, canLeavePage: () => TestBed.inject(ToasterService).confirm('Discard analysis?') } });
    fixture.changeDetectorRef.markForCheck();
    fixture.detectChanges(); await fixture.whenStable();
    expect(confirm).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('input[type="range"]').disabled).toBe(true);
    expect(await fixture.componentInstance.canLeavePage()).toBe(false);
    expect(confirm).toHaveBeenCalledTimes(1);
    confirm.mockRestore();
  });

  it('labels student-reported exam results separately from teacher records', async () => {
    service.getStudentExamResults.mockReturnValue(of({ items: [{ examId: 'self', examTitle: 'Self mock', examType: 'Mock',
      examDate: '2026-10-01T00:00:00Z', score: 80, maxScore: 100, source: 'StudentReported' }], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 }));
    const fixture = TestBed.createComponent(CoachingPortalProgressComponent); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Öğrenci beyanı');
    // T7: the old summary tiles are gone; Haftan's week summary replaces them.
    expect(fixture.nativeElement.textContent).not.toContain('Öğretmen sınavı sayısı');
    expect(fixture.nativeElement.querySelector('app-practice-progress')).not.toBeNull();
  });

  it('creates a self-managed goal with a generated idempotency key', async () => {
    const component = TestBed.createComponent(CoachingPortalProgressComponent).componentInstance;
    await component.ngOnInit();
    component.toggleGoalForm();
    component.goalForm.setValue({
      title: 'Matematik neti',
      category: 1,
      description: 'Haftalık tekrar',
      targetDate: '2030-01-01',
      targetScore: 80
    });

    await component.createGoal();

    expect(service.createStudentGoal).toHaveBeenCalledWith(
      'user-1',
      expect.objectContaining({ title: 'Matematik neti', category: 1, targetScore: 80 }),
      expect.any(String)
    );
    expect(component.showGoalForm()).toBe(false);
    // T7: the page no longer reads the old progress summary.
    expect(service.getStudentProgress).not.toHaveBeenCalled();
  });

  it('localizes named goal categories returned by the API and keeps legacy numeric categories readable', () => {
    const component = TestBed.createComponent(CoachingPortalProgressComponent).componentInstance;
    expect(component.categoryLabel('ExamPreparation')).toBe('Sınav hazırlığı');
    expect(component.categoryLabel('SubjectMastery')).toBe('Ders hakimiyeti');
    expect(component.categoryLabel('1')).toBe('Sınav hazırlığı');
  });

  it('unlocks goal progress after a failed update so the student can retry', async () => {
    const component = TestBed.createComponent(CoachingPortalProgressComponent).componentInstance;
    const goal: Goal = { id: 'goal-1', title: 'Matematik', category: 'SubjectMastery', progress: 20, isCompleted: false };
    service.updateGoalProgress.mockReturnValue(throwError(() => new Error('offline')));
    const input = document.createElement('input');
    input.value = '75';
    await component.updateProgress(goal, { target: input } as unknown as Event);
    expect(component.updatingGoalId()).toBeNull();
    expect(input.value).toBe('20');
  });

  it('loads the next goal page without losing previously loaded goals', async () => {
    const component = TestBed.createComponent(CoachingPortalProgressComponent).componentInstance;
    service.getStudentGoals.mockReturnValueOnce(of({ items: [{ id: 'first' }], pageNumber: 1, pageSize: 100, totalCount: 101, totalPages: 2 }))
      .mockReturnValueOnce(of({ items: [{ id: 'last' }], pageNumber: 2, pageSize: 100, totalCount: 101, totalPages: 2 }));
    await component.ngOnInit();
    component.loadMoreGoals();
    expect(service.getStudentGoals).toHaveBeenLastCalledWith('user-1', 2, 100);
    expect(component.goals().map(goal => goal.id)).toEqual(['first', 'last']);
  });

  it('updates only the selected goal progress after a successful save', async () => {
    const component = TestBed.createComponent(CoachingPortalProgressComponent).componentInstance;
    const goal: Goal = {
      id: 'goal-1',
      title: 'Matematik',
      category: 'SubjectMastery',
      progress: 20,
      isCompleted: false
    };
    component.goals.set([goal]);
    const input = document.createElement('input');
    input.value = '75';

    await component.updateProgress(goal, { target: input } as unknown as Event);

    expect(service.updateGoalProgress).toHaveBeenCalledWith('goal-1', 75);
    expect(component.goals()[0]).toMatchObject({ progress: 75, isCompleted: false });
  });

  it('loads the next result page without mixing the separate net trends', () => {
    const component = TestBed.createComponent(CoachingPortalProgressComponent).componentInstance;
    const first: ExamResult = { examId: 'exam-1', examTitle: 'İlk deneme', examDate: '2030-02-01', examType: 'Mock', score: 60, maxScore: 100 };
    const second: ExamResult = { examId: 'exam-2', examTitle: 'Son deneme', examDate: '2030-03-01', examType: 'Mock', score: 80, maxScore: 100 };
    component.examResults.set([second, first]);
    component.examPageNumber.set(1);
    component.examTotalPages.set(2);
    service.getStudentExamResults.mockReturnValue(of({ items: [second], pageNumber: 2, pageSize: 25, totalCount: 2, totalPages: 2 }));

    expect(component.scorePercentage(second)).toBe(80);
    component.loadMoreExams();

    expect(service.getStudentExamResults).toHaveBeenLastCalledWith('user-1', 2, 25);
    expect(component.examPageNumber()).toBe(2);
    expect(component.examResults()).toHaveLength(3);
  });
});

function user(role: string): UserProfile {
  return {
    id: 'user-1',
    email: 'student@example.test',
    firstName: 'Test',
    lastName: 'Student',
    username: 'student@example.test',
    roles: [role],
    role,
    permissions: []
  };
}

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });

beforeEach(() => TestBed.overrideProvider(CoachingStudentNavigationService, { useValue: { showAssignments: () => true, showSessions: () => true, refresh: () => {} } }));

// K4 (31.10.2026): the earliest goal date is tomorrow in Türkiye, also between 00:00 and 03:00 when UTC is still yesterday.
describe('goal date minimum', () => {
  it('counts tomorrow in Türkiye time', () => {
    expect(tomorrowInTurkey(new Date('2026-10-31T22:30:00Z'))).toBe('2026-11-02');
    expect(tomorrowInTurkey(new Date('2026-10-31T12:00:00Z'))).toBe('2026-11-01');
    expect(tomorrowInTurkey(new Date('2026-12-31T21:30:00Z'))).toBe('2027-01-02');
  });
});
