import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subscription, finalize } from 'rxjs';
import { environment } from '../../../../environments/environment';

interface AdmissionItem {
  id: string; targetId: string; name: string; institution: string; externalCode: string; scoreType: string;
  degree: string | null; field: string | null; minimumScore: number | null; rank: number | null;
  percentile: number | null; year: number | null; stage: string | null; sourceUrl: string; city: string | null;
  district: string | null; institutionType: string | null; language: string | null; scholarship: string | null; quota: number | null;
}

@Component({ selector: 'app-target-admission-search', standalone: true, imports: [CommonModule, FormsModule], styles: [`
    :host { display:block; }
    section { margin-top:1rem; padding:1rem; border:1px solid var(--ui-border); border-radius:.75rem; }
    h4 { margin:0; font-size:1rem; font-weight:600; }
    p { margin:.5rem 0; line-height:1.5; }
    fieldset { display:grid; grid-template-columns:repeat(auto-fit,minmax(min(100%,13rem),1fr)); gap:.8rem; margin:1rem 0; padding:0; border:0; }
    label { display:flex; flex-direction:column; gap:.35rem; font-size:.9rem; }
    input, select { box-sizing:border-box; width:100%; min-width:0; min-height:2.6rem; padding:.55rem .65rem; border:1px solid var(--ui-border); border-radius:.4rem; color:inherit; background:transparent; font:inherit; }
    select:disabled { opacity:.65; }
    button { min-height:2.5rem; padding:.5rem .8rem; border:1px solid var(--ui-border-strong); border-radius:.4rem; font:inherit; cursor:pointer; }
    button:disabled { opacity:.55; cursor:default; }
    button[type=submit] { align-self:end; color: var(--ui-brand-contrast); background:var(--ui-brand); border-color:var(--ui-brand); }
    ul { margin:.75rem 0; padding:0; list-style:none; }
    li { display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between; gap:1rem; margin:.6rem 0; padding:1rem; border:1px solid var(--ui-border); border-radius:.5rem; }
    li > div { min-width:0; overflow-wrap:anywhere; }
    li > div:last-child { display:flex; flex-wrap:wrap; align-items:center; gap:.75rem; }
    a { color:var(--ui-brand-text); text-decoration:underline; }
  `], template: `
  <section aria-label="Doğrulanmış hedef kataloğu" class="mt-4 rounded-lg border p-3">
    <h4 class="font-semibold">{{ kind === 'program' ? 'Üniversite programları · MEBİ kataloğu' : 'LGS okul programları · MEBİ kataloğu' }}</h4>
    <p class="my-2 text-xs">{{kind === 'school' ? 'MEBİ okul programları ve kaynakta gösterilen taban puanları listelenir. Kaynakta puan yılı, yerleştirme aşaması ve kontenjan belirtilmemiştir.' : 'Güncel MEBİ program kataloğu listelenir. Kaynakta bulunmayan bilgiler boş gösterilir. Geçmiş yerleştirme sonucu, gelecek yıl için kabul garantisi değildir.'}}</p>
    <form (ngSubmit)="search()">
      <fieldset [disabled]="busy() || disabled" class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <ng-container *ngIf="kind === 'program'; else schoolFilters">
          <label>Program türü<select name="programType" [(ngModel)]="filters['programType']" (ngModelChange)="filterChanged()" class="w-full rounded border p-2"><option value="">Tümü</option><option *ngFor="let value of facets()['programType'] || []" [value]="value">{{display(value)}}</option></select></label>
          <label>Üniversite<input name="university" [(ngModel)]="filters['university']" maxlength="300" class="w-full rounded border p-2" placeholder="Üniversite ara"></label>
          <label>Fakülte<input name="faculty" [(ngModel)]="filters['faculty']" maxlength="300" class="w-full rounded border p-2" placeholder="Fakülte ara"></label>
          <label>Program<input name="programName" [(ngModel)]="filters['programName']" maxlength="300" class="w-full rounded border p-2" placeholder="Program adı ara"></label>
          <label>Puan türü<select [disabled]="!!scoreType" name="scoreType" [(ngModel)]="filters['scoreType']" class="w-full rounded border p-2"><option value="">Tümü</option><option *ngFor="let value of facets()['scoreType'] || []" [value]="value">{{display(value)}}</option></select></label>
        </ng-container>
        <ng-template #schoolFilters>
          <label>Okul adı<input name="search" [(ngModel)]="filters['search']" maxlength="300" class="w-full rounded border p-2"></label>
          <label>İl<select name="city" [(ngModel)]="filters['city']" (ngModelChange)="filterChanged('city')" class="w-full rounded border p-2"><option value="">Tümü</option><option *ngFor="let value of facets()['city'] || []" [value]="value">{{value}}</option></select></label>
          <label>İlçe<select name="district" [(ngModel)]="filters['district']" (ngModelChange)="filterChanged()" class="w-full rounded border p-2"><option value="">Tümü</option><option *ngFor="let value of facets()['district'] || []" [value]="value">{{value}}</option></select></label>
          <label>Okul türü<select name="institutionType" [(ngModel)]="filters['institutionType']" (ngModelChange)="filterChanged()" class="w-full rounded border p-2"><option value="">Tümü</option><option *ngFor="let value of facets()['institutionType'] || []" [value]="value">{{display(value)}}</option></select></label>
          <label>Dil<select name="language" [(ngModel)]="filters['language']" (ngModelChange)="filterChanged()" class="w-full rounded border p-2"><option value="">Tümü</option><option *ngFor="let value of facets()['language'] || []" [value]="value">{{value}}</option></select></label>
          <label>Alan<select name="field" [(ngModel)]="filters['field']" (ngModelChange)="filterChanged()" [disabled]="!(facets()['field'] || []).length" class="w-full rounded border p-2"><option value="">{{(facets()['field'] || []).length ? 'Tümü' : 'Kaynakta alan bilgisi yok'}}</option><option *ngFor="let value of facets()['field'] || []" [value]="value">{{value}}</option></select></label>
        </ng-template>
        <button type="submit" class="rounded bg-(--ui-brand) p-2 text-white">Ara</button>
      </fieldset>
    </form>
    <p *ngIf="error()" role="alert" class="mt-2 text-red-600">{{error()}}</p>
    <p *ngIf="busy()" role="status">Katalog yükleniyor…</p>
    <p *ngIf="searched() && !busy()" role="status" class="my-2">{{total()}} doğrulanmış kayıt</p>
    <p *ngIf="searched() && !total()" class="my-2 text-sm">Bu filtrelerle katalog kaydı bulunamadı.</p>
    <ul class="space-y-2">
      <li *ngFor="let item of items()" class="flex flex-wrap items-center justify-between gap-3 rounded border p-3">
        <div *ngIf="kind === 'program'; else schoolResult">
          <strong>{{item.name}}</strong><p class="text-sm">{{item.field}}</p>
          <p class="text-sm">{{item.institution}} · {{display(item.degree || '')}} · {{display(item.scoreType)}}</p>
          <p class="text-sm">Taban puan: {{item.minimumScore ?? 'Yayımlanmamış'}}</p>
        </div>
        <ng-template #schoolResult>
          <div><strong>{{item.institution}}</strong><p class="text-sm">{{item.city}} / {{item.district}} · {{display(item.institutionType || '')}}</p>
            <p class="text-sm">Dil: {{item.language ?? '—'}} · Alan: {{item.field ?? '—'}}</p>
            <p class="text-sm">Taban puan: {{item.minimumScore ?? 'Yayımlanmamış'}} · Yüzdelik: {{item.percentile ?? 'Yayımlanmamış'}} · Kontenjan: {{item.quota ?? 'Bilinmiyor'}}</p></div>
        </ng-template>
        <div class="flex items-center gap-3"><a [href]="item.sourceUrl" target="_blank" rel="noopener noreferrer" class="underline">Resmî kaynak</a>
          <button type="button" (click)="choose(item.id)" [disabled]="disabled || busy()" class="rounded border px-3 py-2">Bu hedefi bağla</button></div>
      </li>
    </ul>
    <div *ngIf="total() > 20" class="mt-3 flex gap-3"><button type="button" (click)="search(page-1)" [disabled]="page<=1 || busy()">Önceki</button><span>Sayfa {{page}} · {{total()}} kayıt</span><button type="button" (click)="search(page+1)" [disabled]="page*20>=total() || busy()">Sonraki</button></div>
  </section>` })
