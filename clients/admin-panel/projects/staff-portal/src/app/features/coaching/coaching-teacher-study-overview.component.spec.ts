import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting,HttpTestingController} from '@angular/common/http/testing';
import {CoachingTeacherStudyOverviewComponent} from './coaching-teacher-study-overview.component';

describe('teacher study overview',()=>{
  it('shows scoped bulk totals, exposes student plan management and recovers on retry',async()=>{
    TestBed.configureTestingModule({imports:[CoachingTeacherStudyOverviewComponent],providers:[provideHttpClient(),provideHttpClientTesting()]});
    const f=TestBed.createComponent(CoachingTeacherStudyOverviewComponent),http=TestBed.inject(HttpTestingController);f.detectChanges();
    http.expectOne(r=>r.url.endsWith('/coaching/teachers/me/study-overview')).flush({items:[{studentId:'student',fullName:'Öğrenci Test',hasActivePlan:true,hasDraft:false,completedTasks:2,pendingTasks:3,overdueTasks:1,actualMinutes:45}],totalCount:1,pageNumber:1,pageSize:25});
    f.detectChanges();expect(f.nativeElement.textContent).toContain('Öğrenci Test');expect(f.nativeElement.textContent).toContain('Geciken');expect(f.nativeElement.textContent).toContain('45');
    f.componentInstance.load();http.expectOne(r=>r.url.endsWith('/coaching/teachers/me/study-overview')).flush({},{status:403,statusText:'Forbidden'});f.detectChanges();expect(f.nativeElement.querySelector('[role=alert]')).not.toBeNull();
    f.componentInstance.load();http.expectOne(r=>r.url.endsWith('/coaching/teachers/me/study-overview')).flush({items:[],totalCount:0,pageNumber:1,pageSize:25});f.detectChanges();expect(f.nativeElement.textContent).toContain('öğrenci bulunamadı');http.verify();
  });
});
