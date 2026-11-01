import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, catchError, concatMap, finalize, from, map, of, switchMap, throwError, toArray } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ToasterService } from '../../../core/services/toaster.service';
import { BULK_LIMIT, BulkKind, BulkResult, CoachingCurriculumManagerService, MappingDecision, PATCH_LIMIT, QualityIssue, QualityReport, Skipped, TopicPatch, chunk } from './coaching-curriculum-manager.service';
import { CONFIDENCE_LABELS, MAPPING_STATUS_LABELS, METHOD_LABELS, QUALITY_ISSUE_LABELS, SOURCE_KIND_LABELS, YIELD_LABELS, examLabel, label } from './coaching-curriculum-labels';
import { CoachingExamSessionsEditorComponent, CoachingOutcomeSourceEditorComponent, CoachingYksScopeEditorComponent, groupExamsForSelect } from './coaching-km7a-embeds';
import { CoachingCatalogImportComponent } from './coaching-catalog-import';
import { CoachingCurriculumComponent } from './coaching-curriculum.component';
import { CoachingOutcomesComponent } from './coaching-outcomes.component';

type Kind = 'exams' | 'programs' | 'programTopics' | 'lessons' | 'units' | 'topics' | 'outcomes';
interface Estimate { id: string; number: number; learningMinutes: number; practiceMinutes: number; checkMinutes: number; confidence: string; evidence: string; isPublished: boolean; method?: string }
interface Mapping { topicId: string; path: string; isApproved: boolean; evidence: string; status?: string }
/** KM1 (01.11.2026): a dated session of an exam (YDS 2027/1, e-YDS …) with its optional application window. */
interface ExamSession { id: string; date: string; label: string; applicationStart: string | null; applicationEnd: string | null }
interface SessionDraft { id: string | null; date: string; label: string; applicationStart: string; applicationEnd: string }
interface Node { kind: Kind; id: string; name: string; parentId: string | null; parentKind: Kind | null; isActive: boolean; path: string; fingerprint: string; canDelete: boolean; blockers: string[];
  examDate?: string | null; sessions?: ExamSession[]; code?: string; gradeNumber?: number | null; examCode?: string | null; displayOrder?: number | null; estimatedMinutes?: number | null; yieldWeight?: number | null; officialTopicId?: string; sourcePage?: number | null; sourceKind?: string; notes?: string; estimates: Estimate[]; mappings: Mapping[]; usedEstimateIds?: string[]; topicOutcomeCount?: number;
  academicYearStart?: number; academicYearEnd?: number; sourceUrl?: string; documentSha256?: string; scopeEvidenceUrl?: string; outcomeCount?: number; lessonPeriods?: number | null; curriculumVersionId?: string | null;
  /** KM7: list rows carry the record version for inline bulk edits and the link counts. */
  version?: string | number; pendingLinkCount?: number }
interface Page { items: Node[]; totalCount: number; pageNumber: number; pageSize: number }
interface Listing { rows: Node[]; page: number; total: number; loading: boolean; error?: string }
interface Form { sessions: SessionDraft[]; name: string; reason: string; parentId: string | null; gradeNumber: number | null; examCode: string | null; displayOrder: number | null; estimatedMinutes: number | null; yieldWeight: number; code: string; officialTopicId: string | null; sourcePage: number | null; notes: string; moveParentOutcomes: boolean;
  academicYearStart: number; academicYearEnd: number; sourceUrl: string; documentSha256: string; scopeEvidenceUrl: string; outcomeCount: number; lessonPeriods: number | null; curriculumVersionId: string | null }
interface Edition { id: string; code: string; subjectName: string; academicYearStart: number; academicYearEnd: number }
interface ExamRow { id: string; code: string; name: string; isActive: boolean }
interface InlineEdit { estimatedMinutes: number | null; yieldWeight: number }
interface Share { topicId: string; topicPath: string; learningMinutes: number; practiceMinutes: number; checkMinutes: number; scopeDescription: string }
interface Filters { examCode: string; lessonId: string; active: '' | 'true' | 'false'; issue: '' | QualityIssue }

const LIST_TABS = ['exams', 'lessons', 'units', 'topics', 'subtopics', 'outcomes', 'programs', 'programTopics'];
const KIND_WORDS: Record<string, string> = { exams: 'sınav türü', lessons: 'ders', units: 'ünite', topics: 'konu', outcomes: 'kazanım' };

@Component({ selector: 'app-coaching-curriculum-manager', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CoachingExamSessionsEditorComponent, CoachingYksScopeEditorComponent, CoachingOutcomeSourceEditorComponent,
    CoachingCatalogImportComponent, CoachingCurriculumComponent, CoachingOutcomesComponent],
  templateUrl: './coaching-curriculum-manager.component.html', styleUrl: './coaching-curriculum-manager.component.scss' })
