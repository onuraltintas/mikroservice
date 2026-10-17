import {CommonModule} from '@angular/common';
import {Component,DestroyRef,ViewChild,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {Subscription,finalize} from 'rxjs';
import {CoachingStudyPlanningService,TeacherStudyOverview,TeacherStudyStudent} from '../../../../../../src/app/core/services/coaching-study-planning.service';
import {StudentStudyPlansComponent} from '../../../../../../src/app/features/coaching-portal/pages/student-study-plans.component';
import {dateKey} from '../../../../../../src/app/features/coaching-portal/pages/study-plan-calendar';

@Component({selector:'staff-coaching-teacher-study-overview',standalone:true,imports:[CommonModule,FormsModule,StudentStudyPlansComponent],templateUrl:'./coaching-teacher-study-overview.component.html',styleUrl:'./coaching-teacher-study-overview.component.scss'})
export class CoachingTeacherStudyOverviewComponent{
  private readonly api=inject(CoachingStudyPlanningService);private readonly destroyRef=inject(DestroyRef);private request?:Subscription;
  @ViewChild(StudentStudyPlansComponent) private editor?:StudentStudyPlansComponent;
  readonly data=signal<TeacherStudyOverview>({items:[],totalCount:0,pageNumber:1,pageSize:25});readonly loading=signal(false);readonly error=signal('');
  readonly detail=signal<{student:TeacherStudyStudent;filter:'all'|'pending'|'completed'|'overdue'}|null>(null);
  fromDate=dateKey(new Date(Date.now()-7*86400000));toDate=dateKey(new Date(Date.now()+7*86400000));search='';
  ngOnInit(){this.load();}
  canLeavePage(){return this.editor?.canLeavePage()??true;}
  open(student:TeacherStudyStudent,filter:'all'|'pending'|'completed'|'overdue'='all'){if(this.canLeavePage())this.detail.set({student,filter});}
  back(){if(!this.canLeavePage())return;this.detail.set(null);this.load(this.data().pageNumber);}
  load(page=1){
    this.request?.unsubscribe();this.error.set('');
    if(!this.fromDate||!this.toDate||this.fromDate>this.toDate){this.error.set('Geçerli bir tarih aralığı seçin.');return;}
    this.loading.set(true);this.request=this.api.teacherOverview(this.fromDate,this.toDate,page,this.search).pipe(takeUntilDestroyed(this.destroyRef),finalize(()=>this.loading.set(false))).subscribe({next:r=>this.data.set(r),error:e=>{this.data.set({items:[],totalCount:0,pageNumber:1,pageSize:25});this.error.set(e.error?.message??'Çalışma takibi yüklenemedi. Yetkiyi ve bağlantıyı kontrol edip tekrar deneyin.');}});
  }
}
