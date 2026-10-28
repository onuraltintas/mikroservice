import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, finalize } from 'rxjs';
import { CoachingWeeklyReviewService, DecisionStudyPreview, DecisionStudyProposal, DecisionWorkOptions, WeeklyDecision } from '../../../core/services/coaching-weekly-review.service';

@Component({selector:'app-weekly-decision-work',standalone:true,imports:[CommonModule,FormsModule],
  templateUrl:'./weekly-decision-work.component.html',styleUrl:'./weekly-review.component.scss'})
export class WeeklyDecisionWorkComponent {
  private readonly toaster = coachingToaster();
  @Input({required:true}) studentId!:string;
  @Input({required:true}) decision!:WeeklyDecision;
  @Output() saved=new EventEmitter<void>();
  @Output() close=new EventEmitter<void>();
  private readonly api=inject(CoachingWeeklyReviewService);private readonly destroyRef=inject(DestroyRef);
  private read?:Subscription;
  readonly options=signal<DecisionWorkOptions|null>(null);readonly loading=signal(false);readonly saving=signal(false);readonly error=signal('');readonly stale=signal(false);
  readonly preview=signal<DecisionStudyPreview|null>(null);
  mode:'link'|'study'|'assignment'='link';kind:'study'|'assignment'='study';search='';
  studyId:string|null=null;assignmentId:string|null=null;dirty=false;confirmed=false;moveExisting=false;
  date='';time='18:00';minutes=30;dueAt='';estimatedMinutes:number|null=30;
  private requestId=crypto.randomUUID();
  private initialized=false;
  ngOnChanges(){this.read?.unsubscribe();this.studyId=this.decision.work?.study?.id??null;this.assignmentId=this.decision.work?.assignment?.id??null;
    this.mode='link';this.kind='study';this.search='';this.dirty=false;this.stale.set(false);this.preview.set(null);this.confirmed=false;this.options.set(null);this.error.set('');this.requestId=crypto.randomUUID();if(this.initialized)this.load();}
  ngOnInit(){this.initialized=true;this.load();}
  canRefresh(){return !this.saving()&&!this.dirty;}
  async canLeavePage(){return !this.saving()&&(!this.dirty||await this.toaster.confirm('Kaydedilmemiş iş bağlantısı veya önizleme var. Kaydetmeden ayrılmak istiyor musun?'));}
  load(page=1){this.read?.unsubscribe();this.loading.set(true);this.error.set('');
    this.read=this.api.workOptions(this.studentId,this.decision,this.kind,this.search,page).pipe(takeUntilDestroyed(this.destroyRef),finalize(()=>this.loading.set(false)))
      .subscribe({next:value=>this.options.set(value),error:e=>this.failed(e)});
  }
  async chooseMode(mode:'link'|'study'|'assignment'){
    if(this.stale()||!await this.canLeavePage())return;this.mode=mode;this.dirty=false;this.preview.set(null);this.confirmed=false;this.error.set('');this.requestId=crypto.randomUUID();
    this.studyId=this.decision.work?.study?.id??null;this.assignmentId=this.decision.work?.assignment?.id??null;
    this.moveExisting=false;this.date='';this.time='18:00';this.minutes=30;this.dueAt='';
    this.kind='study';this.search='';this.load();
  }
  changed(){this.dirty=true;this.preview.set(null);this.confirmed=false;this.requestId=crypto.randomUUID();}
  changeKind(){this.search='';this.load();}
  select(id:string){if(this.kind==='study')this.studyId=id;else this.assignmentId=id;this.changed();}
  clear(kind:'study'|'assignment'){if(kind==='study')this.studyId=null;else this.assignmentId=null;this.changed();}
  link(){if(!this.decision.canEdit||this.saving()||this.loading()||this.stale())return;this.write(this.api.linkWork(this.studentId,this.decision,this.studyId,this.assignmentId));}
  setMove(move:boolean){this.moveExisting=move;const work=this.decision.work?.study;this.date=move?work?.date??'':'';
    this.minutes=move?work?.minutes??30:30;this.time=move&&work?.startMinute!=null?this.clock(work.startMinute):'18:00';this.changed();}
  private proposal():DecisionStudyProposal|null {
    const plan=this.options()?.plan;const match=/^(\d{2}):(\d{2})$/.exec(this.time);
    if(!plan||!this.date||!match||Number(match[1])>23||Number(match[2])>59||!Number.isInteger(this.minutes)||this.minutes<1||this.minutes>1440){this.error.set('Planı, tarihi, saati ve süreyi kontrol et.');return null;}
    return {expectedDecisionVersion:this.decision.version,expectedPlanVersion:plan.version,planId:plan.id,taskId:this.moveExisting?this.decision.work?.study?.id??null:null,
      date:this.date,startMinute:Number(match[1])*60+Number(match[2]),minutes:this.minutes};
  }
  prepare(){if(this.loading()||this.saving()||this.stale())return;const proposal=this.proposal();if(!proposal)return;
    this.read?.unsubscribe();this.loading.set(true);this.error.set('');this.preview.set(null);this.confirmed=false;
    this.read=this.api.previewWork(this.studentId,this.decision,proposal).pipe(takeUntilDestroyed(this.destroyRef),finalize(()=>this.loading.set(false)))
      .subscribe({next:value=>{this.preview.set(value);this.dirty=true;},error:e=>this.failed(e)});
  }
  apply(){const preview=this.preview();if(!this.confirmed||!preview||this.saving()||this.loading()||this.stale())return;this.write(this.api.applyWork(this.studentId,this.decision,preview,this.requestId));}
  createAssignment(){if(!this.dueAt||this.saving()||this.loading()||this.stale())return;const due=new Date(this.dueAt);if(!Number.isFinite(due.getTime())){this.error.set('Geçerli bir teslim tarihi seç.');return;}
    this.write(this.api.createWorkAssignment(this.studentId,this.decision,due.toISOString(),this.estimatedMinutes,this.requestId));}
  private write(request:Observable<unknown>){if(this.saving()||this.loading()||this.stale())return;this.saving.set(true);this.error.set('');
    request.pipe(takeUntilDestroyed(this.destroyRef),finalize(()=>this.saving.set(false))).subscribe({next:()=>{this.dirty=false;this.saved.emit();},error:e=>this.failed(e)});}
  private failed(e:{status?:number;error?:{message?:string}}){
    if(e.status===409){this.stale.set(true);this.preview.set(null);this.confirmed=false;}
    if(e.status===403||e.status===404){this.options.set(null);this.preview.set(null);this.stale.set(true);}
    this.error.set(e.error?.message??'İşlem tamamlanamadı. Girdilerin korunuyor; tekrar deneyebilirsin.');
  }
  async reload(){if(!await this.canLeavePage())return;this.dirty=false;this.close.emit();}
  async cancel(){if(await this.canLeavePage())this.close.emit();}
  clock(minute:number){return `${Math.floor(minute/60).toString().padStart(2,'0')}:${(minute%60).toString().padStart(2,'0')}`;}
  private readonly feedback = coachingFeedback(undefined, this.error);
}