export class CoachingCurriculumManagerComponent implements OnInit {
  private readonly http = inject(HttpClient); private readonly destroy = inject(DestroyRef);
  private readonly api = inject(CoachingCurriculumManagerService); private readonly toaster = inject(ToasterService); private readonly cdr = inject(ChangeDetectorRef);
  private readonly base = `${environment.apiUrl}/coaching-admin/curriculum-management`;
  private readonly outcomeBase = `${environment.apiUrl}/coaching-admin/outcomes`;
  private detailRequest?: Subscription; private destinationRequest?: Subscription; private listRequest?: Subscription; private lessonRequest?: Subscription; private qualityRequest?: Subscription;
  private initial = ''; private initialDuration = ''; private editionRequest?: Subscription;
  readonly listing = signal<Listing>({rows:[],page:1,total:0,loading:false}); readonly selected = signal<Node | null>(null);
  readonly busy = signal(false); readonly detailLoading = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly editing = signal(false); readonly creating = signal(false); readonly pending = signal<Node | null>(null);
  readonly destinations = signal<Node[]>([]); readonly destinationTotal = signal(0); readonly destinationLoading = signal(false);
  readonly editions = signal<Edition[]>([]); readonly officialTopics = signal<{ id: string; name: string; lessonPeriods: number }[]>([]);
  readonly labels: Record<Kind,string> = { exams:'Sınav türü',programs:'Kaynak program',programTopics:'Program konu grubu',lessons: 'Ders', units: 'Ünite', topics: 'Konu', outcomes: 'Kazanım' };
  readonly tabs = [{id:'exams',label:'Sınav türleri'},{id:'lessons',label:'Dersler'},{id:'units',label:'Üniteler'},{id:'topics',label:'Konular'},{id:'subtopics',label:'Alt konular'},{id:'outcomes',label:'Kazanımlar'},
    {id:'quality',label:'Kalite'},{id:'programs',label:'Kaynak programlar'},{id:'programTopics',label:'Program konu grupları'},{id:'imports',label:'Aktarım ve sürümler'}];
  readonly hierarchyLevels = [{id:'exams',label:'Sınav türü'},{id:'lessons',label:'Ders'},{id:'units',label:'Ünite'},{id:'topics',label:'Konu'},{id:'subtopics',label:'Alt konu'},{id:'outcomes',label:'Kazanım'}];
  readonly exams = signal<ExamRow[]>([]);
  readonly examGroups = computed(() => groupExamsForSelect(this.exams()));
  readonly lessonOptions = signal<Node[]>([]);
  readonly parentOptions = signal<Node[]>([]); readonly parentLoading = signal(false); readonly parentTotal = signal(0);
  readonly curricula = signal<{id:string;name:string;versionLabel:string;isPublished:boolean}[]>([]);
  readonly pendingLinks = signal<number | null>(null);
  // KM7 bulk selection, inline edits and their results.
  readonly selectedIds = signal<Set<string>>(new Set()); readonly inlineEdits = signal<Record<string, InlineEdit>>({});
  readonly inlineCount = computed(() => Object.keys(this.inlineEdits()).length);
  readonly bulkSkipped = signal<Skipped[]>([]); readonly bulkMessage = signal('');
  readonly quality = signal<QualityReport | null>(null); readonly qualityLoading = signal(false); readonly qualityPage = signal(1);
  readonly qualityIssues = Object.keys(QUALITY_ISSUE_LABELS) as QualityIssue[];
  readonly issueLabels = QUALITY_ISSUE_LABELS; readonly yieldLabels = YIELD_LABELS;
  filters: Filters = { examCode: '', lessonId: '', active: '', issue: '' };
  includeDescendants = true; bulkReason = ''; inlineReason = '';
  shares: Share[] = []; readonly shareTopics = signal<{ topicId: string; topicPath: string }[]>([]);
  tab = 'exams'; pendingTab = ''; filterId = ''; parentSearch = ''; parentPage = 1; activateAfterCreate = true;
  private filterNode: Node | null = null;
  private parentRequest?: Subscription;
  form: Form = this.empty(); kind: Kind = 'exams'; rootSearch = ''; rootScope: Kind = 'exams';
  destinationKind: Kind = 'exams'; destinationSearch = ''; destinationPage = 1; destinationId = ''; sourceTopicId = '';
  moveConfirmed = false; deleteConfirmation = ''; statusConfirmed = false;
  estimateConfirmation = ''; readonly sessionsValid = signal(true);
  editionId = ''; learningMinutes = 0; practiceMinutes = 0; checkMinutes = 0; confidence = 'Low'; durationEvidence = ''; publishDuration = false;