export class TargetAdmissionSearchComponent implements OnChanges {
  @Input() kind: 'school' | 'program' = 'program';
  @Input() scoreType: string | null = null;
  @Input() disabled = false;
  @Output() readonly selected = new EventEmitter<string>();
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly root = `${environment.apiUrl}/coaching/study-planning/targets/admissions`;
  private facetRequest?: Subscription;
  private resultRequest?: Subscription;
  readonly facets = signal<Record<string, string[]>>({});
  readonly items = signal<AdmissionItem[]>([]);
  readonly busy = signal(false);
  readonly searched = signal(false);
  readonly error = signal('');
  readonly total = signal(0);
  readonly year = 2026;
  page = 1;
  filters: Record<string, string> = {};

  display(value: string) {
    return ({ Associate: 'ÖNLİSANS', Bachelor: 'LİSANS', SOZ: 'SÖZ', DIL: 'YDT', 'DİL': 'YDT' } as Record<string, string>)[value] ?? value;
  }

  ngOnChanges(changes: import('@angular/core').SimpleChanges) {
    if (!changes['kind'] && !changes['scoreType']) return;
    this.resultRequest?.unsubscribe(); this.busy.set(false);
    this.page = 1; this.filters = this.scoreType ? {scoreType:this.scoreType} : {}; this.items.set([]); this.facets.set({});
    this.total.set(0); this.searched.set(false); this.error.set(''); this.loadFacets();
  }

