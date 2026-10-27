import { coachingFeedback } from '../coaching-feedback';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { CoachingLearningSetupService, LearningSetupView } from '../../../core/services/coaching-learning-setup.service';

@Component({ selector: 'app-learning-setup-entry', standalone: true, imports: [RouterLink],
  template: `<section aria-labelledby="setup-entry-title" class="setup-entry"><h2 id="setup-entry-title">Çalışma düzenin</h2>
    @if (loading()) { <p role="status">Kurulum bilgilerin yükleniyor…</p> }
    @else if (error()) { <p role="alert">{{ error() }}</p><button type="button" (click)="load()">Tekrar dene</button> }
    @else if (state()?.isReady) { <p>Eğitim bilgilerin ve çalışma tercihlerin hazır.</p><a routerLink="/coaching-portal/setup">Tercihlerimi gözden geçir</a> }
    @else { <p>Eğitim bilgilerini, çalışacağın dersleri ve müsaitliğini küçük adımlarla belirle. İstediğin zaman devam edebilirsin.</p><a routerLink="/coaching-portal/setup">{{ state()?.context ? 'Kuruluma devam et' : 'Çalışma düzenimi kur' }}</a> }
    <a routerLink="/coaching-portal/study-plans">Mevcut planımı aç</a>
  </section>`, styles: [`.setup-entry{border:1px solid #d4dfd5;background:#f5faf5;border-radius:1rem;padding:1.25rem;margin:1rem 0;color:#204739}h2{font-size:1.15rem;margin:0 0 .5rem}p{margin:.5rem 0}a,button{display:inline-block;margin:.5rem 1rem .25rem 0;font:inherit}a{color:inherit;font-weight:600;text-underline-offset:3px}`]
})
export class LearningSetupEntryComponent implements OnInit {
  private readonly service = inject(CoachingLearningSetupService); private readonly destroyRef = inject(DestroyRef);
  readonly state = signal<LearningSetupView | null>(null); readonly loading = signal(false); readonly error = signal<string | null>(null);
  ngOnInit() { this.load(); }
  load() {
    if (this.loading()) return;
    this.loading.set(true); this.error.set(null);
    this.service.get().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: s => this.state.set(s), error: e => {
        if (e?.status === 404) this.state.set(null);
        else this.error.set(e?.status === 403 ? 'Koçluk öğrenci erişimin doğrulanamadı.' : 'Kurulum bilgileri yüklenemedi. Mevcut çalışmalarına devam edebilirsin.');
      }
    });
  }
  private readonly feedback = coachingFeedback(undefined, this.error);
}