  ngOnInit() {
    this.loadRoot(); this.loadExams(); this.refreshPending();
    this.destroy.onDestroy(() => { for (const s of [this.detailRequest,this.destinationRequest,this.editionRequest,this.parentRequest,this.listRequest,this.lessonRequest,this.qualityRequest]) s?.unsubscribe(); });
  }
  // ---- labels ----
  examName(code: string | null | undefined) { return examLabel(code, this.exams()); }
  confidenceLabel(value: string) { return label(CONFIDENCE_LABELS, value); }
  methodLabel(value: string | undefined) { return label(METHOD_LABELS, value, 'Süre sürümü'); }
  sourceKindLabel(value: string | undefined) { return label(SOURCE_KIND_LABELS, value, 'Kazanım'); }
  mappingStatus(m: Mapping) { return m.status ?? (m.isApproved ? 'approved' : 'pending'); }
  mappingLabel(m: Mapping) { return MAPPING_STATUS_LABELS[this.mappingStatus(m)] ?? 'İnceleme bekliyor'; }
  issueLabel(issue: string) { return QUALITY_ISSUE_LABELS[issue] ?? 'Eksik bilgi'; }
  yieldLabel(weight: number | null | undefined) { return YIELD_LABELS[weight ?? 3] ?? 'Orta'; }
  nodeType(n: Node) {
    if (n.kind === 'lessons') return ['Ders',n.gradeNumber ? `${n.gradeNumber}. sınıf` : null,n.examCode ? this.examName(n.examCode) : null].filter(Boolean).join(' · ');
    return n.parentKind === 'topics' && n.kind === 'topics' ? 'Alt konu' : this.labels[n.kind];
  }
  branch() { return this.listing(); }
  isListTab() { return LIST_TABS.includes(this.tab); }
  isTopicTab() { return this.tab === 'topics' || this.tab === 'subtopics'; }
  hasFilterBar() { return ['lessons','units','topics','subtopics','outcomes'].includes(this.tab); }
  canBulk() { return ['exams','lessons','units','topics','subtopics','outcomes'].includes(this.tab); }
  empty(): Form { return { sessions:[],name:'',reason:'',parentId:null,gradeNumber:null,examCode:null,displayOrder:null,estimatedMinutes:null,yieldWeight:3,code:'',officialTopicId:null,sourcePage:null,notes:'',moveParentOutcomes:false,
    academicYearStart:new Date().getFullYear(),academicYearEnd:new Date().getFullYear(),sourceUrl:'',documentSha256:'',scopeEvidenceUrl:'',outcomeCount:1,lessonPeriods:null,curriculumVersionId:null }; }
  dirty() { return this.editing() && (JSON.stringify(this.form) !== this.initial || this.durationDirty()); }
  private durationFields() { return {learningMinutes:this.learningMinutes,practiceMinutes:this.practiceMinutes,checkMinutes:this.checkMinutes,confidence:this.confidence,durationEvidence:this.durationEvidence,publishDuration:this.publishDuration}; }
  durationDirty() { return this.kind === 'outcomes' && !this.creating() && JSON.stringify(this.durationFields()) !== this.initialDuration; }
  // ---- list ----
  loadRoot(page = 1) {
    if (!this.isListTab()) return;
    this.listRequest?.unsubscribe();
    if (page === 1) { this.selectedIds.set(new Set()); this.inlineEdits.set({}); }
    this.listing.update(previous => ({rows:page > 1 ? previous.rows : [],page,total:previous.total,loading:true}));
    let params=this.params(this.rootScope,this.filterId || null,page,this.rootSearch);
    const examId = this.exams().find(x => x.code === this.filters.examCode)?.id;
    if (this.tab === 'lessons' && !this.filterId && examId) params=params.set('parentId',examId);
    else if (!this.filterId) params=params.set('all','true');
    if (this.tab === 'topics' || this.tab === 'subtopics') params=params.set('level',this.tab === 'topics' ? 'root' : 'sub');
    if (this.tab === 'exams') params=params.set('pageSize',200);
    params=this.filterParams(params);
    this.listRequest=this.http.get<{data:Page}>(`${this.base}/children`,{params}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => {
      this.listing.update(previous => ({rows:[...(page > 1 ? previous.rows : []),...r.data.items],page,total:r.data.totalCount,loading:false}));
    },error:() => this.listing.update(previous => ({...previous,loading:false,error:'Liste alınamadı. Yeniden deneyin.'}))});
  }
  /** KM7: exam, lesson and state filters for the lists that support them (topics, units, outcomes, lessons). */
  private filterParams(params: HttpParams) {
    if (!this.hasFilterBar()) return params;
    if (this.filters.examCode && this.tab !== 'lessons') params=params.set('examCode',this.filters.examCode);
    if (this.filters.lessonId && this.tab !== 'lessons') params=params.set('lessonId',this.filters.lessonId);
    if (this.filters.active) params=params.set('active',this.filters.active);
    return params;
  }
  pageSize() { return this.tab === 'exams' ? 200 : 25; }
  examFilterChanged() {
    if (this.busy()) return;
    if (this.tab === 'quality') this.filters.lessonId='';
    this.filters.lessonId=''; this.lessonOptions.set([]); this.lessonRequest?.unsubscribe();
    if (this.filters.examCode) {
      const params=new HttpParams().set('kind','lessons').set('all','true').set('page',1).set('pageSize',200).set('examCode',this.filters.examCode);
      this.lessonRequest=this.http.get<{data:Page}>(`${this.base}/children`,{params}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => this.lessonOptions.set(r.data.items.filter(x => !x.examCode || x.examCode === this.filters.examCode)),error:e => this.fail(e)});
    }
    this.applyFilters();
  }
  applyFilters() {
    if (this.busy()) return;
    if (this.tab === 'quality') { this.loadQuality(); return; }
    this.filterId=''; this.filterNode=null; this.loadRoot(); if (this.parentKind()) this.loadParents();
  }
  /** "Eksikleri göster" opens the quality view with the chosen issue and the current exam/lesson filters. */
  issueFilterChanged() { if (this.tab === 'quality') { this.loadQuality(); return; } if (this.filters.issue) this.chooseTab('quality'); }
  chooseTab(tab: string) {
    if (this.busy() || !this.tabs.some(x => x.id === tab)) return;
    if (this.dirty()) { this.pending.set(null); this.pendingTab=tab; return; }
    this.tab=tab; this.cdr.markForCheck();
    if (tab !== 'quality' && tab !== 'imports') this.rootScope=tab === 'subtopics' ? 'topics' : tab as Kind;
    this.filterId=''; this.filterNode=null; this.rootSearch=''; this.parentSearch=''; this.parentOptions.set([]); this.parentRequest?.unsubscribe(); this.parentLoading.set(false); this.cancel();
    this.bulkSkipped.set([]); this.bulkMessage.set('');
    if (tab === 'quality') { this.loadQuality(); return; }
    if (tab === 'imports') return;
    if (this.filters.issue) this.filters.issue='';
    this.loadRoot(); if (this.parentKind()) this.loadParents();
    if (tab === 'lessons' || tab === 'programs') this.loadExams();
  }
  discardTab() { const tab=this.pendingTab; this.pendingTab=''; this.cancel(); this.chooseTab(tab); }
  navigateTab(event: KeyboardEvent,id: string) {
    const index=this.tabs.findIndex(x => x.id === id);
    const next=event.key === 'ArrowRight' ? (index+1)%this.tabs.length : event.key === 'ArrowLeft' ? (index+this.tabs.length-1)%this.tabs.length : event.key === 'Home' ? 0 : event.key === 'End' ? this.tabs.length-1 : -1;
    if (next < 0) return;
    event.preventDefault(); this.chooseTab(this.tabs[next].id);
    (event.currentTarget as HTMLElement).parentElement?.querySelector<HTMLButtonElement>(`#tab-${this.tabs[next].id}`)?.focus();
  }
  parentKind(): Kind | null { return ({lessons:'exams',units:'lessons',topics:'units',subtopics:'topics',outcomes:'topics',programTopics:'programs'} as Record<string,Kind>)[this.tab] ?? null; }
  tabLabel() { return this.tabs.find(x => x.id === this.tab)?.label ?? ''; }
  hierarchyLevelIndex() { return this.hierarchyLevels.findIndex(x => x.id === this.tab); }
  hierarchyHelp() {
    const index = this.hierarchyLevelIndex();
    if (this.tab === 'quality') return 'Süresi, kazanımı veya YKS kapsamı eksik konuları ders bazında görün; satırdan kaydı açın.';
    if (this.tab === 'imports') return 'Toplu katalog aktarımı, ders programı sürümleri ve kazanım paketleri.';
    if (index < 0) return 'Kaynak programlar ve program konu grupları resmi kaynak eşleştirmelerini yönetir.';
    return index === 0 ? 'Önce sınav türünü seçin; dersler doğrudan sınava bağlanır.'
      : `${this.hierarchyLevels[index].label} kayıtları ${this.hierarchyLevels[index - 1].label.toLocaleLowerCase('tr')} altında düzenlenir. Seçili kaydın tam yolu düzenleme panelinde gösterilir.`;
  }
  createLabel() { return this.tab === 'subtopics' ? 'Alt konu' : this.labels[this.rootScope]; }
  parentChoices() {
    const rows=[...this.parentOptions()];
    for (const n of [this.filterNode,this.creating() ? this.selected() : null]) if (n && !rows.some(x => x.id === n.id)) rows.unshift(n);
    return rows;
  }
  loadParents(page = 1) {
    const kind=this.parentKind(); if (!kind) return;
    this.parentRequest?.unsubscribe(); this.parentPage=page; this.parentLoading.set(true);
    let params=this.params(kind,null,page,this.parentSearch).set('all','true');
    if (this.tab === 'subtopics') params=params.set('level','root');
    if (this.tab === 'outcomes') params=params.set('level','leaf');
    // KM7: the exam and lesson filters narrow the parent options too (AYT Matematik's units instead of 25 of 825 units).
    if (kind !== 'exams' && this.filters.examCode) params=params.set('examCode',this.filters.examCode);
    if ((kind === 'units' || kind === 'topics') && this.filters.lessonId) params=params.set('lessonId',this.filters.lessonId);
    this.parentRequest=this.http.get<{data:Page}>(`${this.base}/children`,{params}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => {
      this.parentOptions.set(r.data.items); this.parentTotal.set(r.data.totalCount); this.parentLoading.set(false);
    },error:e => { this.parentLoading.set(false); this.fail(e); }});
  }
  changeFilter(id: string) {
    if (this.busy()) return;
    if (this.dirty()) { this.error.set('Önce açık formu kaydedin veya kapatın.'); return; }
    this.filterId=id; this.filterNode=this.parentChoices().find(x => x.id === id) ?? null; this.cancel(); this.loadRoot();
  }
  createCurrent() { this.create(this.rootScope,this.parentChoices().find(x => x.id === this.filterId)); }
  selectFormParent(id: string | null) {
    this.form.parentId=id;
    const parent=this.parentChoices().find(x => x.id === id);
    if (this.kind === 'lessons') this.form.examCode=parent?.code ?? this.exams().find(x => x.id === id)?.code ?? null;
    this.selected.set(parent ?? null); this.loadCreationParent(parent);
  }
  private loadExams() {
    this.http.get<{data:ExamRow[]}>(`${environment.apiUrl}/coaching/catalog/exams`).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => this.exams.set(r.data),error:e => this.fail(e)});
  }
  private refreshPending() {
    this.api.pendingCount().pipe(takeUntilDestroyed(this.destroy)).subscribe({next:count => this.pendingLinks.set(count),error:() => this.pendingLinks.set(null)});
  }
  private loadCurricula() {
    this.http.get<{data:{id:string;name:string;versionLabel:string;isPublished:boolean}[]}>(`${environment.apiUrl}/coaching-admin/curricula`).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => this.curricula.set(r.data),error:e => this.fail(e)});
  }
  // ---- selection & bulk (KM7) ----
  isChecked(n: Node) { return this.selectedIds().has(n.id); }
  toggleRow(n: Node) { this.selectedIds.update(set => { const next=new Set(set); if (next.has(n.id)) next.delete(n.id); else next.add(n.id); return next; }); }
  selectPage() { const rows=this.listing().rows; const all=rows.every(x => this.selectedIds().has(x.id)); this.selectedIds.set(all ? new Set() : new Set(rows.map(x => x.id))); }
  allChecked() { const rows=this.listing().rows; return rows.length > 0 && rows.every(x => this.selectedIds().has(x.id)); }
  clearSelection() { this.selectedIds.set(new Set()); }
  private bulkKind(): BulkKind { return (this.tab === 'subtopics' ? 'topics' : this.tab) as BulkKind; }
  async bulkSetActive(active: boolean) {
    const ids=[...this.selectedIds()]; const reason=this.bulkReason.trim();
    if (!ids.length || this.busy() || reason.length < 5 || reason.length > 500) return;
    const kind=this.bulkKind(); const word=KIND_WORDS[kind] ?? 'kayıt';
    const question=`${ids.length} ${word}${this.includeDescendants ? ' ve alt kayıtları' : ''} ${active ? 'etkinleştirilsin' : 'pasife alınsın'} mi? Her kayıt için işlem geçmişine ayrı satır yazılır.`;
    if (!await this.toaster.confirm(question,{title:active ? 'Toplu etkinleştirme' : 'Toplu pasife alma',confirmText:active ? 'Etkinleştir' : 'Pasife al',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    this.busy.set(true); this.error.set(''); this.message.set(''); this.bulkSkipped.set([]); this.bulkMessage.set('');
    from(chunk(ids,BULK_LIMIT)).pipe(concatMap(part => this.api.bulkStatus({kind,ids:part,active,includeDescendants:this.includeDescendants,reason})),toArray(),
      takeUntilDestroyed(this.destroy),finalize(() => this.busy.set(false))).subscribe({next:results => {
        const total=this.merge(results); this.bulkMessage.set(`${total.changed} kayıt güncellendi${total.skipped.length ? `, ${total.skipped.length} kayıt atlandı` : ''}.`);
        this.bulkSkipped.set(total.skipped); this.bulkReason=''; this.loadRoot();
      },error:e => this.fail(e)});
  }
  /** KM7: approves every link of the selected topics (e.g. a page of AYT Tarih topics) with the bulk reason. */
  async approveSelectedTopicLinks() {
    const ids=[...this.selectedIds()]; const reason=this.bulkReason.trim();
    if (!ids.length || this.busy() || !this.isTopicTab() || reason.length < 5 || reason.length > 500) return;
    if (!await this.toaster.confirm(`${ids.length} konunun bekleyen kazanım bağlantıları onaylansın mı?`,{title:'Bağlantıları onayla',confirmText:'Onayla',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    this.busy.set(true); this.error.set(''); this.bulkSkipped.set([]); this.bulkMessage.set('');
    const outcomesOf=(topicId: string) => this.http.get<{data:Page}>(`${this.base}/children`,{params:new HttpParams().set('kind','outcomes').set('parentId',topicId).set('page',1).set('pageSize',200)})
      .pipe(map(r => r.data.items.map(o => ({topicId,outcomeId:o.id}))));
    from(ids).pipe(concatMap(outcomesOf),toArray(),map(rows => rows.flat()),switchMap(keys => keys.length ? from(chunk(keys,BULK_LIMIT)).pipe(concatMap(part => this.api.reviewMappings(part,'approve',reason)),toArray()) : of([] as BulkResult[])),
      takeUntilDestroyed(this.destroy),finalize(() => this.busy.set(false))).subscribe({next:results => {
        const total=this.merge(results); this.bulkMessage.set(`${total.changed} bağlantı onaylandı${total.skipped.length ? `, ${total.skipped.length} bağlantı atlandı` : ''}.`);
        this.bulkSkipped.set(total.skipped); this.bulkReason=''; this.loadRoot(); this.refreshPending();
      },error:e => this.fail(e)});
  }
  skippedName(s: Skipped) { return this.listing().rows.find(x => x.id === s.id)?.name ?? 'Seçilen kayıt'; }
  private merge(results: BulkResult[]): BulkResult { return {changed:results.reduce((s,x) => s+x.changed,0),skipped:results.flatMap(x => x.skipped ?? [])}; }
  // ---- inline minutes / weight (KM7) ----
  inlineValue(n: Node): InlineEdit { return this.inlineEdits()[n.id] ?? {estimatedMinutes:n.estimatedMinutes ?? null,yieldWeight:n.yieldWeight ?? 3}; }
  editMinutes(n: Node,value: number | string | null) { const v=value === '' || value === null ? null : Number(value); this.setInline(n,{...this.inlineValue(n),estimatedMinutes:v}); }
  editWeight(n: Node,value: number | string) { this.setInline(n,{...this.inlineValue(n),yieldWeight:Number(value)}); }
  private setInline(n: Node,edit: InlineEdit) {
    this.inlineEdits.update(all => { const next={...all};
      if (edit.estimatedMinutes === (n.estimatedMinutes ?? null) && edit.yieldWeight === (n.yieldWeight ?? 3)) delete next[n.id]; else next[n.id]=edit; return next; });
  }
  discardInline() { this.inlineEdits.set({}); }
  inlineInvalid() { return Object.values(this.inlineEdits()).some(x => (x.estimatedMinutes !== null && (!Number.isInteger(x.estimatedMinutes) || x.estimatedMinutes < 1 || x.estimatedMinutes > 100000)) || !Number.isInteger(x.yieldWeight) || x.yieldWeight < 1 || x.yieldWeight > 5); }
  async saveInline() {
    const edits=this.inlineEdits(); const reason=this.inlineReason.trim(); const rows=this.listing().rows;
    if (!this.inlineCount() || this.busy() || reason.length < 5 || reason.length > 500) return;
    if (this.inlineInvalid()) { this.error.set('Süre 1 veya daha büyük tam sayı, sıklık 1–5 olmalıdır.'); return; }
    const items: TopicPatch[]=rows.filter(x => edits[x.id]).map(x => ({id:x.id,version:x.version ?? x.fingerprint,estimatedMinutes:edits[x.id].estimatedMinutes,yieldWeight:edits[x.id].yieldWeight}));
    if (!await this.toaster.confirm(`${items.length} konunun süre ve sıklık değeri kaydedilsin mi?`,{title:'Toplu düzenleme',confirmText:'Kaydet',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    from(chunk(items,PATCH_LIMIT)).pipe(concatMap(part => this.api.patchTopics(part,reason)),toArray(),takeUntilDestroyed(this.destroy),finalize(() => this.busy.set(false))).subscribe({next:results => {
      const changed=results.reduce((s,x) => s+x.changed,0); const conflicts=results.flatMap(x => x.conflicts ?? []);
      this.message.set(`${changed} konu güncellendi.`);
      if (conflicts.length) this.error.set(`${conflicts.length} konu başka bir işlemde değişmiş; güncel değerleri kontrol edip yeniden girin.`);
      this.inlineReason=''; this.loadRoot();
    },error:e => this.fail(e)});
  }
  // ---- detail ----
  select(n: Node) { if (this.busy()) return; if (this.dirty()) { this.pendingTab=''; this.pending.set(n); return; } this.open(n); }
  discard() { const n = this.pending(); this.pending.set(null); if (n) this.open(n); }
  private open(n: Node) {
    this.detailRequest?.unsubscribe(); this.error.set(''); this.message.set(''); this.editing.set(false); this.detailLoading.set(true);
    this.detailRequest = this.http.get<{data:Node}>(`${this.base}/${n.kind}/${n.id}`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next:r => { this.apply(r.data); this.detailLoading.set(false); },error:e => { this.detailLoading.set(false); this.fail(e); }
    });
  }
  private apply(n: Node,preserveDuration = false) {
    const unsaved = preserveDuration && this.durationDirty() ? this.durationFields() : null;
    const sameOutcome = this.selected()?.id === n.id && n.kind === 'outcomes';
    this.selected.set(n); this.kind = n.kind; this.creating.set(false); this.editing.set(true);
    this.form = { ...this.empty(),name:n.name,parentId:n.parentId,gradeNumber:n.gradeNumber ?? null,examCode:n.examCode ?? null,displayOrder:n.displayOrder ?? null,estimatedMinutes:n.estimatedMinutes ?? null,yieldWeight:n.yieldWeight ?? 3,
      sessions:(n.sessions ?? []).map(s => ({id:s.id,date:s.date,label:s.label,applicationStart:s.applicationStart ?? '',applicationEnd:s.applicationEnd ?? ''})),code:n.code ?? '',officialTopicId:n.officialTopicId ?? null,sourcePage:n.sourceKind === 'ExamSkill' ? null : n.sourcePage ?? 1,notes:n.notes ?? '',
      academicYearStart:n.academicYearStart ?? new Date().getFullYear(),academicYearEnd:n.academicYearEnd ?? new Date().getFullYear(),sourceUrl:n.sourceUrl ?? '',documentSha256:n.documentSha256 ?? '',scopeEvidenceUrl:n.scopeEvidenceUrl ?? '',outcomeCount:n.outcomeCount ?? 1,lessonPeriods:n.lessonPeriods ?? null,curriculumVersionId:n.curriculumVersionId ?? null };
    this.initial = JSON.stringify(this.form); this.moveConfirmed = false; this.statusConfirmed = false; this.deleteConfirmation = ''; this.estimateConfirmation = ''; this.destinationId = ''; this.destinations.set([]); this.sessionsValid.set(true);
    this.sourceTopicId = n.mappings?.[0]?.topicId ?? ''; this.destinationKind = n.kind === 'lessons' ? 'exams' : n.kind === 'units' ? 'lessons' : n.kind === 'outcomes' ? 'topics' : 'units';
    const estimate = n.estimates?.find(x => x.isPublished) ?? n.estimates?.[0];
    this.learningMinutes = estimate?.learningMinutes ?? 0; this.practiceMinutes = estimate?.practiceMinutes ?? 0; this.checkMinutes = estimate?.checkMinutes ?? 0;
    this.confidence = estimate?.confidence ?? 'Low'; this.durationEvidence = ''; this.publishDuration = false;
    this.initialDuration = JSON.stringify(this.durationFields()); if (unsaved) Object.assign(this,unsaved);
    if (n.kind === 'outcomes') this.loadShareTopics(n,sameOutcome); else { this.shares=[]; this.shareTopics.set([]); }
    if (n.kind === 'lessons') { this.loadExams(); this.loadCurricula(); }
    if (n.kind === 'programs') this.loadExams();
  }
  create(kind: Kind,parent?: Node) {
    if (this.busy() || this.dirty()) { this.error.set('Önce açık formdaki değişiklikleri kaydedin veya kaydı yeniden açın.'); return; }
    this.detailRequest?.unsubscribe(); this.detailLoading.set(false); this.kind = kind; this.selected.set(parent ?? null); this.creating.set(true); this.editing.set(true); this.sessionsValid.set(true);
    this.form = {...this.empty(),parentId:parent?.id ?? null,examCode:kind === 'lessons' && parent?.kind === 'exams' ? parent.code ?? null : null}; this.initial = JSON.stringify(this.form); this.error.set(''); this.message.set(''); this.activateAfterCreate=true;
    if (kind === 'lessons') { this.loadExams(); this.loadCurricula(); }
    if (kind === 'programs') this.loadExams();
    this.loadCreationParent(parent);
    if (kind === 'outcomes') this.loadEditions();
  }
  private loadCreationParent(parent?: Node) {
    this.detailRequest?.unsubscribe(); this.detailLoading.set(false);
    if (this.kind !== 'topics' || parent?.kind !== 'topics') return;
    this.detailLoading.set(true);
    this.detailRequest=this.http.get<{data:Node}>(`${this.base}/topics/${parent.id}`).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => { this.selected.set(r.data); this.detailLoading.set(false); },error:e => { this.detailLoading.set(false); this.fail(e); }});
  }
  cancel() { this.detailRequest?.unsubscribe(); this.detailLoading.set(false); this.editing.set(false); this.creating.set(false); this.error.set(''); this.pending.set(null); this.pendingTab=''; }
  save() {
    if (this.busy() || !this.editing() || !this.form.name.trim() || this.form.reason.trim().length < 5) return;
    if (this.kind === 'exams' && !this.sessionsValid()) { this.error.set('Oturum tarihlerini kontrol edin.'); return; }
    const n = this.selected();
    const sessions = this.kind === 'exams' ? this.form.sessions.map(s => ({id:s.id,date:s.date,label:s.label.trim(),applicationStart:s.applicationStart || null,applicationEnd:s.applicationEnd || null})) : undefined;
    const r = {...this.form,sessions,name:this.form.name.trim(),reason:this.form.reason.trim(),...(!this.creating() && n ? {fingerprint:n.fingerprint} : {})};
    if (this.creating() && this.parentKind() && !r.parentId) { this.error.set('Önce üst kaydı seçin.'); return; }
    let request=this.creating() ? this.http.post<{data:Node}>(`${this.base}/${this.kind}`,r) : this.http.put<{data:Node}>(`${this.base}/${this.kind}/${n!.id}`,r);
    if (this.creating() && this.activateAfterCreate && ['lessons','units','topics'].includes(this.kind)) request=request.pipe(switchMap(created =>
      this.http.put<{data:Node}>(`${this.base}/${created.data.kind}/${created.data.id}/status`,{fingerprint:created.data.fingerprint,reason:r.reason,isActive:true}).pipe(catchError(() => {
        this.apply(created.data); return throwError(() => ({error:{message:'Kayıt oluşturuldu; etkinleştirilemedi. Üst kaydın aktifliğini kontrol edip bu kaydı etkinleştirin.'}}));
      }))));
    this.write(request,'Kayıt kaydedildi.',true);
  }
  searchDestinations(page = 1) {
    this.destinationRequest?.unsubscribe(); this.destinationPage = page; this.destinationId = ''; this.moveConfirmed = false; this.destinationLoading.set(true);
    let params = this.params(this.destinationKind,null,page,this.destinationSearch).set('all','true');
    if (this.destinationKind !== 'exams' && this.filters.examCode) params=params.set('examCode',this.filters.examCode);
    this.destinationRequest = this.http.get<{data:Page}>(`${this.base}/children`,{params}).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next:r => {
      this.destinations.set(r.data.items.filter(x => x.id !== this.selected()?.id)); this.destinationTotal.set(r.data.totalCount); this.destinationLoading.set(false);
    },error:e => { this.destinationLoading.set(false); this.fail(e); } });
  }
  /** KM7: an unused record moves in place (same id); a used one is copied and the old one kept passive. */
  async move() {
    const n = this.selected(); if (this.creating() || this.busy() || !n || !this.destinationId || !this.moveConfirmed || this.form.reason.trim().length < 5 || this.dirtyFields() || this.durationDirty()) return;
    const target = this.destinations().find(x => x.id === this.destinationId);
    if (!await this.toaster.confirm(`“${n.name}” ${target ? `“${target.path || target.name}” altına` : 'seçilen konuma'} taşınsın mı?`,{title:'Taşıma',confirmText:'Taşı',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    this.write(this.http.post<{data:Node}>(`${this.base}/${n.kind}/${n.id}/move`,{destinationId:this.destinationId,fingerprint:n.fingerprint,reason:this.form.reason.trim(),sourceTopicId:this.sourceTopicId || null}),
      moved => moved.data.id === n.id ? 'Taşıma tamamlandı: aynı kayıt yeni konuma taşındı.' : 'Kayıt plan veya öğrenci kayıtlarında kullanımda olduğu için yeni konumda kopyası oluşturuldu; eski kayıt pasife alındı ve eski planlar korundu.');
  }
  async setActive() {
    const n = this.selected(); if (!n || this.busy() || this.dirtyFields() || this.durationDirty() || !this.statusConfirmed || this.form.reason.trim().length < 5) return;
    if (!await this.toaster.confirm(`“${n.name}” ${n.isActive ? 'pasife alınsın' : 'etkinleştirilsin'} mi?`,{title:'Kullanım durumu',confirmText:n.isActive ? 'Pasife al' : 'Etkinleştir',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    this.write(this.http.put<{data:Node}>(`${this.base}/${n.kind}/${n.id}/status`,{fingerprint:n.fingerprint,reason:this.form.reason.trim(),isActive:!n.isActive}),'Kullanım durumu güncellendi.');
  }
  dirtyFields() { const current = JSON.parse(this.initial || '{}') as Partial<Form>; return JSON.stringify({...this.form,reason:current.reason}) !== this.initial; }
  async remove() {
    const n = this.selected(); if (!n?.canDelete || this.creating() || this.busy() || this.deleteConfirmation !== 'SİL' || this.form.reason.trim().length < 5) return;
    if (!await this.toaster.confirm(`“${n.name}” kalıcı olarak silinsin mi? Bu işlem geri alınamaz.`,{title:'Kalıcı silme',confirmText:'Kalıcı sil',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    this.busy.set(true); this.error.set('');
    this.http.delete(`${this.base}/${n.kind}/${n.id}`,{body:{fingerprint:n.fingerprint,reason:this.form.reason.trim(),confirmId:n.id}}).pipe(takeUntilDestroyed(this.destroy),finalize(() => this.busy.set(false))).subscribe({
      next:() => { this.editing.set(false); this.selected.set(null); this.message.set('Kullanılmayan kayıt silindi.'); this.refreshList(); },error:e => this.fail(e)
    });
  }
  loadEditions() {
    this.http.get<{data:Edition[]}>(this.outcomeBase).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => this.editions.set(r.data),error:e => this.fail(e)});
  }
  selectEdition() {
    this.editionRequest?.unsubscribe(); this.officialTopics.set([]); this.form.officialTopicId = null;
    if (!this.editionId) return;
    this.editionRequest = this.http.get<{data:{officialTopics:{id:string;name:string;lessonPeriods:number}[]}}>(`${this.outcomeBase}/${this.editionId}`).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => this.officialTopics.set(r.data.officialTopics),error:e => this.fail(e)});
  }
  // ---- topic–outcome links (KM7) ----
  pendingMappings() { return (this.selected()?.mappings ?? []).filter(m => this.mappingStatus(m) === 'pending'); }
  private reviewKeys(items: { topicId: string; outcomeId: string }[],decision: MappingDecision,done: (r: BulkResult) => string) {
    const reason=this.form.reason.trim(); const n=this.selected(); if (!n || !items.length) return;
    this.write(from(chunk(items,BULK_LIMIT)).pipe(concatMap(part => this.api.reviewMappings(part,decision,reason)),toArray(),map(results => this.merge(results)),
      switchMap(total => { this.bulkSkipped.set(total.skipped); return this.http.get<{data:Node}>(`${this.base}/${n.kind}/${n.id}`).pipe(map(r => ({...r,total}))); })),
      res => done(res.total),true,true);
  }
  private reasonReady() {
    if (this.form.reason.trim().length < 5) { this.error.set('Önce 5–500 karakterlik işlem gerekçesini yazın.'); return false; }
    if (this.dirtyFields()) { this.error.set('Önce kazanım metni ve alan değişikliklerini kaydedin.'); return false; }
    return !this.busy();
  }
  async reviewAllPending() {
    const n=this.selected(); const pendingRows=this.pendingMappings(); if (!n || !pendingRows.length || !this.reasonReady()) return;
    if (!await this.toaster.confirm(`Bu kazanımın ${pendingRows.length} bekleyen konu bağlantısı onaylansın mı?`,{title:'Bağlantıları onayla',confirmText:'Onayla',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    this.reviewKeys(pendingRows.map(m => ({topicId:m.topicId,outcomeId:n.id})),'approve',r => `${r.changed} bağlantı onaylandı.`);
  }
  async reviewMapping(m: Mapping,decision: MappingDecision) {
    const n=this.selected(); if (!n || !this.reasonReady()) return;
    if (decision !== 'approve') {
      const text=decision === 'reject' ? `“${m.path}” bağlantısı reddedilsin mi? Kayıt geçmiş için saklanır, planlamada kullanılmaz.` : `“${m.path}” bağlantısının onayı kaldırılsın mı?`;
      if (!await this.toaster.confirm(text,{title:'Bağlantı incelemesi',confirmText:decision === 'reject' ? 'Reddet' : 'Onayı kaldır',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    }
    this.reviewKeys([{topicId:m.topicId,outcomeId:n.id}],decision,r => r.changed ? (decision === 'approve' ? 'Bağlantı onaylandı.' : decision === 'reject' ? 'Bağlantı reddedildi.' : 'Bağlantının onayı kaldırıldı.') : 'Bağlantı değişmedi.');
  }
  async removeMapping(m: Mapping) {
    const n=this.selected(); if (!n || !this.reasonReady()) return;
    if (!await this.toaster.confirm(`“${m.path}” ile bu kazanım arasındaki bağlantı kaldırılsın mı? Planlarda veya yayınlanmış süre paylarında kullanılan bağlantı kaldırılamaz.`,{title:'Bağlantıyı kaldır',confirmText:'Kaldır',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    this.write(this.api.deleteMapping({topicId:m.topicId,outcomeId:n.id},this.form.reason.trim()).pipe(switchMap(() => this.http.get<{data:Node}>(`${this.base}/${n.kind}/${n.id}`))),'Bağlantı kaldırıldı.',true,true);
  }
  /** KM7: approve every link of the open topic in one request ("Bağlantıları onayla" from a topic). */
  async approveTopicLinks() {
    const n=this.selected(); if (!n || n.kind !== 'topics' || !this.reasonReady()) return;
    if (!await this.toaster.confirm(`“${n.name}” konusunun bekleyen kazanım bağlantıları onaylansın mı?`,{title:'Bağlantıları onayla',confirmText:'Onayla',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    const reason=this.form.reason.trim();
    const params=new HttpParams().set('kind','outcomes').set('parentId',n.id).set('page',1).set('pageSize',200);
    this.write(this.http.get<{data:Page}>(`${this.base}/children`,{params}).pipe(map(r => r.data.items.map(o => ({topicId:n.id,outcomeId:o.id}))),
      switchMap(keys => keys.length ? from(chunk(keys,BULK_LIMIT)).pipe(concatMap(part => this.api.reviewMappings(part,'approve',reason)),toArray()) : of([] as BulkResult[])),
      map(results => this.merge(results)),
      switchMap(total => { this.bulkSkipped.set(total.skipped); return this.http.get<{data:Node}>(`${this.base}/topics/${n.id}`).pipe(map(r => ({...r,total}))); })),
      res => res.total.changed || res.total.skipped.length ? `${res.total.changed} bağlantı onaylandı.` : 'Bu konuya bağlı kazanım yok.',false,true);
  }
  // ---- duration estimate with topic shares (KM7) ----
  approvedTopicIds() { return (this.selected()?.mappings ?? []).filter(m => this.mappingStatus(m) === 'approved').map(m => m.topicId); }
  needsShares() { return this.kind === 'outcomes' && this.approvedTopicIds().length >= 2; }
  private loadShareTopics(n: Node,keep: boolean) {
    const approved=this.approvedTopicIds();
    if (approved.length < 2) { this.shares=[]; this.shareTopics.set([]); return; }
    const build=(topics: { topicId: string; topicPath: string }[]) => {
      const previous=keep ? this.shares : [];
      this.shareTopics.set(topics);
      this.shares=approved.map(id => previous.find(x => x.topicId === id) ?? {topicId:id,topicPath:topics.find(t => t.topicId === id)?.topicPath ?? n.mappings.find(m => m.topicId === id)?.path ?? 'Konu',
        learningMinutes:0,practiceMinutes:0,checkMinutes:0,scopeDescription:topics.find(t => t.topicId === id)?.topicPath ?? n.mappings.find(m => m.topicId === id)?.path ?? 'Konu kapsamı'});
      if (!keep) this.splitShares();
    };
    build(n.mappings.filter(m => approved.includes(m.topicId)).map(m => ({topicId:m.topicId,topicPath:m.path})));
    this.api.outcomeTopics(n.id).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:topics => { if (this.selected()?.id === n.id) build(topics.length ? topics : this.shareTopics()); },error:() => undefined});
  }
  /** Splits each duration component evenly; earlier topics get the remainder minute. */
  splitShares() {
    const count=this.shares.length; if (!count) return;
    const split=(total: number) => Array.from({length:count},(_,i) => Math.floor(total/count) + (i < total % count ? 1 : 0));
    const l=split(this.learningMinutes),p=split(this.practiceMinutes),c=split(this.checkMinutes);
    this.shares=this.shares.map((s,i) => ({...s,learningMinutes:l[i],practiceMinutes:p[i],checkMinutes:c[i]}));
  }
  shareSum(part: 'learningMinutes' | 'practiceMinutes' | 'checkMinutes') { return this.shares.reduce((s,x) => s + (Number(x[part]) || 0),0); }
  sharesValid() {
    if (!this.needsShares()) return true;
    return this.shareSum('learningMinutes') === this.learningMinutes && this.shareSum('practiceMinutes') === this.practiceMinutes && this.shareSum('checkMinutes') === this.checkMinutes
      && this.shares.every(s => [s.learningMinutes,s.practiceMinutes,s.checkMinutes].every(x => Number.isInteger(x) && x >= 0) && s.learningMinutes + s.practiceMinutes + s.checkMinutes >= 1 && s.scopeDescription.trim().length >= 2);
  }
  saveDuration() {
    const n = this.selected(); const parts = [this.learningMinutes,this.practiceMinutes,this.checkMinutes]; const total = parts.reduce((s,x) => s+x,0);
    if (!n || this.busy() || this.form.reason.trim().length < 5 || this.durationEvidence.trim().length < 5) return;
    if (this.dirtyFields()) { this.error.set('Önce kazanım metni ve alan değişikliklerini kaydedin. Süre girişleriniz korunacak.'); return; }
    if (parts.some(x => !Number.isInteger(x) || x < 0) || total < 1 || total > 1440) { this.error.set('Toplam süre 1–1440 dakika olmalıdır.'); return; }
    if (this.publishDuration && !n.mappings.some(x => this.mappingStatus(x) === 'approved')) { this.error.set('Önce en az bir konu eşleştirmesini onaylayın.'); return; }
    if (!this.sharesValid()) { this.error.set('Konu payları her süre bileşeninin toplamına eşit olmalı ve her konuya en az 1 dakika ayrılmalıdır.'); return; }
    const topicShares = this.needsShares() ? this.shares.map(s => ({topicId:s.topicId,learningMinutes:s.learningMinutes,practiceMinutes:s.practiceMinutes,checkMinutes:s.checkMinutes,scopeDescription:s.scopeDescription.trim()})) : undefined;
    let created = false;
    const request = this.http.post<{data:Estimate}>(`${this.outcomeBase}/${n.id}/estimates`,{learningMinutes:this.learningMinutes,practiceMinutes:this.practiceMinutes,checkMinutes:this.checkMinutes,
      confidence:this.confidence,evidence:this.durationEvidence.trim(),reason:this.form.reason.trim(),...(topicShares ? {topicShares} : {})}).pipe(switchMap(r => {
        created = true;
        return this.publishDuration
          ? this.http.put(`${this.outcomeBase}/estimates/${r.data.id}/publication`,{reason:this.form.reason.trim()}).pipe(switchMap(() => this.http.get<{data:Node}>(`${this.base}/outcomes/${n.id}`)))
          : this.http.get<{data:Node}>(`${this.base}/outcomes/${n.id}`);
      }),catchError(e => {
        if (created) return throwError(() => ({error:{message:'Süre sürümü oluşturuldu; sonraki işlem tamamlanamadı. Kaydı yeniden açıp süre geçmişini kontrol edin; yeniden sürüm oluşturmayın.'}}));
        return throwError(() => e);
      }));
    this.write(request,this.publishDuration ? 'Yeni süre sürümü yayınlandı.' : 'Yeni süre sürümü taslak olarak kaydedildi.');
  }
  publish(estimate: Estimate) {
    const n = this.selected(); if (!n || this.busy() || this.form.reason.trim().length < 5) return;
    if (this.dirtyFields()) { this.error.set('Önce kazanım metni ve alan değişikliklerini kaydedin.'); return; }
    this.write(this.http.put(`${this.outcomeBase}/estimates/${estimate.id}/publication`,{reason:this.form.reason.trim()}).pipe(switchMap(() => this.http.get<{data:Node}>(`${this.base}/outcomes/${n.id}`))),'Süre sürümü yayınlandı.',true);
  }
  async withdrawEstimate(e: Estimate) {
    const n = this.selected(); if (!n || this.busy() || this.estimateConfirmation !== e.id || this.form.reason.trim().length < 5) return;
    if (this.dirtyFields()) { this.error.set('Önce kazanım metni ve alan değişikliklerini kaydedin.'); return; }
    if (!await this.toaster.confirm(`Sürüm ${e.number} yayından kaldırılsın mı? Eski planların süreleri korunur.`,{title:'Yayından kaldır',confirmText:'Yayından kaldır',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    this.write(this.http.put<{data:Node}>(`${this.base}/outcomes/${n.id}/estimates/${e.id}/publication`,{fingerprint:n.fingerprint,reason:this.form.reason.trim(),isActive:false}),'Süre sürümü yayından kaldırıldı. Eski planların süreleri korundu.',true);
  }
  async deleteEstimate(e: Estimate) {
    const n = this.selected(); if (!n || this.busy() || n.usedEstimateIds?.includes(e.id) || this.estimateConfirmation !== e.id || this.deleteConfirmation !== 'SİL' || this.form.reason.trim().length < 5) return;
    if (this.dirtyFields()) { this.error.set('Önce kazanım metni ve alan değişikliklerini kaydedin.'); return; }
    if (!await this.toaster.confirm(`Sürüm ${e.number} kalıcı olarak silinsin mi? Bu işlem geri alınamaz.`,{title:'Kalıcı silme',confirmText:'Kalıcı sil',cancelText:'Vazgeç'}) || this.destroy.destroyed) return;
    this.write(this.http.delete<{data:Node}>(`${this.base}/outcomes/${n.id}/estimates/${e.id}`,{body:{fingerprint:n.fingerprint,reason:this.form.reason.trim(),confirmId:e.id}}),'Kullanılmamış süre sürümü silindi.',true);
  }
  total(estimate: Estimate) { return estimate.learningMinutes + estimate.practiceMinutes + estimate.checkMinutes; }
  /** Reloads the open record after an embedded editor (YKS scope, outcome source) saved. */
  embeddedSaved(text: string) { const n=this.selected(); this.message.set(text); if (n) { this.refreshList(); } }
  // ---- quality (KM7) ----
  loadQuality(page = 1) {
    this.qualityRequest?.unsubscribe(); this.qualityPage.set(page); this.qualityLoading.set(true);
    this.qualityRequest=this.api.quality({examCode:this.filters.examCode || undefined,lessonId:this.filters.lessonId || undefined,issue:this.filters.issue || undefined,page,pageSize:50})
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => { this.quality.set(r); this.qualityLoading.set(false); },error:e => { this.qualityLoading.set(false); this.fail(e); }});
  }
  filterQualityLesson(lessonId: string) { this.filters.lessonId=lessonId; this.loadQuality(); }
  filterQualityIssue(issue: QualityIssue | '') { this.filters.issue=issue; this.loadQuality(); }
  fmt(value: number | null | undefined) { return (value ?? 0).toLocaleString('tr-TR'); }
  qualityTotal() { const q=this.quality(); return q?.totalCount ?? q?.counts[this.filters.issue || ''] ?? q?.items.length ?? 0; }
  openQualityItem(item: { topicId: string; topicPath?: string; issue?: string }) { this.select({kind:'topics',id:item.topicId} as Node); }
  exportQuality() {
    this.api.qualityCsv({examCode:this.filters.examCode || undefined,lessonId:this.filters.lessonId || undefined,issue:this.filters.issue || undefined}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:blob => {
      const url=URL.createObjectURL(blob); const a=document.createElement('a'); a.href=url; a.download='mufredat-kalite.csv'; a.click(); setTimeout(() => URL.revokeObjectURL(url),1000);
    },error:e => this.fail(e)});
  }
  // ---- shared ----
  private write<T extends {data:Node}>(request: Observable<T>,message: string | ((r: T) => string),preserveDuration = false,linksChanged = false) {
    this.busy.set(true); this.error.set(''); this.message.set('');
    request.pipe(takeUntilDestroyed(this.destroy),finalize(() => this.busy.set(false))).subscribe({next:r => {
      this.apply(r.data,preserveDuration); this.message.set(typeof message === 'string' ? message : message(r)); this.refreshList(); if (linksChanged) this.refreshPending();
    },error:e => this.fail(e)});
  }
  private refreshList() {
    if (this.tab === 'quality') this.loadQuality(this.qualityPage()); else this.loadRoot();
    if (this.kind === 'exams') this.loadExams();
  }
  private params(kind: Kind,parent: string | null,page: number,search: string) { let p = new HttpParams().set('kind',kind).set('page',page); if (parent) p = p.set('parentId',parent); if (search.trim()) p = p.set('search',search.trim()); return p; }
  private fail(e: {error?:{message?:unknown}}) { this.error.set(typeof e?.error?.message === 'string' ? e.error.message : 'İşlem tamamlanamadı. Girdiğiniz bilgiler korundu. Yeniden deneyin.'); }
}
