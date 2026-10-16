import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { CoachingCatalogService, CoachingCatalogRow } from '../../../core/services/coaching-catalog.service';
import { CurriculumVersion } from '../../../core/services/coaching-learning-setup.service';
import { environment } from '../../../../environments/environment';

@Component({ selector: 'app-coaching-curriculum', standalone: true, imports: [FormsModule],
  template: `<section class="curriculum"><h2>Müfredat ve program sürümleri</h2>
    <p>Programın kaynağını ve geçerlilik yıllarını kaydedin. Dersler yalnız bir sürüme bağlanır; yeni program sürümünde yeni ders kayıtları kullanın. Eski planlar değişmez.</p>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    @if (loading()) { <p role="status">Programlar yükleniyor…</p> }
    <button type="button" [disabled]="loading() || saving()" (click)="load()">Yeniden yükle</button>
    <form #form="ngForm" (ngSubmit)="create()"><fieldset [disabled]="saving() || loading() || !ready()">
      <label>Program kodu<input name="code" required maxlength="100" [(ngModel)]="code"></label>
      <label>Program adı<input name="name" required maxlength="300" [(ngModel)]="name"></label>
      <label>Sürüm etiketi<input name="versionLabel" required maxlength="100" [(ngModel)]="versionLabel"></label>
      <label>İlk eğitim yılı başlangıcı<input name="validFrom" type="number" min="2000" max="2200" required [(ngModel)]="validFromYear"></label>
      <label>Son eğitim yılı başlangıcı<input name="validTo" type="number" [min]="validFromYear" max="2200" required [(ngModel)]="validToYear"></label>
      <label>Kaynak açıklaması<input name="source" required maxlength="300" [(ngModel)]="source"></label>
      <label>Kaynak bağlantısı (isteğe bağlı)<input name="sourceUrl" type="url" maxlength="1000" [(ngModel)]="sourceUrl"></label>
      <label>Ders ara<input name="search" maxlength="200" [(ngModel)]="search"></label><button type="button" (click)="loadLessons(1)">Dersleri ara</button>
      <div class="lessons">@for (lesson of lessons(); track lesson.id) {
        <label class="check"><input type="checkbox" [disabled]="!!lesson.curriculumVersionId" [checked]="selected.includes(lesson.id)" (change)="toggle(lesson.id)">{{ lesson.name }} {{ lesson.examCode || '' }} {{ lesson.gradeNumber ? ' / ' + lesson.gradeNumber + '. sınıf' : '' }} {{ lesson.curriculumVersionId ? '(Bir programa bağlı)' : '' }}</label>
      }</div>
      <div><button type="button" [disabled]="page <= 1" (click)="loadLessons(page - 1)">Önceki dersler</button><span>{{ selected.length }} ders seçildi</span><button type="button" [disabled]="page * 25 >= total" (click)="loadLessons(page + 1)">Sonraki dersler</button></div>
      <label>İşlem gerekçesi<textarea name="reason" required minlength="5" maxlength="500" [(ngModel)]="reason"></textarea></label>
      <button type="submit" [disabled]="form.invalid || !selected.length">Taslak program oluştur</button>
    </fieldset></form>
    <p>Yayın için işlem gerekçesini doldurun. Ders, ünite ve konuların aktifliğini ve konu sürelerini ortak katalogdan kontrol edin.</p>
    @for (version of versions(); track version.id) { <article><strong>{{ version.name }} · {{ version.versionLabel }}</strong><p>{{ version.validFromYear }}–{{ version.validToYear }} eğitim yılı başlangıçları · {{ version.source }} · {{ version.isPublished ? 'Yayında' : 'Taslak / yayında değil' }}</p>
      <button type="button" [disabled]="saving() || loading() || reason.trim().length < 5" (click)="publish(version)">{{ version.isPublished ? 'Yayından kaldır' : 'Yayınla' }}</button></article> }
  </section>`, styles: [`.curriculum{padding:1rem;border:1px solid #d6deea;border-radius:.75rem}fieldset{border:0;display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:.75rem;padding:0}label{display:grid;gap:.3rem}.lessons{grid-column:1/-1}.check{display:flex;align-items:center}input,textarea,button{font:inherit;border:1px solid #b8c5d5;border-radius:.4rem;padding:.5rem;max-width:100%;box-sizing:border-box}button{cursor:pointer;margin:.3rem}button:disabled{opacity:.5}article{border-top:1px solid #d6deea;padding-top:1rem;margin-top:1rem}[role=alert]{color:#a21825}`]
})
export class CoachingCurriculumComponent implements OnInit {
  private readonly http = inject(HttpClient); private readonly catalogs = inject(CoachingCatalogService); private readonly destroyRef = inject(DestroyRef);
  private readonly url = `${environment.apiUrl}/coaching-admin/curricula`;
  readonly versions = signal<CurriculumVersion[]>([]); readonly lessons = signal<CoachingCatalogRow[]>([]);
  readonly loading = signal(false); readonly saving = signal(false); readonly ready = signal(false); readonly error = signal<string | null>(null);
  code = ''; name = ''; versionLabel = ''; source = ''; sourceUrl = ''; reason = ''; search = ''; selected: string[] = [];
  validFromYear = new Date().getFullYear(); validToYear = new Date().getFullYear(); page = 1; total = 0;
  ngOnInit() { this.load(); }
  load() {
    if (this.loading() || this.saving()) return;
    this.loading.set(true); this.ready.set(false); this.error.set(null);
    forkJoin({ versions: this.http.get<{ data: CurriculumVersion[] }>(this.url), lessons: this.catalogs.list('lessons', { pageNumber: 1, pageSize: 25 }) })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
        next: r => { this.versions.set(r.versions.data); this.lessons.set(r.lessons.items); this.page = 1; this.total = r.lessons.totalCount; this.ready.set(true); },
        error: () => this.error.set('Program sürümleri yüklenemedi. Yeniden deneyin.')
      });
  }
  loadLessons(page: number) {
    if (this.loading() || this.saving()) return;
    this.loading.set(true); this.error.set(null);
    this.catalogs.list('lessons', { pageNumber: page, pageSize: 25, search: this.search.trim() })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
        next: r => { this.lessons.set(r.items); this.page = page; this.total = r.totalCount; }, error: () => this.error.set('Ders seçenekleri yüklenemedi.')
      });
  }
  toggle(id: string) { this.selected = this.selected.includes(id) ? this.selected.filter(x => x !== id) : [...this.selected, id]; }
  create() {
    if (this.saving() || this.loading() || !this.ready() || !this.selected.length || this.reason.trim().length < 5) return;
    this.write(this.http.post(this.url, { code: this.code.trim(), name: this.name.trim(), versionLabel: this.versionLabel.trim(),
      validFromYear: this.validFromYear, validToYear: this.validToYear, source: this.source.trim(), sourceUrl: this.sourceUrl.trim() || null, lessonIds: this.selected, reason: this.reason.trim() }));
  }
  publish(version: CurriculumVersion) {
    if (this.saving() || this.loading() || this.reason.trim().length < 5) return;
    this.write(this.http.put(`${this.url}/${encodeURIComponent(version.id)}/publication`, { expectedVersion: version.version, isPublished: !version.isPublished, reason: this.reason.trim() }));
  }
  private write(request: import('rxjs').Observable<unknown>) {
    this.saving.set(true); this.error.set(null);
    request.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.selected = []; this.saving.set(false); this.load(); },
      error: e => this.error.set(e?.error?.message ?? 'Program kaydedilemedi. Bilgileriniz korundu.')
    });
  }
}
