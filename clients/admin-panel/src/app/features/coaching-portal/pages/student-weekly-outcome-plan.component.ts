import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import {CommonModule} from '@angular/common';
import {Component,DestroyRef,EventEmitter,Input,OnInit,Output,computed,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {Router,RouterLink} from '@angular/router';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {finalize,switchMap} from 'rxjs';
import {CoachingWeeklyOutcomePlanService,WeeklyOutcomeOptions,WeeklyOutcomePreview,WeeklyOutcomeRequest,weeklyPlanKindLabel} from '../../../core/services/coaching-weekly-outcome-plan.service';
import {CoachingWeeklyDraftService} from '../../../core/services/coaching-weekly-draft.service';
import {clock} from './weekly-calendar';

@Component({selector:'app-student-weekly-outcome-plan',standalone:true,imports:[CommonModule,FormsModule,RouterLink],templateUrl:'./student-weekly-outcome-plan.component.html',styleUrl:'./student-weekly-outcome-plan.component.scss'})
export class StudentWeeklyOutcomePlanComponent implements OnInit {
  @Input() studentId?:string;
  /** E6 #29: Plan önizlemesi (Çalışma tercihlerim) shows the preview as soon as the saved week is loaded. */
  @Input() autoPreview=false;
  @Output() planCreated=new EventEmitter<string>();
  private readonly service=inject(CoachingWeeklyOutcomePlanService);private readonly destroy=inject(DestroyRef);private readonly router=inject(Router);
  private readonly weekly=inject(CoachingWeeklyDraftService);private readonly toaster=coachingToaster();
  readonly options=signal<WeeklyOutcomeOptions|null>(null);readonly preview=signal<WeeklyOutcomePreview|null>(null);readonly loading=signal(false);readonly generating=signal(false);readonly saving=signal(false);readonly stale=signal(false);readonly error=signal('');
  readonly lessonFilter=signal('');readonly page=signal(1);readonly pageSize=25;readonly clock=clock;
  readonly filteredTasks=computed(()=>this.preview()?.schedule.tasks.filter(x=>!this.lessonFilter()||x.lessonId===this.lessonFilter())??[]);
  readonly visibleTasks=computed(()=>this.filteredTasks().slice((this.page()-1)*this.pageSize,this.page()*this.pageSize));
  readonly remaining=computed(()=>this.preview()?.schedule.lessons.reduce((sum,x)=>sum+x.remainingMinutes,0)??0);
  /** Paket G #41: the chosen courses without a weekly lesson, named next to the courses that do not fit. */
  readonly unscheduledNames=computed(()=>(this.preview()?.unscheduledLessons??[]).map(x=>x.lessonName).join(', '));
  /** Review of PR 126: a teacher reads about the student's week. */
  unscheduledText(){const names=this.unscheduledNames();return this.studentId
    ?`${names} için öğrencinin haftalık düzeninde ders saati yok; bu dersler plana hiç girmez. Öğrencinin haftalık takvimine bu dersler için saat eklenmeli.`
    :`${names} için haftalık düzeninde ders saati yok; bu dersler plana hiç girmez. Haftalık takvimine bu dersler için saat ekleyip yeniden önizleyebilirsin.`;}
  readonly totalMinutes=computed(()=>(this.preview()?.schedule.tasks.reduce((sum,x)=>sum+x.minutes,0)??0)+(this.preview()?.protectedTasks?.reduce((sum,x)=>sum+x.minutes,0)??0));
  readonly kindLabel=weeklyPlanKindLabel;readonly savingExamDate=signal(false);
  /**
   * Paket H1 (P10): the weekly lessons that are no longer chosen and stop the preview (review of PR #129: from the code
   * StudyPlanning.LessonsNotChosen and its lessons). A student gets the way to her weekly routine and one-click removal.
   */
  readonly notChosen=signal<{lessonId:string;lessonName:string}[]>([]);
  notChosenLessons(){return !this.studentId&&this.notChosen().length>0;}
  /** Review of PR #129: "Bu dersleri haftalık düzenimden kaldır" — confirm, remove their cards from the saved week, preview again. */
  async removeNotChosenLessons(){
    const lessons=this.notChosen();if(this.studentId||!lessons.length||this.busy())return;
    const names=lessons.map(x=>x.lessonName);
    const list=names.length>1?`${names.slice(0,-1).join(', ')} ve ${names.at(-1)} dersleri`:`${names[0]} dersi`;
    if(!await this.toaster.confirm(`${list} haftalık düzeninden kaldırılacak. Devam edelim mi?`,{confirmText:'Kaldır',cancelText:'Vazgeç'}))return;
    const ids=new Set(lessons.map(x=>x.lessonId.toLowerCase()));
    this.saving.set(true);this.error.set('');
    this.weekly.get().pipe(takeUntilDestroyed(this.destroy),switchMap(page=>{
      const draft=page.draft;if(!draft)throw {status:409};
      return this.weekly.save(draft.version,draft.timeZoneId,draft.slots.filter(x=>!ids.has(x.lessonId.toLowerCase())));
    }),finalize(()=>this.saving.set(false))).subscribe({
      next:saved=>{this.saving.set(false);const opt=this.options();this.notChosen.set([]);if(opt)this.options.set({...opt,weeklyDraftVersion:saved.version});this.stale.set(false);this.generatePreview();},
      error:e=>this.fail(e,'Haftalık düzenin kaydedilemedi. Yeniden deneyebilirsin.')
    });
  }
  // Roadmap O7: the rule-based engine (version 2) is always used by this screen. Paket H1: the freed hours are used by default.
  readonly engineVersion=2;prioritizeWhenShort=false;examDateInput='';
  startDate='';endDate='';reuseFreedSlots=true;title='Haftalık derslerimden çalışma planım';confirmed=false;private defaultEnd=true;
  ngOnInit(){this.load();}
  busy(){return this.loading()||this.generating()||this.saving()||this.savingExamDate();}
  canLeavePage(){return !this.busy();}
  load(){if(this.busy())return;this.loading.set(true);this.error.set('');let loaded=false;this.service.options(this.studentId).pipe(takeUntilDestroyed(this.destroy),finalize(()=>{this.loading.set(false);if(loaded&&this.autoPreview&&!this.preview())this.generatePreview();})).subscribe({next:r=>{loaded=true;
    if(!this.options()){this.startDate=r.startDate;this.endDate=r.endDate;}this.options.set(r);this.examDateInput=r.examDate??'';this.changed();this.stale.set(false);
  },error:e=>this.fail(e,'Haftalık taslak yüklenemedi. Önce takvimini kaydet.')} );}
  changed(){this.preview.set(null);this.confirmed=false;this.page.set(1);this.lessonFilter.set('');this.error.set('');this.notChosen.set([]);}
  startChanged(){if(this.defaultEnd&&!this.options()?.examDate&&/^\d{4}-\d{2}-\d{2}$/.test(this.startDate)){const [y,m,d]=this.startDate.split('-').map(Number);this.endDate=`${m<6||m===6&&d===1?y:y+1}-06-01`;}this.changed();}
  endChanged(){this.defaultEnd=false;this.changed();}
  private request():WeeklyOutcomeRequest{return{startDate:this.startDate,endDate:this.endDate,expectedWeeklyDraftVersion:this.options()!.weeklyDraftVersion,reuseFreedSlots:this.reuseFreedSlots,engineVersion:this.engineVersion,prioritizeWhenShort:this.prioritizeWhenShort};}
  /** Student only: her own exam date moves the default end to the day before it; null returns to the admin's date. */
  saveExamDate(value:string|null){
    if(this.busy()||this.studentId)return;
    if(value!==null&&!/^\d{4}-\d{2}-\d{2}$/.test(value)){this.error.set('Geçerli bir sınav tarihi seç.');return;}
    this.savingExamDate.set(true);this.error.set('');
    this.service.setExamDate(value).pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.savingExamDate.set(false))).subscribe({next:r=>{
      this.options.set(r);this.examDateInput=r.examDate??'';if(this.defaultEnd)this.endDate=r.endDate;this.changed();
    },error:e=>this.fail(e,'Sınav tarihi kaydedilemedi.')});
  }
  prioritizeAndPreview(){if(this.busy())return;this.prioritizeWhenShort=true;this.generatePreview();}
  generatePreview(){
    if(this.busy()||!this.options())return;if(!this.startDate||!this.endDate||this.endDate<this.startDate){this.error.set('Başlangıç ve bitiş tarihlerini kontrol et.');return;}const dateIssue=this.dateIssue();if(dateIssue){this.error.set(dateIssue);return;}
    this.generating.set(true);this.error.set('');this.notChosen.set([]);this.confirmed=false;this.preview.set(null);this.stale.set(false);
    this.service.preview(this.request(),this.studentId).pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.generating.set(false))).subscribe({next:r=>{this.preview.set(r);this.page.set(1);this.lessonFilter.set('');},error:e=>this.fail(e,'Önizleme hazırlanamadı.')});
  }
  /** T4: K4 date rules shown on the fields — start from today, finish at the latest on the day before the exam. */
  readonly todayIso=new Intl.DateTimeFormat('en-CA',{timeZone:'Europe/Istanbul',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
  /** G2 review (M2): an estimated exam date sets the default end only; it is no limit. */
  lastDay(){const o=this.options();const exam=o?.examDate;return exam&&o?.endDateSource!=='Estimated'?dayBefore(exam):null;}
  estimated(){return this.options()?.endDateSource==='Estimated';}
  dateIssue(){if(this.startDate&&this.startDate<this.todayIso)return'Başlangıç tarihi bugünden önce olamaz.';const last=this.lastDay();if(last&&this.endDate>last)return`Bitiş en geç sınavdan bir gün önce olabilir (${last.split('-').reverse().join('.')}).`;return null;}
  /** K4 (31.10.2026): the longest deadline is the day before the exam; the server rejects anything later. */
  extendedDeadline():string|null{
    const end=this.preview()?.schedule.recommendedEndDate;if(!end)return null;
    const last=this.lastDay();
    const target=last&&end>last?last:end;return target>this.endDate?target:null;
  }
  canExtendDeadline(){return !!this.extendedDeadline();}
  extendDeadline(){const end=this.extendedDeadline();if(!end||this.busy())return;this.endDate=end;this.defaultEnd=false;this.generatePreview();}
  useFreedSlots(){if(this.busy())return;this.reuseFreedSlots=true;this.generatePreview();}
  isCurrent(){const p=this.preview();return !!p&&!!this.options()&&p.request.startDate===this.startDate&&p.request.endDate===this.endDate&&p.request.expectedWeeklyDraftVersion===this.options()!.weeklyDraftVersion&&p.request.reuseFreedSlots===this.reuseFreedSlots&&!!p.request.prioritizeWhenShort===this.prioritizeWhenShort;}
  save(){
    const p=this.preview();if(this.busy()||!p?.canCreate||!this.confirmed||this.stale()||!this.isCurrent())return;
    if(!this.title.trim()||this.title.trim().length>200){this.error.set('Plan için en fazla 200 karakterlik bir başlık gir.');return;}
    this.saving.set(true);this.error.set('');this.service.create({title:this.title.trim(),preview:p.request,fingerprint:p.fingerprint},this.studentId).pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.saving.set(false)))
      .subscribe({next:r=>this.studentId?this.planCreated.emit(r.id):void this.router.navigate(['/coaching-portal/plan'],{queryParams:{plan:r.id}}),error:e=>this.fail(e,'Plan taslağı kaydedilemedi. Yeniden deneyebilirsin.')});
  }
  private fail(e:{status?:number;error?:{message?:string;code?:string;lessons?:{lessonId:string;lessonName:string}[]}},fallback:string){
    // Review of PR #129: lessons no longer chosen are not a stale preview; the week has to change first.
    const notChosen=e.status===409&&e.error?.code===LESSONS_NOT_CHOSEN?e.error.lessons??[]:[];this.notChosen.set(notChosen);
    if(e.status===409&&e.error?.code!==LESSONS_NOT_CHOSEN){this.stale.set(true);this.confirmed=false;}this.error.set(e.error?.message??fallback);}
  filter(value:string){this.lessonFilter.set(value);this.page.set(1);}
  private readonly feedback = coachingFeedback(undefined, this.error);
}

/** Paket H1 (P10): the server's code for weekly lessons that are no longer chosen. */
const LESSONS_NOT_CHOSEN='StudyPlanning.LessonsNotChosen';

function dayBefore(date:string):string{
  const [y,m,d]=date.split('-').map(Number);const day=new Date(Date.UTC(y,m-1,d-1));
  return `${day.getUTCFullYear()}-${String(day.getUTCMonth()+1).padStart(2,'0')}-${String(day.getUTCDate()).padStart(2,'0')}`;
}
