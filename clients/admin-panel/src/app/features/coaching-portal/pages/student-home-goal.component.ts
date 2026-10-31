import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin, map, of, switchMap } from 'rxjs';
import { CoachingStudyPlanningService, GoalTarget } from '../../../core/services/coaching-study-planning.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { goalFitsContext } from './student-goal-context';
@Component({selector:'app-student-home-goal',standalone:true,imports:[RouterLink],styles:[`
 :host{display:block;min-width:0}h2{font-size:1.15rem;margin:.3rem 0 .7rem}p{font-size:.9rem;line-height:1.6;overflow-wrap:anywhere}.metrics{display:flex;flex-wrap:wrap;gap:.5rem;margin:.75rem 0}.metrics span{padding:.5rem .7rem;border-radius:.75rem;background:var(--ui-brand-soft);color:var(--ui-text)}a,button{display:inline-flex;min-height:44px;align-items:center;padding:.6rem .9rem;border-radius:.7rem;border:1px solid var(--ui-border);background:var(--ui-brand-soft);color:var(--ui-text);font:inherit;font-weight:650}button{cursor:pointer}
 `],template:`<section aria-labelledby="home-goal-title"><h2 id="home-goal-title">Hedefim</h2>
 @if(loading()){<p role="status">Hedefin yükleniyor…</p>}@else if(error()){<p role="alert">Hedef özeti yüklenemedi.</p><button type="button" (click)="load()">Yeniden dene</button>}
 @else if(target();as goal){<p><strong>{{goal.catalogTarget?.name || 'Okul veya programını seç'}}</strong></p><div class="metrics">@for(metric of goal.netTargets || [];track metric.examCode){<span>{{(metric.examCode || '').replace('AYT_','AYT ').replace('YDT_EN','YDT İngilizce')}} · <strong>{{metric.targetNet}} net</strong></span>}</div>}
 @else{<p>Okul veya program hedefini belirleyerek başlayabilirsin.</p>}
 <a routerLink="/coaching-portal/growth/goals">Hedefimi aç →</a></section>`})
export class StudentHomeGoalComponent implements OnInit {
 private readonly api=inject(CoachingStudyPlanningService);private readonly setup=inject(CoachingLearningSetupService);private readonly destroy=inject(DestroyRef);
 readonly target=signal<GoalTarget|null>(null);readonly loading=signal(false);readonly error=signal(false);
 ngOnInit(){this.load();}
 load(){if(this.loading())return;this.loading.set(true);this.error.set(false);
  forkJoin({primary:this.api.getPrimaryGoal(),setup:this.setup.get()}).pipe(switchMap(data=>data.primary.goalId?this.api.getGoalTarget(data.primary.goalId).pipe(map(target=>goalFitsContext(target,data.setup.context)?target:null)):of(null)),takeUntilDestroyed(this.destroy),finalize(()=>this.loading.set(false))).subscribe({next:target=>this.target.set(target),error:()=>this.error.set(true)});
 }
}
