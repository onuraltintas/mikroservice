import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { CoachingStudyPlanningService, StudyTopic } from '../../core/services/coaching-study-planning.service';
import {
  CoachingResourcesService, RESOURCE_KINDS, RESOURCE_UNITS, ResourceKind, ResourceTarget, ResourceUnit, StudentResource, StudentResourceInput,
  TeacherStudentChoice, resourceKindLabel, resourceUnitWord
} from '../../core/services/coaching-resources.service';

interface SectionDraft { title: string; fromUnit: number | null; toUnit: number | null; topicId: string | null; topicName?: string | null;
  topicQuery?: string; topicResults?: StudyTopic[] | null }
interface ResourceDraft {
  id: string | null; version: number | null; name: string; publisher: string; lessonName: string; kind: ResourceKind; unit: ResourceUnit;
  totalUnits: number | null; doneBefore: number | null; sections: SectionDraft[];
}

const blank = (): ResourceDraft => ({ id: null, version: null, name: '', publisher: '', lessonName: '', kind: 'QuestionBank', unit: 'Page',
  totalUnits: null, doneBefore: 0, sections: [] });

/** What the draft is missing, in the reader's words; null when it can be saved. */
export function resourceDraftProblem(draft: ResourceDraft, sen = true): string | null {
  if (!draft.name.trim()) return sen ? 'Kaynağın adını yaz.' : 'Kaynağın adını yazın.';
  const total = Number(draft.totalUnits);
  if (!Number.isInteger(total) || total < 1 || total > 10000) return `Toplam ${resourceUnitWord(draft.unit)} sayısı 1 ile 10.000 arasında olmalı.`;
  const before = Number(draft.doneBefore ?? 0);
  if (!Number.isInteger(before) || before < 0 || before > total) return 'Önceden bitirilen kısım toplamı aşamaz.';
  for (const section of draft.sections) {
    if (!section.title.trim()) return 'Her bölümün bir adı olmalı.';
    const from = Number(section.fromUnit), to = Number(section.toUnit);
    if (!Number.isInteger(from) || !Number.isInteger(to) || from < 1 || to < from || to > total)
      return `“${section.title.trim()}” bölümünün aralığı 1–${total} içinde olmalı.`;
  }
  return null;
}

/**
 * KocApp uyumu (22.11.2026): the student's books and sets with their progress ("Kaynaklarım"). The same component serves the
 * student (sen), her teacher (siz, read and write), her institution (siz, read) and her parent (read; the archived ones stay out).
 * Progress grows by itself when a plan task that points at a range of the resource is completed.
 */
