import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting,HttpTestingController} from '@angular/common/http/testing';
import {CoachingTeacherStudyOverviewComponent} from './coaching-teacher-study-overview.component';

describe('teacher study overview',()=>{
  it('validates dates, preserves a dirty plan on back, and refreshes after leaving',async()=>{
    TestBed.configureTestingModule({imports:[CoachingTeacherStudyOverviewComponent],providers:[provideHttpClient(),provideHttpClientTesting()]});
    const f=TestBed.createComponent(CoachingTeacherStudyOverviewComponent),http=TestBed.inject(HttpTestingController);f.detectChanges();
    const data={items:[{studentId:'s',fullName:'Öğrenci Test',hasActivePlan:false,hasDraft:false,completedTasks:0,pendingTasks:1,overdueTasks:1,actualMinutes:0,tasks:[{taskId:'task',planId:'old-plan',title:'Geciken konu',plannedDate:'2026-10-01',plannedStartMinute:null,plannedMinutes:30,isCompleted:false,isOverdue:true,actualMinutes:null}]}],totalCount:1,pageNumber:1,pageSize:25};
    http.expectOne(r=>r.url.endsWith('/study-overview')).flush(data);f.detectChanges();
    const c=f.componentInstance;c.display='overdue';expect(c.workItems()[0].task.title).toBe('Geciken konu');c.open(data.items[0],'overdue');f.detectChanges();http.expectOne(r=>r.url.includes('/study/plans')).flush({data:{items:[],totalCount:0,pageNumber:1,pageSize:20}});http.expectOne(r=>r.params.get('status')==='Active').flush({data:{items:[],totalCount:0,pageNumber:1,pageSize:20}});f.detectChanges();
    (f.nativeElement.querySelector('app-student-study-plans .page-heading button') as HTMLButtonElement).click();f.detectChanges();
    const confirm=vi.spyOn(window,'confirm').mockReturnValue(false);c.back();expect(c.detail()).not.toBeNull();confirm.mockReturnValue(true);c.back();expect(c.detail()).toBeNull();
    http.expectOne(r=>r.url.endsWith('/study-overview')).flush(data);confirm.mockRestore();c.fromDate='2030-01-02';c.toDate='2030-01-01';c.load();expect(c.error()).toContain('tarih');http.verify();
  });
  it('shows scoped bulk totals, exposes student plan management and recovers on retry',async()=>{
    TestBed.configureTestingModule({imports:[CoachingTeacherStudyOverviewComponent],providers:[provideHttpClient(),provideHttpClientTesting()]});
    const f=TestBed.createComponent(CoachingTeacherStudyOverviewComponent),http=TestBed.inject(HttpTestingController);f.detectChanges();
    http.expectOne(r=>r.url.endsWith('/coaching/teachers/me/study-overview')).flush({items:[{studentId:'student',fullName:'Öğrenci Test',hasActivePlan:true,hasDraft:false,completedTasks:2,pendingTasks:3,overdueTasks:1,actualMinutes:45}],totalCount:1,pageNumber:1,pageSize:25});
    f.detectChanges();expect(f.nativeElement.textContent).toContain('Öğrenci Test');expect(f.nativeElement.textContent).toContain('Geciken');expect(f.nativeElement.textContent).toContain('45');
    f.componentInstance.load();http.expectOne(r=>r.url.endsWith('/coaching/teachers/me/study-overview')).flush({},{status:403,statusText:'Forbidden'});f.detectChanges();expect(f.nativeElement.querySelector('[role=alert]')).not.toBeNull();
    f.componentInstance.load();http.expectOne(r=>r.url.endsWith('/coaching/teachers/me/study-overview')).flush({items:[],totalCount:0,pageNumber:1,pageSize:25});f.detectChanges();expect(f.nativeElement.textContent).toContain('öğrenci bulunamadı');http.verify();
  });
});
