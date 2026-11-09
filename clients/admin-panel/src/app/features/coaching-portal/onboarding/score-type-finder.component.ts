import { HttpClient, HttpParams } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, OnInit, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, debounce, map, merge, of, startWith, switchMap, timer } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { Choice, yksFieldOf } from './onboarding-model';

/** A programme name and the score type it admits with, as the server lists it ("SAY", "EA", "SOZ", "YDT"). */
export interface ProgramField { name: string; scoreType: string }
/** What the student picked: the wizard's field (SAY, EA, SOZ, DIL) and the programme's name. */
export interface ScoreTypeFound { field: Choice; program: string }
interface Row { key: string; label: string; found: ScoreTypeFound }
type SearchState = { kind: 'idle' | 'short' | 'loading' | 'error' } | { kind: 'done'; rows: Row[] };

/** The shortest search sent to the server (the server lists nothing for less). */
const MIN_LETTERS = 2;
/** The most rows the server sends (it lists the closest names first). */
const LIMIT = 20;
let nextId = 0;
/** PR 137 review: the server's word separators (TurkishSearch); a search needs one word of at least two letters. */
export function searchable(term: string): boolean { return term.split(/[\s()\-/.,']+/).some(word => word.length >= MIN_LETTERS); }

/**
 * Paket H6 (09.11.2026): "Emin değil misin? Bölümüne göre bulalım". A student who knows the programme they want but not its score
 * type types its name; the undergraduate programmes of that name are listed with their score types across the country, and a tap
 * picks the score type. The parent applies it (the wizard answers its question; Çalışma tercihlerim only sets the form field).
 * A disclosure: a button with aria-expanded, a labelled search box, results as a list of buttons. The search waits for a pause
 * and drops the answer of an older search.
 */
@Component({
  selector: 'app-score-type-finder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stf">
      <button #toggle type="button" class="stf-toggle" [attr.aria-expanded]="open()" [attr.aria-controls]="open() ? id : null" (click)="setOpen(!open())">Emin değil misin? Bölümüne göre bulalım</button>
      @if (open()) {
        <div class="stf-panel" [id]="id">
          <label class="stf-label" [for]="id + '-search'">Bölüm adı</label>
          <input #box [id]="id + '-search'" type="search" autocomplete="off" enterkeyhint="search" placeholder="Örneğin bilgisayar mühendisliği"
            [attr.aria-describedby]="id + '-status ' + id + '-hint'" (input)="type($event)" (keydown.enter)="$event.preventDefault()" (keydown.escape)="close($event)">
          <small class="stf-hint" [id]="id + '-hint'">Önlisans bölümleri puan türüne bakmaz, TYT puanıyla öğrenci alır. Onlar için “Yalnız TYT (önlisans)”u seç.</small>
          <p class="stf-status" [id]="id + '-status'" role="status" aria-live="polite">{{ status() }}</p>
          @if (state().kind === 'error') {
            <div class="stf-error" role="alert"><span>Bölümler yüklenemedi.</span> <button type="button" class="stf-retry" (click)="retry()" (keydown.escape)="close($event)">Yeniden dene</button></div>
          }
          @if (rows().length) {
            <ul class="stf-list" aria-label="Bulunan bölümler">
              @for (row of rows(); track row.key) {
                <li><button type="button" class="stf-row" (click)="choose(row)" (keydown.escape)="close($event)">{{ row.label }}</button></li>
              }
            </ul>
          }
        </div>
      }
    </div>`,
  styles: [`
    :host { display: block; }
    .stf { display: grid; gap: .5rem; }
    .stf-toggle, .stf-retry { font: inherit; font-size: .9rem; border: 0; background: none; color: var(--ui-brand); text-decoration: underline;
      cursor: pointer; padding: .35rem 0; justify-self: start; min-height: 2.25rem; text-align: left; }
    .stf-panel { display: grid; gap: .45rem; padding: .75rem; border: 1px solid var(--ui-border); border-radius: .75rem; background: var(--ui-surface); }
    .stf-label { font-weight: 600; font-size: .9rem; }
    .stf-panel input { font: inherit; min-height: 2.75rem; padding: .5rem .7rem; border: 1px solid var(--ui-border); border-radius: .6rem;
      background: var(--ui-page); color: inherit; width: 100%; box-sizing: border-box; }
    .stf-hint, .stf-status { margin: 0; color: var(--ui-text-muted); font-size: .85rem; }
    .stf-status:empty { display: none; }
    .stf-error { color: var(--ui-danger); font-size: .9rem; }
    .stf-list { list-style: none; margin: 0; padding: 0; display: grid; gap: .35rem; max-height: 18rem; overflow: auto; }
    .stf-row { font: inherit; width: 100%; text-align: left; min-height: 2.75rem; padding: .5rem .75rem; border: 1px solid var(--ui-border);
      border-radius: .6rem; background: var(--ui-page); color: inherit; cursor: pointer; }
    .stf-row:hover { border-color: var(--ui-brand); }
    button:focus-visible, input:focus-visible { outline: 3px solid var(--coaching-focus, var(--ui-brand)); outline-offset: 2px; }
  `]
})
export class ScoreTypeFinderComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);
  private readonly url = `${environment.apiUrl}/coaching/students/me/onboarding/options/programs/fields`;
  readonly chosen = output<ScoreTypeFound>();
  readonly id = `stf-${nextId++}`;
  readonly open = signal(false);
  readonly state = signal<SearchState>({ kind: 'idle' });
  private readonly typed = new Subject<string>();
  private readonly retried = new Subject<string>();
  /** Reopening starts over at once (and drops a search still running). */
  private readonly resets = new Subject<string>();
  private term = '';

  rows(): Row[] { const state = this.state(); return state.kind === 'done' ? state.rows : []; }
  status(): string {
    const state = this.state();
    switch (state.kind) {
      case 'idle': return 'Bölümünün adını yaz; hangi puan türüyle öğrenci aldığını gösterelim.';
      case 'short': return `Aramak için en az ${MIN_LETTERS} harf yaz.`;
      case 'loading': return 'Aranıyor…';
      case 'error': return '';
      case 'done': return !state.rows.length ? 'Bu adla bir lisans bölümü bulamadık. Başka bir kelime dene.'
        : `${state.rows.length} bölüm bulundu.${state.rows.length >= LIMIT ? ' En yakın sonuçları gösteriyoruz; daha fazlası için aramanı daralt.' : ''}`;
    }
  }

  ngOnInit() {
    // debounce with a timer, not debounceTime: the pause does not depend on the clock (pages that fix the date in tests).
    // PR 137 review: no distinctUntilChanged: the same term typed again after a cleared box is searched again.
    // A reset also drops a pause still waiting (the typed stream starts over).
    const typed = this.resets.pipe(startWith(''), switchMap(() => this.typed.pipe(debounce(() => timer(300)), map(x => x.trim()))));
    merge(typed, this.retried, this.resets)
      .pipe(switchMap(term => this.search(term)), takeUntilDestroyed(this.destroyRef))
      .subscribe(state => this.state.set(state));
  }

  setOpen(open: boolean) {
    if (open) { this.term = ''; this.state.set({ kind: 'idle' }); this.resets.next(''); }
    this.open.set(open);
    if (open) setTimeout(() => (this.host.nativeElement.querySelector('input[type="search"]') as HTMLInputElement | null)?.focus());
  }
  close(event: Event) {
    event.preventDefault(); event.stopPropagation();
    this.open.set(false);
    setTimeout(() => (this.host.nativeElement.querySelector('.stf-toggle') as HTMLElement | null)?.focus());
  }
  type(event: Event) {
    const value = (event.target as HTMLInputElement).value;
    // Typing shows the short-search note at once; the request waits for a pause (switchMap drops an older answer).
    if (!searchable(value.trim())) this.state.set({ kind: value.trim() ? 'short' : 'idle' });
    this.typed.next(value);
  }
  retry() { this.retried.next(this.term); }
  /** The helper closes and the focus goes back to its button (in the wizard the next question takes the focus instead). */
  choose(row: Row) {
    this.chosen.emit(row.found);
    this.open.set(false);
    setTimeout(() => (this.host.nativeElement.querySelector('.stf-toggle') as HTMLElement | null)?.focus());
  }

  private search(term: string): Observable<SearchState> {
    this.term = term;
    if (!searchable(term)) return of<SearchState>({ kind: term ? 'short' : 'idle' });
    return this.http.get<{ data: ProgramField[] }>(this.url, { params: new HttpParams().set('search', term) }).pipe(
      map(r => ({ kind: 'done' as const, rows: rowsOf(r.data ?? []) })),
      catchError(() => of<SearchState>({ kind: 'error' })),
      startWith<SearchState>({ kind: 'loading' }));
  }
}

/** One row per name and field; a TYT (önlisans) entry has no field and is not offered. */
export function rowsOf(list: ProgramField[]): Row[] {
  const rows: Row[] = []; const seen = new Set<string>();
  for (const item of list) {
    const field = yksFieldOf(item.scoreType); const key = `${item.name}|${field?.value}`;
    if (!field || seen.has(key)) continue;
    seen.add(key);
    rows.push({ key, label: `${item.name} · ${field.label}`, found: { field, program: item.name } });
  }
  return rows;
}
