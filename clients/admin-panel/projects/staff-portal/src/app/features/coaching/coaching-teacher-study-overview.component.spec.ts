import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import {TestBed,DeferBlockState,DeferBlockBehavior} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting,HttpTestingController} from '@angular/common/http/testing';
import {provideRouter} from '@angular/router';
import {By} from '@angular/platform-browser';
import {CoachingTeacherStudyOverviewComponent} from './coaching-teacher-study-overview.component';
import {StudentStudyPlanAssistantComponent} from '../../../../../../src/app/features/coaching-portal/pages/student-study-plan-assistant.component';

describe('teacher study overview',()=>{
  it('filters combined work by state and opens the original revision even for archived work',async ()=>{
    await TestBed.configureTestingModule({deferBlockBehavior:DeferBlockBehavior.Manual,imports:[CoachingTeacherStudyOverviewComponent],providers:[provideHttpClient(),provideHttpClientTesting()]}).compileComponents();
    const c=TestBed.createComponent(CoachingTeacherStudyOverviewComponent).componentInstance;
    const base={plannedDate:'2027-01-04',plannedStartMinute:null,plannedMinutes:30,actualMinutes:null};
    c.data.set({items:[{studentId:'s',fullName:'Test',hasActivePlan:true,hasDraft:false,completedTasks:1,pendingTasks:2,overdueTasks:1,actualMinutes:12,tasks:[{...base,taskId:'a',planId:'active',title:'Geciken',isCompleted:false,isOverdue:true},{...base,taskId:'b',planId:'active',title:'Bekleyen',isCompleted:false,isOverdue:false},{...base,taskId:'c',planId:'archived',title:'Tamamlanan',isCompleted:true,isOverdue:false,actualMinutes:12}]}],totalCount:1,pageNumber:1,pageSize:25});
    expect(c.workItems()).toHaveLength(3);c.display='pending';expect(c.workItems()).toHaveLength(2);c.display='overdue';expect(c.workItems()).toHaveLength(1);c.display='completed';expect(c.workItems()[0].task.actualMinutes).toBe(12);(await c.openWork(c.workItems()[0]));expect(c.detail()?.planId).toBe('archived');expect(c.detail()?.filter).toBe('completed');
  });
  it('validates dates, preserves a dirty plan on back, and refreshes after leaving',async()=>{
    await TestBed.configureTestingModule({deferBlockBehavior:DeferBlockBehavior.Manual,imports:[CoachingTeacherStudyOverviewComponent],providers:[provideHttpClient(),provideHttpClientTesting()]}).compileComponents();
    const f=TestBed.createComponent(CoachingTeacherStudyOverviewComponent),http=TestBed.inject(HttpTestingController);f.detectChanges();
    const data={items:[{studentId:'s',fullName:'Öğrenci Test',hasActivePlan:false,hasDraft:false,completedTasks:0,pendingTasks:1,overdueTasks:1,actualMinutes:0,tasks:[{taskId:'task',planId:'old-plan',title:'Geciken konu',plannedDate:'2026-10-01',plannedStartMinute:null,plannedMinutes:30,isCompleted:false,isOverdue:true,actualMinutes:null}]}],totalCount:1,pageNumber:1,pageSize:25};
    http.expectOne(r=>r.url.endsWith('/study-overview')).flush(data);f.detectChanges();
    const c=f.componentInstance;c.display='overdue';expect(c.workItems()[0].task.title).toBe('Geciken konu');(await c.open(data.items[0],'overdue'));f.detectChanges();http.expectOne(r=>r.url.includes('/study/plans')).flush({data:{items:[],totalCount:0,pageNumber:1,pageSize:20}});http.expectOne(r=>r.params.get('status')==='Active').flush({data:{items:[],totalCount:0,pageNumber:1,pageSize:20}});f.detectChanges();
    (f.nativeElement.querySelector('app-student-study-plans .page-heading button') as HTMLButtonElement).click();await vi.waitFor(()=>expect(f.nativeElement.querySelector('app-student-study-plans input[name=planTitle]')).not.toBeNull());f.detectChanges();
    const confirm=vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);(await c.back());expect(c.detail()).not.toBeNull();confirm.mockResolvedValue(true);(await c.back());expect(c.detail()).toBeNull();
    http.expectOne(r=>r.url.endsWith('/study-overview')).flush(data);confirm.mockRestore();c.fromDate='2030-01-02';c.toDate='2030-01-01';(await c.load());expect(c.error()).toContain('tarih');http.verify();
  });
  it('shows scoped bulk totals, exposes student plan management and recovers on retry',async()=>{
    await TestBed.configureTestingModule({deferBlockBehavior:DeferBlockBehavior.Manual,imports:[CoachingTeacherStudyOverviewComponent],providers:[provideHttpClient(),provideHttpClientTesting()]}).compileComponents();
    const f=TestBed.createComponent(CoachingTeacherStudyOverviewComponent),http=TestBed.inject(HttpTestingController);f.detectChanges();
    http.expectOne(r=>r.url.endsWith('/coaching/teachers/me/study-overview')).flush({items:[{studentId:'student',fullName:'Öğrenci Test',hasActivePlan:true,hasDraft:false,completedTasks:2,pendingTasks:3,overdueTasks:1,actualMinutes:45}],totalCount:1,pageNumber:1,pageSize:25});
    f.detectChanges();expect(f.nativeElement.textContent).toContain('Öğrenci Test');expect(f.nativeElement.textContent).toContain('Geciken');expect(f.nativeElement.textContent).toContain('45');
    f.componentInstance.load();http.expectOne(r=>r.url.endsWith('/coaching/teachers/me/study-overview')).flush({},{status:403,statusText:'Forbidden'});f.detectChanges();expect(f.nativeElement.querySelector('[role=alert]')).not.toBeNull();
    f.componentInstance.load();http.expectOne(r=>r.url.endsWith('/coaching/teachers/me/study-overview')).flush({items:[],totalCount:0,pageNumber:1,pageSize:25});f.detectChanges();expect(f.nativeElement.textContent).toContain('öğrenci bulunamadı');http.verify();
  });
  it('opens the automatic wizard for the selected student and loads that student scoped setup',async ()=>{
    await TestBed.configureTestingModule({deferBlockBehavior:DeferBlockBehavior.Manual,imports:[CoachingTeacherStudyOverviewComponent],providers:[provideHttpClient(),provideHttpClientTesting(),provideRouter([])]}).compileComponents();
    const f=TestBed.createComponent(CoachingTeacherStudyOverviewComponent),http=TestBed.inject(HttpTestingController),c=f.componentInstance;
    c.detail.set({student:{studentId:'student-42',fullName:'Seçili Öğrenci',hasActivePlan:false,hasDraft:false,completedTasks:0,pendingTasks:0,overdueTasks:0,actualMinutes:0},filter:'all'});
    (await c.createWithWizard());f.detectChanges();http.expectOne(r=>r.url.endsWith('/coaching/teachers/me/study-overview')).flush({items:[],totalCount:0,pageNumber:1,pageSize:25});
    const deferred=await f.getDeferBlocks();await deferred[0].render(DeferBlockState.Complete);f.detectChanges();expect(f.nativeElement.textContent).toContain('Öğrenci için plan hazırla');
    expect(f.debugElement.query(By.directive(StudentStudyPlanAssistantComponent)).componentInstance.staffStudentId).toBe('student-42');
    http.expectOne(r=>r.url.endsWith('/coaching/staff/students/student-42/study/weekly-draft')).flush({data:{draft:null,lessons:[]}});
    http.expectOne(r=>r.url.endsWith('/coaching/staff/students/student-42/study/learning-setup')).flush({data:{context:null,nextStep:'Profile',isReady:false,missingFields:[],warnings:[]}});
    f.detectChanges();expect(f.nativeElement.querySelectorAll('a[href*="/coaching-portal"]').length).toBe(0);
    http.verify();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
