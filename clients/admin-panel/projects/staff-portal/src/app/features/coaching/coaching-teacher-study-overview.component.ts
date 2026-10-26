import { coachingFeedback } from '../../../../../../src/app/features/coaching-portal/coaching-feedback';
import {CommonModule} from '@angular/common';
import {Component,DestroyRef,ViewChild,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {Subscription,finalize} from 'rxjs';
import {CoachingStudyPlanningService,TeacherStudyOverview,TeacherStudyStudent,TeacherStudyWork} from '../../../../../../src/app/core/services/coaching-study-planning.service';
import {StudentStudyPlansComponent} from '../../../../../../src/app/features/coaching-portal/pages/student-study-plans.component';
import {StudentStudyPlanAssistantComponent} from '../../../../../../src/app/features/coaching-portal/pages/student-study-plan-assistant.component';
import {dateKey} from '../../../../../../src/app/features/coaching-portal/pages/study-plan-calendar';
import {clock} from '../../../../../../src/app/features/coaching-portal/pages/weekly-calendar';

@Component({selector:'staff-coaching-teacher-study-overview',standalone:true,imports:[CommonModule,FormsModule,StudentStudyPlansComponent,StudentStudyPlanAssistantComponent],templateUrl:'./coaching-teacher-study-overview.component.html',styleUrl:'./coaching-teacher-study-overview.component.scss'})
export class CoachingTeacherStudyOverviewComponent{
  readonly clock=clock;
  private readonly api=inject(CoachingStudyPlanningService);private readonly destroyRef=inject(DestroyRef);private request?:Subscription;
  @ViewChild(StudentStudyPlansComponent) private editor?:StudentStudyPlansComponent;
  @ViewChild('wizard') private assistant?:StudentStudyPlanAssistantComponent;
  readonly data=signal<TeacherStudyOverview>({items:[],totalCount:0,pageNumber:1,pageSize:25});readonly loading=signal(false);readonly error=signal('');
  readonly detail=signal<{student:TeacherStudyStudent;filter:'all'|'pending'|'completed'|'overdue';planId?:string}|null>(null);
  display:'summary'|'pending'|'completed'|'overdue'='summary';workPage=1;
  workItems(){return this.data().items.flatMap(student=>(student.tasks??[]).map(task=>({student,task}))).filter(x=>this.display==='summary'||this.display==='pending'&&!x.task.isCompleted||this.display==='completed'&&x.task.isCompleted||this.display==='overdue'&&x.task.isOverdue).sort((a,b)=>a.task.plannedDate.localeCompare(b.task.plannedDate)||(a.task.plannedStartMinute??-1)-(b.task.plannedStartMinute??-1)||a.task.taskId.localeCompare(b.task.taskId));}
  async openWork(row:{student:TeacherStudyStudent;task:TeacherStudyWork}){await this.open(row.student,this.display==='summary'?'all':this.display,row.task.planId);}
  fromDate=dateKey(new Date(Date.now()-7*86400000));toDate=dateKey(new Date(Date.now()+7*86400000));search='';
  ngOnInit(){this.load();}
  wizardMode=false;
  async canLeavePage(){return this.wizardMode?(await this.assistant?.canLeavePage()??true):(await this.editor?.canLeavePage()??true);}
  async open(student:TeacherStudyStudent,filter:'all'|'pending'|'completed'|'overdue'='all',planId?:string){if(await this.canLeavePage()){this.wizardMode=false;this.detail.set({student,filter,planId});}}
  async createWithWizard(){if(await this.canLeavePage())this.wizardMode=true;}
  planCreated(planId:string){this.wizardMode=false;this.detail.update(value=>value?{...value,filter:'all',planId}:value);this.load(this.data().pageNumber);}
  async back(){if(!await this.canLeavePage())return;this.detail.set(null);this.load(this.data().pageNumber);}
  load(page=1){
    this.request?.unsubscribe();this.error.set('');this.workPage=1;
    if(!this.fromDate||!this.toDate||this.fromDate>this.toDate){this.error.set('Geçerli bir tarih aralığı seçin.');return;}
    this.loading.set(true);this.request=this.api.teacherOverview(this.fromDate,this.toDate,page,this.search).pipe(takeUntilDestroyed(this.destroyRef),finalize(()=>this.loading.set(false))).subscribe({next:r=>this.data.set(r),error:e=>{this.data.set({items:[],totalCount:0,pageNumber:1,pageSize:25});this.error.set(e.error?.message??'Çalışma takibi yüklenemedi. Yetkiyi ve bağlantıyı kontrol edip tekrar deneyin.');}});
  }

  private readonly errorFeedback = coachingFeedback(undefined, this.error);
}