@Component({
  selector: 'app-student-resources',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="res" [attr.aria-labelledby]="headingId">
      <div class="res-head">
        <h2 [id]="headingId">{{ title() }}</h2>
        @if (canEdit() && !draft()) { <button type="button" class="res-primary" (click)="startNew()">Kaynak ekle</button> }
      </div>
      <p class="res-muted">{{ intro() }}</p>

      @if (error()) { <p class="res-alert" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="res-notice" role="status">{{ notice() }}</p> }

      @if (draft(); as d) {
        <form class="res-form" (ngSubmit)="save()" data-testid="resource-form">
          <h3>{{ d.id ? 'Kaynağı düzenle' : 'Yeni kaynak' }}</h3>
          <label>Ad<input name="name" [(ngModel)]="d.name" maxlength="200" required autocomplete="off" placeholder="Örneğin: Matematik Soru Bankası"></label>
          <div class="res-grid">
            <label>Yayınevi <small>(isteğe bağlı)</small><input name="publisher" [(ngModel)]="d.publisher" maxlength="120" autocomplete="off"></label>
            <label>Ders <small>(isteğe bağlı)</small><input name="lesson" [(ngModel)]="d.lessonName" maxlength="100" autocomplete="off"></label>
            <label>Tür<select name="kind" [(ngModel)]="d.kind">@for (kind of kinds; track kind.value) { <option [value]="kind.value">{{ kind.label }}</option> }</select></label>
          </div>
          <fieldset class="res-units">
            <legend>İlerleme neyle sayılsın?</legend>
            @for (unit of units; track unit.value) {
              <label><input type="radio" name="unit" [value]="unit.value" [(ngModel)]="d.unit"><span>{{ unit.label }}</span></label>
            }
          </fieldset>
          <div class="res-grid">
            <label>Toplam {{ word(d.unit) }}<input type="number" name="total" [(ngModel)]="d.totalUnits" min="1" max="10000" step="1" required inputmode="numeric"></label>
            <label>Önceden bitirilen {{ word(d.unit) }} <small>(baştan)</small><input type="number" name="before" [(ngModel)]="d.doneBefore" min="0" max="10000" step="1" inputmode="numeric"></label>
          </div>
          <fieldset class="res-sections">
            <legend>Bölümler <small>(isteğe bağlı)</small></legend>
            <p class="res-muted">{{ sen() ? 'Üniteleri ya da konuları aralıklarıyla yazarsan' : 'Üniteleri ya da konuları aralıklarıyla yazarsanız' }} ilerleme bölüm bölüm görünür ve plana sıradaki bölüm önerilir.</p>
            @for (section of d.sections; track $index; let i = $index) {
              <div class="res-section">
                <label>Bölüm adı<input [name]="'section-title-' + i" [(ngModel)]="section.title" maxlength="200" required autocomplete="off"></label>
                <label>İlk {{ word(d.unit) }}<input type="number" [name]="'section-from-' + i" [(ngModel)]="section.fromUnit" min="1" step="1" required inputmode="numeric"></label>
                <label>Son {{ word(d.unit) }}<input type="number" [name]="'section-to-' + i" [(ngModel)]="section.toUnit" min="1" step="1" required inputmode="numeric"></label>
                <button type="button" class="res-link" (click)="removeSection(i)">Bölümü kaldır</button>
                <!-- KocApp plan, phase 2: a section tied to a topic gets the topic's plan tasks (its next free part). -->
                <div class="res-topic">
                  @if (section.topicId) {
                    <p>Konu: <b>{{ section.topicName || 'Seçili konu' }}</b> <button type="button" class="res-link" (click)="clearTopic(section)">Konuyu kaldır</button></p>
                  } @else {
                    <label>Konu <small>(isteğe bağlı; plan bu konunun çalışmalarına bu bölümü önerir)</small>
                      <span class="res-topic-search"><input [name]="'section-topic-' + i" [(ngModel)]="section.topicQuery" maxlength="100" autocomplete="off" (keydown.enter)="$event.preventDefault(); searchTopic(section)">
                      <button type="button" class="res-secondary" (click)="searchTopic(section)" [disabled]="!(section.topicQuery || '').trim()">Konu ara</button></span></label>
                    @if (section.topicResults; as results) {
                      @if (!results.length) { <p class="res-muted" role="status">Bu adla bir konu bulunamadı.</p> }
                      <ul class="res-topic-results">@for (topic of results; track topic.id) {
                        <li><button type="button" class="res-link" (click)="pickTopic(section, topic)">{{ topic.name }}<span class="res-muted">, {{ topic.lessonName }}@if (topic.gradeNumber) {, {{ topic.gradeNumber }}. sınıf}</span></button></li>
                      }</ul>
                    }
                  }
                </div>
              </div>
            }
            <button type="button" class="res-secondary" (click)="addSection()" [disabled]="d.sections.length >= 300">Bölüm ekle</button>
          </fieldset>
          <div class="res-actions">
            <button type="submit" class="res-primary" [disabled]="busy()">{{ d.id ? 'Değişiklikleri kaydet' : 'Kaynağı ekle' }}</button>
            <button type="button" class="res-secondary" (click)="cancel()" [disabled]="busy()">Vazgeç</button>
          </div>
        </form>
      }

      @if (loading() && !items().length) { <p class="res-muted" role="status">Kaynaklar yükleniyor…</p> }
      @else if (!items().length && !draft()) {
        <p class="res-empty" data-testid="resources-empty">{{ emptyText() }}</p>
      }

      <ul class="res-list">
        @for (item of items(); track item.id) {
          <li class="res-card" [class.is-archived]="item.isArchived" data-testid="resource-card">
            <div class="res-card-head">
              <h3>{{ item.name }}</h3>
              <span class="res-tag">{{ kindLabel(item.kind) }}</span>
              @if (item.isArchived) { <span class="res-tag">Arşivde</span> }
            </div>
            @if (item.publisher || item.lessonName) { <p class="res-muted">{{ meta(item) }}</p> }
            <div class="res-meter" role="meter" aria-valuemin="0" aria-valuemax="100" [attr.aria-valuenow]="item.percent"
              [attr.aria-label]="item.name + ' ilerlemesi'" [attr.aria-valuetext]="progressText(item)">
              <span class="res-meter-fill" [style.width.%]="item.percent"></span>
            </div>
            <p class="res-progress"><b>%{{ item.percent }}</b> {{ progressText(item) }}</p>
            @if (item.plannedUnits > 0) { <p class="res-muted">{{ item.plannedUnits }} {{ word(item.unit) }} planda bekliyor.</p> }
            @if (item.next && audience() !== 'parent') {
              <p class="res-next" data-testid="resource-next">Sıradaki: {{ word(item.unit) }} {{ range(item.next.fromUnit, item.next.toUnit) }}@if (item.next.sectionTitle) {, {{ item.next.sectionTitle }}}</p>
            }
            @if (item.sections.length) {
              <details class="res-details"><summary>Bölümler ({{ item.sections.length }})</summary>
                <ul>@for (section of item.sections; track $index) {
                  <li><span>{{ section.title }}</span><span class="res-muted">{{ section.doneUnits ?? 0 }} / {{ section.toUnit - section.fromUnit + 1 }} {{ word(item.unit) }}, {{ word(item.unit) }} {{ range(section.fromUnit, section.toUnit) }}</span></li>
                }</ul>
              </details>
            }
            @if (canEdit()) {
              <div class="res-actions">
                @if (!item.isArchived) { <button type="button" class="res-secondary" (click)="edit(item)" [disabled]="busy()">Düzenle</button> }
                @if (canCopy() && !item.isArchived) { <button type="button" class="res-secondary" (click)="startCopy(item)" [disabled]="busy()" [attr.aria-expanded]="copying() === item.id">Diğer öğrencilere kopyala</button> }
                <button type="button" class="res-secondary" (click)="archive(item, !item.isArchived)" [disabled]="busy()">{{ item.isArchived ? 'Arşivden çıkar' : (item.percent >= 100 ? 'Bitti, arşivle' : 'Arşivle') }}</button>
                @if (item.canDelete) {
                  @if (confirmDelete() === item.id) {
                    <span class="res-confirm" role="group" aria-label="Silmeyi onayla">
                      <span>Silinsin mi?</span>
                      <button type="button" class="res-danger" (click)="remove(item)" [disabled]="busy()">Evet, sil</button>
                      <button type="button" class="res-secondary" (click)="confirmDelete.set(null)">Hayır</button>
                    </span>
                  } @else { <button type="button" class="res-link" (click)="confirmDelete.set(item.id)" [disabled]="busy()">Sil</button> }
                }
              </div>
              @if (copying() === item.id) {
                <fieldset class="res-copy" data-testid="resource-copy">
                  <legend>Bu kaynağı hangi öğrencilerinize ekleyelim?</legend>
                  @if (classmates() === null) { <p class="res-muted" role="status">Öğrencileriniz yükleniyor…</p> }
                  @else if (!classmates()!.length) { <p class="res-muted">Başka öğrenciniz yok.</p> }
                  @else {
                    @for (choice of classmates()!; track choice.userId) {
                      <label><input type="checkbox" [checked]="copyIds().includes(choice.userId)" (change)="toggleCopy(choice.userId, $any($event.target).checked)"> {{ choice.fullName }}</label>
                    }
                  }
                  <p class="res-muted">Kopyada bölümler aynı kalır, ilerleme sıfırdan başlar. Aynı adlı kaynağı olan öğrenci atlanır.</p>
                  <div class="res-actions">
                    <button type="button" class="res-primary" (click)="copy(item)" [disabled]="busy() || !copyIds().length">Kopyala</button>
                    <button type="button" class="res-secondary" (click)="copying.set(null)">Vazgeç</button>
                  </div>
                </fieldset>
              }
            }
          </li>
        }
      </ul>

      @if (audience() !== 'parent') {
        <label class="res-toggle"><input type="checkbox" [checked]="showArchived()" (change)="toggleArchived($any($event.target).checked)"> Arşivdekileri de göster</label>
      }
    </section>`,
  styles: [`
    :host { display: block; }
    .res { display: grid; gap: 12px; color: var(--ui-text); }
    .res-head { display: flex; flex-wrap: wrap; gap: 8px 16px; align-items: center; justify-content: space-between; }
    .res-head h2 { margin: 0; font-size: 20px; }
    .res-muted { margin: 0; color: var(--ui-text-muted); font-size: 14px; }
    .res-empty { margin: 0; padding: 16px; border: 1px dashed var(--ui-border-strong); border-radius: 10px; color: var(--ui-text-muted); }
    .res-alert { margin: 0; padding: 10px 12px; border-radius: 8px; background: var(--ui-danger-soft, #fde8e8); color: var(--ui-danger-text, #7f1d1d); }
    .res-notice { margin: 0; padding: 10px 12px; border-radius: 8px; background: var(--ui-success-soft, #dcf5e6); color: var(--ui-success-text, #14532d); }
    .res-list { display: grid; gap: 10px; margin: 0; padding: 0; list-style: none; }
    .res-card { display: grid; gap: 6px; padding: 14px 16px; border: 1px solid var(--ui-border); border-radius: 10px; background: var(--ui-surface); }
    .res-card.is-archived { opacity: .8; }
    .res-card-head { display: flex; flex-wrap: wrap; gap: 6px 8px; align-items: center; }
    .res-card-head h3 { margin: 0; font-size: 16px; }
    .res-tag { padding: 1px 8px; border-radius: 999px; background: var(--ui-surface-muted); font-size: 13px; color: var(--ui-text); }
    .res-meter { height: 10px; border-radius: 999px; background: var(--ui-surface-muted); overflow: hidden; border: 1px solid var(--ui-border); }
    .res-meter-fill { display: block; height: 100%; background: var(--ui-brand, var(--ui-accent)); }
    .res-progress { margin: 0; font-variant-numeric: tabular-nums; }
    .res-next { margin: 0; font-weight: 600; }
    .res-details summary { cursor: pointer; min-height: 32px; display: flex; align-items: center; color: var(--ui-brand-text); }
    .res-details ul { display: grid; gap: 4px; margin: 6px 0 0; padding: 0; list-style: none; }
    .res-details li { display: flex; flex-wrap: wrap; gap: 4px 12px; justify-content: space-between; font-variant-numeric: tabular-nums; }
    .res-actions { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    .res-confirm { display: inline-flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    .res-form { display: grid; gap: 12px; padding: 16px; border: 1px solid var(--ui-border-strong); border-radius: 10px; background: var(--ui-surface); }
    .res-form h3 { margin: 0; }
    .res-form label { display: grid; gap: 4px; font-size: 14px; font-weight: 600; }
    .res-form small { font-weight: 400; color: var(--ui-text-muted); }
    .res-form input, .res-form select { min-height: 44px; padding: 6px 10px; border: 1px solid var(--ui-border-strong); border-radius: 8px; background: var(--ui-surface); color: var(--ui-text); font: inherit; }
    .res-grid { display: grid; gap: 12px; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); }
    .res-units, .res-sections { display: flex; flex-wrap: wrap; gap: 8px; margin: 0; padding: 0; border: 0; }
    .res-sections { display: grid; }
    .res-units legend, .res-sections legend { width: 100%; padding: 0; margin-bottom: 6px; font-weight: 700; }
    .res-units label { position: relative; }
    .res-units input { position: absolute; opacity: 0; width: 1px; height: 1px; }
    .res-units span { display: inline-flex; align-items: center; min-height: 44px; padding: 0 16px; border: 1px solid var(--ui-border-strong); border-radius: 999px; cursor: pointer; font-weight: 600; }
    .res-units input:checked + span { background: var(--ui-brand, var(--ui-accent)); border-color: var(--ui-brand, var(--ui-accent)); color: var(--ui-on-brand, #fff); }
    .res-units input:focus-visible + span { outline: 3px solid var(--ui-focus, #f59e0b); outline-offset: 2px; }
    .res-section { display: grid; gap: 8px; grid-template-columns: 2fr 1fr 1fr auto; align-items: end; padding-bottom: 10px; border-bottom: 1px solid var(--ui-border); }
    .res-topic { grid-column: 1 / -1; }
    .res-topic p { margin: 0; }
    .res-topic-search { display: flex; gap: 8px; }
    .res-topic-search input { flex: 1; }
    .res-topic-results { display: grid; gap: 2px; margin: 4px 0 0; padding: 0; list-style: none; }
    .res-topic-results button { text-align: left; min-height: 36px; text-decoration: none; }
    @media (max-width: 640px) { .res-section { grid-template-columns: 1fr 1fr; } .res-section label:first-child { grid-column: 1 / -1; } }
    .res-primary, .res-secondary, .res-danger, .res-link { min-height: 44px; padding: 0 16px; border-radius: 8px; font: inherit; font-weight: 700; cursor: pointer; }
    .res-primary { border: 1px solid var(--ui-brand, var(--ui-accent)); background: var(--ui-brand, var(--ui-accent)); color: var(--ui-on-brand, #fff); }
    .res-secondary { border: 1px solid var(--ui-border-strong); background: var(--ui-surface); color: var(--ui-text); }
    .res-danger { border: 1px solid var(--ui-danger, #b91c1c); background: var(--ui-danger, #b91c1c); color: #fff; }
    .res-link { border: 0; background: none; color: var(--ui-brand-text); text-decoration: underline; padding: 0 8px; }
    button:disabled { opacity: .55; cursor: not-allowed; }
    button:focus-visible, summary:focus-visible, input:focus-visible, select:focus-visible { outline: 3px solid var(--ui-focus, #f59e0b); outline-offset: 2px; }
    .res-copy { display: grid; gap: 6px; margin: 4px 0 0; padding: 12px; border: 1px solid var(--ui-border); border-radius: 8px; }
    .res-copy legend { padding: 0 4px; font-weight: 700; }
    .res-copy label { display: flex; gap: 8px; align-items: center; min-height: 36px; }
    .res-toggle { display: inline-flex; gap: 8px; align-items: center; min-height: 44px; font-size: 14px; }
  `]
})
export class StudentResourcesComponent implements OnInit {
  private readonly api = inject(CoachingResourcesService);
  private readonly topics = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  /** null: the signed-in student. */
  readonly studentId = input<string | null>(null);
  readonly audience = input<'student' | 'staff' | 'parent'>('student');
  /** Staff who may only read (an institution administrator). */
  readonly readonly = input(false);
  readonly childName = input('');

  readonly items = signal<StudentResource[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly draft = signal<ResourceDraft | null>(null);
  readonly showArchived = signal(false);
  readonly confirmDelete = signal<string | null>(null);
  readonly kinds = RESOURCE_KINDS;
  readonly units = RESOURCE_UNITS;
  readonly word = resourceUnitWord;
  readonly kindLabel = resourceKindLabel;
  readonly headingId = `resources-${Math.random().toString(36).slice(2, 9)}`;

  readonly canEdit = computed(() => this.audience() !== 'parent' && !this.readonly());
  /** The student and the parent are addressed with "sen", staff with "siz". */
  readonly sen = computed(() => this.audience() !== 'staff');
  v(sen: string, siz: string) { return this.sen() ? sen : siz; }
  /** KocApp plan, phase 2: a teacher gives the same book to other students of hers. */
  readonly canCopy = computed(() => this.audience() === 'staff' && !this.readonly() && !!this.studentId());
  readonly copying = signal<string | null>(null);
  readonly copyIds = signal<string[]>([]);
  readonly classmates = signal<TeacherStudentChoice[] | null>(null);
  readonly title = computed(() => this.audience() === 'student' ? 'Kaynaklarım' : 'Kaynaklar');
  readonly intro = computed(() => {
    switch (this.audience()) {
      case 'student': return 'Çalıştığın kitapları ve setleri ekle. Planındaki bir çalışmayı kaynağın bir aralığına bağladığında, çalışmayı tamamlayınca ilerleme kendiliğinden artar.';
      case 'parent': return `${this.childName() || 'Çocuğunun'} çalıştığı kitaplar ve ne kadarının bittiği.`;
      default: return this.readonly()
        ? 'Öğrencinin çalıştığı kitaplar ve setler, ne kadarının bittiğiyle.'
        : 'Öğrencinin çalıştığı kitapları ve setleri ekleyin. Plandaki çalışmaları bir aralığa bağladığınızda, tamamlanan çalışmalar ilerlemeye eklenir.';
    }
  });
  readonly emptyText = computed(() => this.audience() === 'student'
    ? 'Henüz kaynak eklemedin. Kullandığın soru bankasını ya da konu anlatım kitabını ekleyerek başla.'
    : this.audience() === 'parent' ? 'Henüz kaynak eklenmedi.'
    : this.readonly() ? 'Öğrencinin henüz kaynağı yok.' : 'Öğrencinin henüz kaynağı yok. Kullandığı soru bankasını ya da konu anlatım kitabını ekleyerek başlayın.');

  private get target(): ResourceTarget { return { studentId: this.studentId(), parent: this.audience() === 'parent' }; }

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.list(this.target, this.showArchived() && this.audience() !== 'parent').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: items => { this.items.set(items); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set(this.v('Kaynaklar şu an yüklenemedi. Sayfayı yenileyip yeniden dene.', 'Kaynaklar şu an yüklenemedi. Sayfayı yenileyip yeniden deneyin.')); }
    });
  }

  toggleArchived(value: boolean) { this.showArchived.set(value); this.load(); }

  meta(item: StudentResource): string { return [item.publisher, item.lessonName].filter(x => !!x).join(', '); }
  progressText(item: StudentResource): string {
    return `${item.doneUnits} / ${item.totalUnits} ${resourceUnitWord(item.unit)} bitti`;
  }
  range(from: number, to: number) { return from === to ? `${from}` : `${from}–${to}`; }

  startNew() { this.clear(); this.draft.set(blank()); }
  edit(item: StudentResource) {
    this.clear();
    this.draft.set({ id: item.id, version: item.version, name: item.name, publisher: item.publisher ?? '', lessonName: item.lessonName ?? '',
      kind: item.kind, unit: item.unit, totalUnits: item.totalUnits, doneBefore: item.doneBefore,
      sections: item.sections.map(x => ({ title: x.title, fromUnit: x.fromUnit, toUnit: x.toUnit, topicId: x.topicId ?? null, topicName: x.topicName ?? null })) });
  }
  cancel() { this.draft.set(null); this.error.set(null); }
  addSection() {
    const d = this.draft(); if (!d) return;
    const last = d.sections.at(-1)?.toUnit ?? 0;
    d.sections.push({ title: '', fromUnit: last + 1, toUnit: null, topicId: null });
    this.draft.set({ ...d });
  }
  searchTopic(section: SectionDraft) {
    const query = (section.topicQuery ?? '').trim(); if (!query) return;
    this.topics.searchTopics(query, null, '').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: page => { section.topicResults = page.items.slice(0, 8); this.draft.set({ ...this.draft()! }); },
      error: () => this.error.set('Konular şu an aranamadı.')
    });
  }
  pickTopic(section: SectionDraft, topic: StudyTopic) {
    section.topicId = topic.id; section.topicName = topic.name; section.topicResults = null; section.topicQuery = '';
    if (!section.title.trim()) section.title = topic.name.slice(0, 200);
    this.draft.set({ ...this.draft()! });
  }
  clearTopic(section: SectionDraft) { section.topicId = null; section.topicName = null; this.draft.set({ ...this.draft()! }); }
  removeSection(index: number) { const d = this.draft(); if (!d) return; d.sections.splice(index, 1); this.draft.set({ ...d }); }

  save() {
    const d = this.draft(); if (!d) return;
    const problem = resourceDraftProblem(d, this.sen());
    if (problem) { this.error.set(problem); return; }
    const input: StudentResourceInput = {
      name: d.name.trim(), publisher: d.publisher.trim() || null, lessonName: d.lessonName.trim() || null, kind: d.kind, unit: d.unit,
      totalUnits: Number(d.totalUnits), doneBefore: Number(d.doneBefore ?? 0),
      sections: d.sections.map(x => ({ title: x.title.trim(), fromUnit: Number(x.fromUnit), toUnit: Number(x.toUnit), topicId: x.topicId })),
      expectedVersion: d.version
    };
    const request = d.id ? this.api.update(this.target, d.id, input) : this.api.create(this.target, input);
    this.run(request, saved => {
      this.items.set(d.id ? this.items().map(x => x.id === saved.id ? saved : x) : [...this.items(), saved]);
      this.draft.set(null);
      this.notice.set(d.id ? 'Kaynak güncellendi.' : 'Kaynak eklendi.');
    });
  }

  archive(item: StudentResource, archived: boolean) {
    this.clear();
    this.run(this.api.archive(this.target, item, archived), saved => {
      this.items.set(archived && !this.showArchived() ? this.items().filter(x => x.id !== saved.id) : this.items().map(x => x.id === saved.id ? saved : x));
      this.notice.set(archived ? 'Kaynak arşivlendi.' : 'Kaynak arşivden çıkarıldı.');
    });
  }

  remove(item: StudentResource) {
    this.clear();
    this.run(this.api.remove(this.target, item), () => {
      this.items.set(this.items().filter(x => x.id !== item.id));
      this.confirmDelete.set(null);
      this.notice.set('Kaynak silindi.');
    });
  }

  startCopy(item: StudentResource) {
    this.clear();
    this.copying.set(this.copying() === item.id ? null : item.id);
    this.copyIds.set([]);
    if (this.classmates() !== null) return;
    this.api.teacherStudents().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: students => this.classmates.set(students.filter(x => x.userId !== this.studentId())),
      error: () => { this.classmates.set([]); this.error.set('Öğrencileriniz şu an yüklenemedi.'); }
    });
  }
  toggleCopy(id: string, checked: boolean) {
    this.copyIds.set(checked ? [...new Set([...this.copyIds(), id])] : this.copyIds().filter(x => x !== id));
  }
  copy(item: StudentResource) {
    const studentId = this.studentId(); if (!studentId || !this.copyIds().length) return;
    this.run(this.api.copy(studentId, item, this.copyIds()), result => {
      this.copying.set(null);
      this.notice.set(result.skipped
        ? `${result.copied} öğrenciye eklendi. ${result.skipped} öğrencide aynı adlı kaynak zaten vardı.`
        : `${result.copied} öğrenciye eklendi.`);
    });
  }

  private clear() { this.error.set(null); this.notice.set(null); }

  private run<T>(request: Observable<T>, done: (value: T) => void) {
    this.busy.set(true); this.clear();
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => { this.busy.set(false); done(value); },
      error: (failure: { error?: { message?: string }; status?: number }) => {
        this.busy.set(false);
        this.error.set(failure?.error?.message || (failure?.status === 409
          ? this.v('Kaynak bu arada değişti. Sayfayı yenileyip yeniden dene.', 'Kaynak bu arada değişti. Sayfayı yenileyip yeniden deneyin.')
          : this.v('İşlem tamamlanamadı. Bilgileri kontrol edip yeniden dene.', 'İşlem tamamlanamadı. Bilgileri kontrol edip yeniden deneyin.')));
      }
    });
  }
}