  filterChanged(field?: string) {
    if (field === 'city') this.filters['district'] = '';
    this.resultRequest?.unsubscribe(); this.busy.set(false);
    this.page = 1; this.items.set([]); this.total.set(0); this.searched.set(false);
    this.loadFacets();
  }

  private params() {
    const params = new HttpParams().set('examCode', this.kind === 'school' ? 'LGS' : 'YKS').set('year', this.year);
    return Object.entries(this.filters).reduce((result, [key, value]) => value ? result.set(key, value) : result, params);
  }

  private loadFacets() {
    this.facetRequest?.unsubscribe();
    this.facetRequest = this.http.get<{ data: Record<string, string[]> }>(`${this.root}/filters`, { params: this.params() })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => this.facets.set(result.data),
        error: () => this.error.set('Katalog filtreleri yüklenemedi.')
      });
  }

  search(page = 1) {
    if (this.busy() || this.disabled) return;
    this.resultRequest?.unsubscribe();
    this.busy.set(true); this.items.set([]); this.error.set(''); this.searched.set(false);
    const params = this.params().set('pageNumber', page).set('pageSize', 20);
    this.resultRequest = this.http.get<{ data: { items: AdmissionItem[]; totalCount: number; pageNumber: number } }>(this.root, { params })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({
        next: result => { this.items.set(result.data.items); this.total.set(result.data.totalCount); this.page = result.data.pageNumber; this.searched.set(true); },
        error: () => this.error.set('Arama başarısız. Filtreleri kontrol et.')
      });
  }

  choose(id: string) {
    const item = this.items().find(result => result.id === id);
    if (item && !this.disabled && !this.busy()) this.selected.emit(item.targetId);
  }
  private readonly feedback = coachingFeedback(undefined, this.error);
}
