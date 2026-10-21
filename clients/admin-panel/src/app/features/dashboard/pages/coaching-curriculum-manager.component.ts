import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, catchError, finalize, switchMap, throwError } from 'rxjs';
import { environment } from '../../../../environments/environment';

type Kind = 'exams' | 'programs' | 'programTopics' | 'lessons' | 'units' | 'topics' | 'outcomes';
interface Estimate { id: string; number: number; learningMinutes: number; practiceMinutes: number; checkMinutes: number; confidence: string; evidence: string; isPublished: boolean; method?: string }
interface Mapping { topicId: string; path: string; isApproved: boolean; evidence: string }
interface Node { kind: Kind; id: string; name: string; parentId: string | null; parentKind: Kind | null; isActive: boolean; path: string; fingerprint: string; canDelete: boolean; blockers: string[];
  examDate?: string | null; code?: string; gradeNumber?: number | null; examCode?: string | null; displayOrder?: number | null; estimatedMinutes?: number | null; officialTopicId?: string; sourcePage?: number | null; sourceKind?: string; notes?: string; estimates: Estimate[]; mappings: Mapping[]; usedEstimateIds?: string[]; topicOutcomeCount?: number;
  academicYearStart?: number; academicYearEnd?: number; sourceUrl?: string; documentSha256?: string; scopeEvidenceUrl?: string; outcomeCount?: number; lessonPeriods?: number | null; curriculumVersionId?: string | null }
interface Page { items: Node[]; totalCount: number; pageNumber: number; pageSize: number }
interface Listing { rows: Node[]; page: number; total: number; loading: boolean; error?: string }
interface Form { examDate: string; name: string; reason: string; parentId: string | null; gradeNumber: number | null; examCode: string | null; displayOrder: number | null; estimatedMinutes: number | null; code: string; officialTopicId: string | null; sourcePage: number | null; notes: string; moveParentOutcomes: boolean;
  academicYearStart: number; academicYearEnd: number; sourceUrl: string; documentSha256: string; scopeEvidenceUrl: string; outcomeCount: number; lessonPeriods: number | null; curriculumVersionId: string | null }
interface Edition { id: string; code: string; subjectName: string; academicYearStart: number; academicYearEnd: number }

@Component({ selector: 'app-coaching-curriculum-manager', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './coaching-curriculum-manager.component.html', styleUrl: './coaching-curriculum-manager.component.scss' })
export class CoachingCurriculumManagerComponent implements OnInit {
  private readonly http = inject(HttpClient); private readonly destroy = inject(DestroyRef);
  private readonly base = `${environment.apiUrl}/coaching-admin/curriculum-management`;
  private readonly outcomeBase = `${environment.apiUrl}/coaching-admin/outcomes`;
  private detailRequest?: Subscription; private destinationRequest?: Subscription; private listRequest?: Subscription;
  private initial = ''; private initialDuration = ''; private editionRequest?: Subscription;
  readonly listing = signal<Listing>({rows:[],page:1,total:0,loading:false}); readonly selected = signal<Node | null>(null);
  readonly busy = signal(false); readonly detailLoading = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly editing = signal(false); readonly creating = signal(false); readonly pending = signal<Node | null>(null);
  readonly destinations = signal<Node[]>([]); readonly destinationTotal = signal(0); readonly destinationLoading = signal(false);
  readonly editions = signal<Edition[]>([]); readonly officialTopics = signal<{ id: string; name: string; lessonPeriods: number }[]>([]);
  readonly labels: Record<Kind,string> = { exams:'Sınav türü',programs:'Kaynak program',programTopics:'Program konu grubu',lessons: 'Ders', units: 'Ünite', topics: 'Konu', outcomes: 'Kazanım' };
  readonly tabs = [{id:'exams',label:'Sınav türleri'},{id:'lessons',label:'Dersler'},{id:'units',label:'Üniteler'},{id:'topics',label:'Konular'},{id:'subtopics',label:'Alt konular'},{id:'outcomes',label:'Kazanımlar'},{id:'programs',label:'Kaynak programlar'},{id:'programTopics',label:'Program konu grupları'}];
  readonly hierarchyLevels = [{id:'exams',label:'Sınav türü'},{id:'lessons',label:'Ders'},{id:'units',label:'Ünite'},{id:'topics',label:'Konu'},{id:'subtopics',label:'Alt konu'},{id:'outcomes',label:'Kazanım'}];
  readonly exams = signal<{id:string;code:string;name:string;isActive:boolean}[]>([]);
  readonly parentOptions = signal<Node[]>([]); readonly parentLoading = signal(false); readonly parentTotal = signal(0);
  readonly curricula = signal<{id:string;name:string;versionLabel:string;isPublished:boolean}[]>([]);
  tab = 'exams'; pendingTab = ''; filterId = ''; parentSearch = ''; parentPage = 1; activateAfterCreate = true;
  private filterNode: Node | null = null;
  private parentRequest?: Subscription;
  form: Form = this.empty(); kind: Kind = 'exams'; rootSearch = ''; rootScope: Kind = 'exams';
  destinationKind: Kind = 'exams'; destinationSearch = ''; destinationPage = 1; destinationId = ''; sourceTopicId = '';
  moveConfirmed = false; deleteConfirmation = ''; statusConfirmed = false;
  estimateConfirmation = '';
  editionId = ''; learningMinutes = 0; practiceMinutes = 0; checkMinutes = 0; confidence = 'Low'; durationEvidence = ''; publishDuration = false;

