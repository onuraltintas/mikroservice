import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, expand, finalize, forkJoin, map, of, reduce, switchMap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, Goal } from '../../../core/services/coaching-portal.service';
import { CoachingLearningSetupService, LearningContext } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService, GoalTarget } from '../../../core/services/coaching-study-planning.service';
import { StudentGoalTargetComponent } from './student-goal-target.component';
import { goalFitsContext } from './student-goal-context';
import { coachingFeedback } from '../coaching-feedback';

@Component({selector:'app-student-primary-goal',standalone:true,imports:[CommonModule,RouterLink,StudentGoalTargetComponent],
 styleUrl:'./student-study-plans.component.scss',template:`
 <section class="study-plans" aria-labelledby="primary-goal-title">
  <header class="page-heading"><div><p class="eyebrow">Hedefine odaklan</p><h1 id="primary-goal-title">Hedefim</h1><p>Bir ana okul veya program seç; net hedeflerini onun altında takip et.</p></div><a routerLink="/coaching-portal/progress">Çalışma hedeflerim ve gelişimim →</a></header>
  @if(error()){<p role="alert" class="notice error">{{error()}}</p>}
  @if(message()){<p role="status" class="notice success">{{message()}}</p>}
  @if(loading()){<p role="status">Hedefin yükleniyor…</p>}
  @if(!loading() && ready()){
   @if(context()?.purpose !== 'Lgs' && context()?.purpose !== 'Yks'){
    <div class="panel"><h2>Önce sınavını belirle</h2><p>Profil ve tercihlerinden LGS veya YKS hazırlığını seçerek sana uygun hedefleri açabilirsin.</p><a routerLink="/coaching-portal/setup">Profil ve tercihlerime git →</a></div>
   }@else{
    <div class="panel"><h2>{{context()?.purpose === 'Lgs' ? 'LGS okul hedefim' : 'YKS program hedefim'}}</h2>
     @if(current();as goal){<p><strong>{{goal.target.catalogTarget?.name || goal.title}}</strong></p>
      <app-student-goal-target [goalId]="goal.id" [context]="context()" [autoOpen]="true" (targetChanged)="targetUpdated($event)" />
     }@else{<p>Henüz bu sınav için ana hedef seçmedin. Var olan hedeflerinden birini seçebilir veya yeni hedef oluşturabilirsin.</p>}
    </div>
    <details class="panel"><summary>Ana hedefimi değiştir</summary><p>Seçimin hesabına kaydedilir. Önceki hedefler ve çalışma kayıtların korunur.</p>
     @for(goal of choices();track goal.id){<div class="draft-task"><strong>{{goal.target.catalogTarget?.name || goal.title}}</strong><button type="button" [disabled]="saving() || creating() || current()?.id===goal.id" (click)="select(goal.id)">{{current()?.id===goal.id ? 'Ana hedefim' : 'Ana hedef olarak seç'}}</button></div>}
     <button type="button" class="primary" (click)="create()" [disabled]="saving() || creating()">{{creating()?'Hazırlanıyor…':'Yeni okul / program hedefi oluştur'}}</button>
    </details>
   }
  }
  <button type="button" class="secondary" (click)="load()" [disabled]="loading() || saving() || creating()">Yeniden yükle</button>
 </section>`})
export class StudentPrimaryGoalComponent implements OnInit {
 private readonly auth=inject(AuthService);private readonly api=inject(CoachingStudyPlanningService);
 private readonly goals=inject(CoachingPortalService);private readonly setup=inject(CoachingLearningSetupService);private readonly destroy=inject(DestroyRef);
 @ViewChild(StudentGoalTargetComponent) private picker?:StudentGoalTargetComponent;
 readonly loading=signal(false);readonly saving=signal(false);readonly creating=signal(false);readonly ready=signal(false);
 readonly error=signal('');readonly message=signal('');readonly context=signal<LearningContext|null>(null);
 readonly choices=signal<(Goal & {target:GoalTarget})[]>([]);readonly current=signal<(Goal & {target:GoalTarget})|null>(null);
 private version:number|null=null;
 ngOnInit(){this.load();}
 load(){
  const student=this.auth.userProfile()?.id;if(!student||!this.canLeavePage()||this.loading())return;
  this.loading.set(true);this.error.set('');this.ready.set(false);
  const goals=this.goals.getStudentGoals(student,1,100).pipe(expand(p=>p.pageNumber*p.pageSize<p.totalCount?this.goals.getStudentGoals(student,p.pageNumber+1,100):EMPTY),reduce((all:Goal[],p)=>[...all,...p.items],[]));
  forkJoin({goals,setup:this.setup.get(),primary:this.api.getPrimaryGoal()}).pipe(switchMap(data=>{
   const candidates=data.goals.filter(g=>(g.category==='ExamPreparation'||g.category==='1')&&!g.isCompleted);
   const targets = candidates.length ? forkJoin(candidates.map(g=>this.api.getGoalTarget(g.id).pipe(map(target=>({...g,target}))))) : of([]);
   return targets.pipe(map(values=>({data,values})));

  }),takeUntilDestroyed(this.destroy),finalize(()=>this.loading.set(false))).subscribe({next:({data,values})=>{
   this.context.set(data.setup.context);this.version=data.primary.version;
   const choices=values.filter(g=>goalFitsContext(g.target,data.setup.context));this.choices.set(choices);
   this.current.set(choices.find(g=>g.id===data.primary.goalId)??null);this.ready.set(true);
  },error:()=>this.error.set('Hedefler yüklenemedi. Mevcut kayıtların korunuyor; yeniden deneyebilirsin.')});
 }
 select(id:string){if(this.saving()||this.creating()||!this.canLeavePage())return;this.selectCore(id);}
 private selectCore(id:string){this.saving.set(true);this.error.set('');this.api.selectPrimaryGoal(id,this.version).pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.saving.set(false))).subscribe({next:value=>{this.version=value.version;this.current.set(this.choices().find(x=>x.id===id)??null);this.message.set('Ana hedefin kaydedildi.');},error:e=>this.error.set(e?.error?.message??'Ana hedef kaydedilemedi. Yeniden yükleyip tekrar deneyebilirsin.')});}
 create(){
  const id=this.auth.userProfile()?.id;if(!id||this.creating()||this.saving()||!this.canLeavePage())return;
  this.creating.set(true);this.error.set('');const title=this.context()?.purpose==='Lgs'?'LGS okul hedefim':'YKS program hedefim';
  this.goals.createStudentGoal(id,{title,category:1},crypto.randomUUID()).pipe(switchMap(r=>this.api.getGoalTarget(r.goalId)),takeUntilDestroyed(this.destroy),finalize(()=>this.creating.set(false))).subscribe({next:target=>{
   this.choices.update(rows=>[...rows,{id:target.goalId,title,category:'ExamPreparation',progress:0,isCompleted:false,target}]);this.selectCore(target.goalId);
  },error:()=>this.error.set('Hedef oluşturulamadı. Yeniden yükleyerek mevcut hedeflerini kontrol et.')});
 }
 targetUpdated(target:GoalTarget){this.choices.update(rows=>rows.map(g=>g.id===target.goalId?{...g,target}:g));this.current.set(this.choices().find(g=>g.id===target.goalId)??null);}
 canLeavePage(){return !this.saving()&&!this.creating()&&(this.picker?.canLeavePage()??true);}
 private readonly feedback=coachingFeedback(this.message,this.error);
}
