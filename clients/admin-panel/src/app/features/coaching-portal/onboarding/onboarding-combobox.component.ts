import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, OnInit, ViewChild, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, debounceTime, switchMap, catchError, of } from 'rxjs';

export interface ComboOption { value: string; label: string; hint?: string; data?: Record<string, unknown> }
export interface ComboPage { items: ComboOption[]; total: number }
let nextId = 0;

/**
 * Paket C (#3): a searchable list (ARIA combobox). A short list is filtered here (`options`); a long one is searched and paged
 * on the server (`load`), so the browser never holds a whole catalog. Arrow keys move, Enter picks, Escape closes.
 */
@Component({
  selector: 'app-onboarding-combobox',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="ob-combo">
      <input #box type="text" role="combobox" autocomplete="off" [id]="id + '-input'" [attr.aria-label]="label()" aria-autocomplete="list"
        [attr.aria-expanded]="open()" [attr.aria-controls]="id + '-list'" [attr.aria-activedescendant]="active() >= 0 ? id + '-opt-' + active() : null"
        [attr.aria-invalid]="invalid() ? 'true' : null" [attr.aria-describedby]="describedBy()"
        [placeholder]="placeholder()" [value]="query()" (input)="type($event)" (focus)="open.set(true)" (keydown)="key($event)">
      @if (open()) {
        <!-- Only options inside the listbox; the empty state and "more" sit next to it (ARIA combobox pattern). -->
        <ul class="ob-combo-list" role="listbox" [id]="id + '-list'" [attr.aria-label]="label()">
          @for (option of shown(); track option.value; let i = $index) {
            <li role="option" [id]="id + '-opt-' + i" [attr.aria-selected]="i === active()" [class.is-active]="i === active()"
              (mousedown)="$event.preventDefault()" (click)="pick(option)">
              <span>{{ option.label }}</span>@if (option.hint) { <small>{{ option.hint }}</small> }
            </li>
          }
        </ul>
        @if (!shown().length) { <p class="ob-combo-empty" role="status">{{ loading() ? 'Yükleniyor…' : 'Sonuç yok. Başka bir şey yaz.' }}</p> }
        @if (more()) { <p class="ob-combo-more"><button type="button" (mousedown)="$event.preventDefault()" (click)="loadMore()">Daha fazla göster</button></p> }
      }
      @if (error()) { <p class="ob-error" role="alert">{{ error() }}</p> }
    </div>`
})
export class OnboardingComboboxComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  readonly label = input.required<string>();
  readonly placeholder = input('Yazarak ara');
  /** F2 review: a refused field marks its list (focus-first-invalid reaches it) and points at its message. */
  readonly invalid = input(false);
  readonly describedBy = input<string | null>(null);
  /** A short list, filtered in the browser. */
  readonly options = input<ComboOption[] | null>(null);
  /** A long list, searched and paged on the server. */
  readonly load = input<((search: string, page: number) => Observable<ComboPage>) | null>(null);
  readonly picked = output<ComboOption>();
  @ViewChild('box', { static: true }) private box!: ElementRef<HTMLInputElement>;

  readonly id = `ob-combo-${nextId++}`;
  readonly query = signal('');
  readonly open = signal(false);
  readonly active = signal(-1);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  private readonly remote = signal<ComboOption[]>([]);
  private readonly total = signal(0);
  private page = 1;
  private readonly searches = new Subject<{ search: string; page: number }>();

  readonly shown = computed(() => {
    const options = this.options();
    if (!options) return this.remote();
    const text = fold(this.query());
    return text ? options.filter(x => fold(x.label).includes(text)) : options;
  });
  readonly more = computed(() => !this.options() && this.remote().length < this.total());

  ngOnInit() {
    this.searches.pipe(debounceTime(200), switchMap(({ search, page }) => {
      this.loading.set(true); this.error.set(null);
      return (this.load()?.(search, page) ?? of({ items: [], total: 0 })).pipe(catchError(() => {
        this.error.set('Liste yüklenemedi. Yeniden yazmayı dene.'); return of(null);
      }));
    }), takeUntilDestroyed(this.destroyRef)).subscribe(result => {
      this.loading.set(false);
      if (!result) return;
      this.remote.update(current => this.page === 1 ? result.items : [...current, ...result.items]);
      this.total.set(result.total);
    });
    if (this.load()) this.search();
    queueMicrotask(() => this.box.nativeElement.focus());
  }

  type(event: Event) {
    this.query.set((event.target as HTMLInputElement).value); this.open.set(true); this.active.set(-1);
    if (this.load()) this.search();
  }

  key(event: KeyboardEvent) {
    const count = this.shown().length;
    if (event.key === 'ArrowDown') { event.preventDefault(); this.open.set(true); this.active.set(Math.min(count - 1, this.active() + 1)); }
    else if (event.key === 'ArrowUp') { event.preventDefault(); this.active.set(Math.max(0, this.active() - 1)); }
    else if (event.key === 'Enter') {
      event.preventDefault();
      const option = this.shown()[this.active() >= 0 ? this.active() : count === 1 ? 0 : -1];
      if (option) this.pick(option);
    } else if (event.key === 'Escape') { this.open.set(false); this.active.set(-1); }
  }

  pick(option: ComboOption) { this.open.set(false); this.query.set(option.label); this.picked.emit(option); }
  loadMore() { this.page++; this.searches.next({ search: this.query(), page: this.page }); }
  private search() { this.page = 1; this.searches.next({ search: this.query(), page: 1 }); }
}

/** Case- and Turkish-i-insensitive matching. */
export function fold(value: string): string {
  return value.toLocaleLowerCase('tr').replace(/ı/g, 'i').normalize('NFD').replace(/[̀-ͯ]/g, '');
}