  ngOnInit() { this.loadRoot(); this.destroy.onDestroy(() => { this.detailRequest?.unsubscribe(); this.destinationRequest?.unsubscribe(); this.editionRequest?.unsubscribe(); this.parentRequest?.unsubscribe(); this.listRequest?.unsubscribe(); }); }
  nodeType(n: Node) {
    if (n.kind === 'lessons') return ['Ders',n.gradeNumber ? `${n.gradeNumber}. sınıf` : null,n.examCode].filter(Boolean).join(' · ');
    return n.parentKind === 'topics' && n.kind === 'topics' ? 'Alt konu' : this.labels[n.kind];
  }
  branch() { return this.listing(); }
  empty(): Form { return { examDate:'',name:'',reason:'',parentId:null,gradeNumber:null,examCode:null,displayOrder:null,estimatedMinutes:null,code:'',officialTopicId:null,sourcePage:null,notes:'',moveParentOutcomes:false,
    academicYearStart:new Date().getFullYear(),academicYearEnd:new Date().getFullYear(),sourceUrl:'',documentSha256:'',scopeEvidenceUrl:'',outcomeCount:1,lessonPeriods:null,curriculumVersionId:null }; }
  dirty() { return this.editing() && (JSON.stringify(this.form) !== this.initial || this.durationDirty()); }
  private durationFields() { return {learningMinutes:this.learningMinutes,practiceMinutes:this.practiceMinutes,checkMinutes:this.checkMinutes,confidence:this.confidence,durationEvidence:this.durationEvidence,publishDuration:this.publishDuration}; }
  durationDirty() { return this.kind === 'outcomes' && !this.creating() && JSON.stringify(this.durationFields()) !== this.initialDuration; }
  loadRoot(page = 1) {
    this.listRequest?.unsubscribe();
    this.listing.update(previous => ({rows:page > 1 ? previous.rows : [],page,total:previous.total,loading:true}));
    let params=this.params(this.rootScope,this.filterId || null,page,this.rootSearch);
    if (!this.filterId) params=params.set('all','true');
    if (this.tab === 'topics' || this.tab === 'subtopics') params=params.set('level',this.tab === 'topics' ? 'root' : 'sub');
    this.listRequest=this.http.get<{data:Page}>(`${this.base}/children`,{params}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => {
      this.listing.update(previous => ({rows:[...(page > 1 ? previous.rows : []),...r.data.items],page,total:r.data.totalCount,loading:false}));
    },error:() => this.listing.update(previous => ({...previous,loading:false,error:'Liste alınamadı. Yeniden deneyin.'}))});
  }
  chooseTab(tab: string) {
    if (this.busy() || !this.tabs.some(x => x.id === tab)) return;
    if (this.dirty()) { this.pending.set(null); this.pendingTab=tab; return; }
    this.tab=tab; this.rootScope=tab === 'subtopics' ? 'topics' : tab as Kind;
    this.filterId=''; this.filterNode=null; this.rootSearch=''; this.parentSearch=''; this.parentOptions.set([]); this.parentRequest?.unsubscribe(); this.parentLoading.set(false); this.cancel();
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
    this.http.get<{data:{id:string;code:string;name:string;isActive:boolean}[]}>(`${environment.apiUrl}/coaching/catalog/exams`).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => this.exams.set(r.data),error:e => this.fail(e)});
  }
  private loadCurricula() {
    this.http.get<{data:{id:string;name:string;versionLabel:string;isPublished:boolean}[]}>(`${environment.apiUrl}/coaching-admin/curricula`).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => this.curricula.set(r.data),error:e => this.fail(e)});
  }
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
    this.selected.set(n); this.kind = n.kind; this.creating.set(false); this.editing.set(true);
    this.form = { ...this.empty(),name:n.name,parentId:n.parentId,gradeNumber:n.gradeNumber ?? null,examCode:n.examCode ?? null,displayOrder:n.displayOrder ?? null,estimatedMinutes:n.estimatedMinutes ?? null,
      examDate:n.examDate ?? '',code:n.code ?? '',officialTopicId:n.officialTopicId ?? null,sourcePage:n.sourceKind === 'ExamSkill' ? null : n.sourcePage ?? 1,notes:n.notes ?? '',
      academicYearStart:n.academicYearStart ?? new Date().getFullYear(),academicYearEnd:n.academicYearEnd ?? new Date().getFullYear(),sourceUrl:n.sourceUrl ?? '',documentSha256:n.documentSha256 ?? '',scopeEvidenceUrl:n.scopeEvidenceUrl ?? '',outcomeCount:n.outcomeCount ?? 1,lessonPeriods:n.lessonPeriods ?? null,curriculumVersionId:n.curriculumVersionId ?? null };
    this.initial = JSON.stringify(this.form); this.moveConfirmed = false; this.statusConfirmed = false; this.deleteConfirmation = ''; this.estimateConfirmation = ''; this.destinationId = ''; this.destinations.set([]);
    this.sourceTopicId = n.mappings?.[0]?.topicId ?? ''; this.destinationKind = n.kind === 'lessons' ? 'exams' : n.kind === 'units' ? 'lessons' : n.kind === 'outcomes' ? 'topics' : 'units';
    const estimate = n.estimates?.find(x => x.isPublished) ?? n.estimates?.[0];
    this.learningMinutes = estimate?.learningMinutes ?? 0; this.practiceMinutes = estimate?.practiceMinutes ?? 0; this.checkMinutes = estimate?.checkMinutes ?? 0;
    this.confidence = estimate?.confidence ?? 'Low'; this.durationEvidence = ''; this.publishDuration = false;
    this.initialDuration = JSON.stringify(this.durationFields()); if (unsaved) Object.assign(this,unsaved);
    if (n.kind === 'lessons') { this.loadExams(); this.loadCurricula(); }
    if (n.kind === 'programs') this.loadExams();
  }
  create(kind: Kind,parent?: Node) {
    if (this.busy() || this.dirty()) { this.error.set('Önce açık formdaki değişiklikleri kaydedin veya kaydı yeniden açın.'); return; }
    this.detailRequest?.unsubscribe(); this.detailLoading.set(false); this.kind = kind; this.selected.set(parent ?? null); this.creating.set(true); this.editing.set(true);
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
    const n = this.selected(); const r = {...this.form,examDate:this.form.examDate || null,name:this.form.name.trim(),reason:this.form.reason.trim(),...(!this.creating() && n ? {fingerprint:n.fingerprint} : {})};
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
    const params = this.params(this.destinationKind,null,page,this.destinationSearch).set('all','true');
    this.destinationRequest = this.http.get<{data:Page}>(`${this.base}/children`,{params}).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next:r => {
      this.destinations.set(r.data.items.filter(x => x.id !== this.selected()?.id)); this.destinationTotal.set(r.data.totalCount); this.destinationLoading.set(false);
    },error:e => { this.destinationLoading.set(false); this.fail(e); } });
  }
  move() {
    const n = this.selected(); if (this.creating() || this.busy() || !n || !this.destinationId || !this.moveConfirmed || this.form.reason.trim().length < 5 || this.dirtyFields() || this.durationDirty()) return;
    this.write(this.http.post<{data:Node}>(`${this.base}/${n.kind}/${n.id}/move`,{destinationId:this.destinationId,fingerprint:n.fingerprint,reason:this.form.reason.trim(),sourceTopicId:this.sourceTopicId || null}),'Taşıma tamamlandı. Önceki planların bağlantıları korundu.');
  }
  setActive() {
    const n = this.selected(); if (!n || this.busy() || this.dirtyFields() || this.durationDirty() || !this.statusConfirmed || this.form.reason.trim().length < 5) return;
    this.write(this.http.put<{data:Node}>(`${this.base}/${n.kind}/${n.id}/status`,{fingerprint:n.fingerprint,reason:this.form.reason.trim(),isActive:!n.isActive}),'Kullanım durumu güncellendi.');
  }
  dirtyFields() { const current = JSON.parse(this.initial || '{}') as Partial<Form>; return JSON.stringify({...this.form,reason:current.reason}) !== this.initial; }
  remove() {
    const n = this.selected(); if (!n?.canDelete || this.creating() || this.busy() || this.deleteConfirmation !== 'SİL' || this.form.reason.trim().length < 5) return;
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
  reviewMapping(m: Mapping) {
    const n = this.selected(); if (!n || this.busy() || this.form.reason.trim().length < 5) return;
    if (this.dirtyFields()) { this.error.set('Önce kazanım metni ve alan değişikliklerini kaydedin.'); return; }
    this.write(this.http.put(`${this.outcomeBase}/mappings/${m.topicId}/${n.id}`,{approved:!m.isApproved,evidence:this.form.reason.trim()}).pipe(switchMap(() => this.http.get<{data:Node}>(`${this.base}/outcomes/${n.id}`))),'Konu kapsamı onayı güncellendi.',true);
  }
  saveDuration() {
    const n = this.selected(); const parts = [this.learningMinutes,this.practiceMinutes,this.checkMinutes]; const total = parts.reduce((s,x) => s+x,0);
    if (!n || this.busy() || this.form.reason.trim().length < 5 || this.durationEvidence.trim().length < 5) return;
    if (this.dirtyFields()) { this.error.set('Önce kazanım metni ve alan değişikliklerini kaydedin. Süre girişleriniz korunacak.'); return; }
    if (parts.some(x => !Number.isInteger(x) || x < 0) || total < 1 || total > 1440) { this.error.set('Toplam süre 1–1440 dakika olmalıdır.'); return; }
    if (this.publishDuration && !n.mappings.some(x => x.isApproved)) { this.error.set('Önce en az bir konu eşleştirmesini onaylayın.'); return; }
    let created = false;
    const request = this.http.post<{data:Estimate}>(`${this.outcomeBase}/${n.id}/estimates`,{learningMinutes:this.learningMinutes,practiceMinutes:this.practiceMinutes,checkMinutes:this.checkMinutes,
      confidence:this.confidence,evidence:this.durationEvidence.trim(),reason:this.form.reason.trim()}).pipe(switchMap(r => {
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
  withdrawEstimate(e: Estimate) {
    const n = this.selected(); if (!n || this.busy() || this.estimateConfirmation !== e.id || this.form.reason.trim().length < 5) return;
    if (this.dirtyFields()) { this.error.set('Önce kazanım metni ve alan değişikliklerini kaydedin.'); return; }
    this.write(this.http.put<{data:Node}>(`${this.base}/outcomes/${n.id}/estimates/${e.id}/publication`,{fingerprint:n.fingerprint,reason:this.form.reason.trim(),isActive:false}),'Süre sürümü yayından kaldırıldı. Eski planların süreleri korundu.',true);
  }
  deleteEstimate(e: Estimate) {
    const n = this.selected(); if (!n || this.busy() || n.usedEstimateIds?.includes(e.id) || this.estimateConfirmation !== e.id || this.deleteConfirmation !== 'SİL' || this.form.reason.trim().length < 5) return;
    if (this.dirtyFields()) { this.error.set('Önce kazanım metni ve alan değişikliklerini kaydedin.'); return; }
    this.write(this.http.delete<{data:Node}>(`${this.base}/outcomes/${n.id}/estimates/${e.id}`,{body:{fingerprint:n.fingerprint,reason:this.form.reason.trim(),confirmId:e.id}}),'Kullanılmamış süre sürümü silindi.',true);
  }
  total(estimate: Estimate) { return estimate.learningMinutes + estimate.practiceMinutes + estimate.checkMinutes; }
  private write(request: Observable<{data:Node}>,message: string,preserveDuration = false) {
    this.busy.set(true); this.error.set(''); this.message.set('');
    request.pipe(takeUntilDestroyed(this.destroy),finalize(() => this.busy.set(false))).subscribe({next:r => { this.apply(r.data,preserveDuration); this.message.set(message); this.refreshList(); },error:e => this.fail(e)});
  }
  private refreshList() {
    this.loadRoot();
    if (this.kind === 'exams') this.loadExams();
  }
  private params(kind: Kind,parent: string | null,page: number,search: string) { let p = new HttpParams().set('kind',kind).set('page',page); if (parent) p = p.set('parentId',parent); if (search.trim()) p = p.set('search',search.trim()); return p; }
  private fail(e: {error?:{message?:unknown}}) { this.error.set(typeof e?.error?.message === 'string' ? e.error.message : 'İşlem tamamlanamadı. Girdiğiniz bilgiler korundu. Yeniden deneyin.'); }
}
