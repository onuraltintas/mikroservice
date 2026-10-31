import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, Input, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { CoachingWeeklyReviewService, WeeklyHome } from '../../../core/services/coaching-weekly-review.service';

@Component({selector:'app-student-weekly-home',standalone:true,imports:[CommonModule,RouterLink],
  styles:[`:host{display:block;margin:1.5rem 0}section{padding:20px;border:1px solid var(--ui-border);border-radius:12px;background:var(--ui-warning-soft);color:var(--ui-text)}header{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:12px}h2{font-size:22px;font-weight:750;line-height:1.3}p{line-height:1.6}a,button{display:inline-block;padding:9px 12px;border:1px solid var(--ui-border);border-radius:7px;color:inherit;background:transparent;font:inherit}a:focus-visible,button:focus-visible{outline:2px solid var(--ui-border-strong);outline-offset:3px}article{padding:14px 0;border-top:1px solid var(--ui-border);overflow-wrap:anywhere}.error{color:var(--ui-danger)}`],
  template:`<section aria-label="Haftalık değerlendirme ve kararların"><header><h2>{{showDecisions ? 'Haftan ve kararların' : 'Haftalık değerlendirmen'}}</h2><button type="button" (click)="load()" [disabled]="loading()">Yenile</button></header>
  @if(loading()){<p role="status">Haftalık bilgiler yükleniyor…</p>}
  @if(error()){<p role="alert" class="error">{{error()}}</p>}
  @if(data();as view){<p>{{view.weekStart | date:'dd.MM.yyyy'}} haftası · {{view.needsReflection ? 'Değerlendirmen bekleniyor.' : 'Değerlendirme beklemiyor.'}}</p>
    @if(view.missingReflectionCount){<p>{{view.missingReflectionCount}} haftanın değerlendirmesi eksik. En eski eksik haftadan başlayabilirsin.</p>}<a routerLink="/coaching-portal/growth/week" [queryParams]="{week:view.weekStart}">{{view.needsReflection ? 'Haftanı değerlendir' : 'Haftalık değerlendirmeyi aç'}}</a>
    @if(showDecisions){<p>{{view.openDecisions}} açık karar · {{view.overdueDecisions}} kararın tarihi geçti. Önce en yakın tarihli beş karar gösterilir.</p>
    @for(decision of view.decisions;track decision.id){<article><strong>{{decision.title}}</strong><p>Son tarih: {{decision.dueOn | date:'dd.MM.yyyy'}} @if(decision.isOverdue){<span class="error"> · Tarihi geçti</span>}</p><a routerLink="/coaching-portal/growth/week" [queryParams]="{week:decision.weekStart,decision:decision.id}">Kararı aç</a></article>}
    @if(!view.decisions.length){<p>Açık kararın bulunmuyor.</p>}
    <a routerLink="/coaching-portal/growth/week" [queryParams]="{week:view.currentWeekStart}">Bu haftanın tüm kararlarını aç</a>}
  }</section>`})
export class StudentWeeklyHomeComponent {
  @Input() showDecisions = true;
  private readonly api=inject(CoachingWeeklyReviewService); private readonly destroyRef=inject(DestroyRef);
  readonly data=signal<WeeklyHome|null>(null);readonly loading=signal(false);readonly error=signal('');
  ngOnInit(){this.load();}
  load(){if(this.loading())return;this.loading.set(true);this.data.set(null);this.error.set('');
    this.api.home().pipe(takeUntilDestroyed(this.destroyRef),finalize(()=>this.loading.set(false))).subscribe({next:value=>this.data.set(value),error:()=>this.error.set('Haftalık bilgiler yüklenemedi. Yeniden deneyebilirsin.')});}
  private readonly feedback = coachingFeedback(undefined, this.error);
}
