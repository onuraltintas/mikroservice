import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, catchError, finalize, forkJoin, switchMap, throwError } from 'rxjs';
import { environment } from '../../../../environments/environment';

type Kind = 'groups' | 'lessons' | 'units' | 'topics' | 'outcomes';
interface Estimate { id: string; number: number; learningMinutes: number; practiceMinutes: number; checkMinutes: number; confidence: string; evidence: string; isPublished: boolean; method?: string }
interface Mapping { topicId: string; path: string; isApproved: boolean; evidence: string }
interface Node { kind: Kind; id: string; name: string; parentId: string | null; parentKind: Kind | null; isActive: boolean; path: string; fingerprint: string; canDelete: boolean; blockers: string[];
  code?: string; gradeNumber?: number | null; examCode?: string | null; displayOrder?: number | null; estimatedMinutes?: number | null; officialTopicId?: string; sourcePage?: number; notes?: string; estimates: Estimate[]; mappings: Mapping[]; usedEstimateIds?: string[] }
interface Page { items: Node[]; totalCount: number; pageNumber: number; pageSize: number }
interface Branch { rows: Node[]; page: number; total: number; loading: boolean; open: boolean; error?: string }
interface Form { name: string; reason: string; parentId: string | null; gradeNumber: number | null; examCode: string | null; displayOrder: number | null; estimatedMinutes: number | null; code: string; officialTopicId: string | null; sourcePage: number; notes: string }
interface Edition { id: string; code: string; subjectName: string; academicYearStart: number; academicYearEnd: number }

@Component({ selector: 'app-coaching-curriculum-manager', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './coaching-curriculum-manager.component.html', styleUrl: './coaching-curriculum-manager.component.scss' })
export class CoachingCurriculumManagerComponent implements OnInit {
  private readonly http = inject(HttpClient); private readonly destroy = inject(DestroyRef);
  private readonly base = `${environment.apiUrl}/coaching-admin/curriculum-management`;
  private readonly outcomeBase = `${environment.apiUrl}/coaching-admin/outcomes`;
  private detailRequest?: Subscription; private destinationRequest?: Subscription; private branchRequests = new Map<string, Subscription>();
  private initial = ''; private editionRequest?: Subscription;
  readonly branches = signal<Record<string, Branch>>({}); readonly selected = signal<Node | null>(null);
  readonly busy = signal(false); readonly detailLoading = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly editing = signal(false); readonly creating = signal(false); readonly pending = signal<Node | null>(null);
  readonly destinations = signal<Node[]>([]); readonly destinationTotal = signal(0); readonly destinationLoading = signal(false);
  readonly editions = signal<Edition[]>([]); readonly officialTopics = signal<{ id: string; name: string; lessonPeriods: number }[]>([]);
  readonly labels: Record<Kind,string> = { groups: 'Ders grubu', lessons: 'Ders', units: 'Ünite', topics: 'Konu', outcomes: 'Kazanım' };
  readonly exams = ['LGS','TYT','AYT','YDT','TDP'];
  form: Form = this.empty(); kind: Kind = 'groups'; rootSearch = ''; rootScope: 'groups' | 'lessons' | 'outcomes' = 'groups';
  destinationKind: Kind = 'groups'; destinationSearch = ''; destinationPage = 1; destinationId = ''; sourceTopicId = '';
  moveConfirmed = false; deleteConfirmation = ''; statusConfirmed = false;
  estimateConfirmation = '';
  editionId = ''; learningMinutes = 0; practiceMinutes = 0; checkMinutes = 0; confidence = 'Low'; durationEvidence = ''; publishDuration = false;

  ngOnInit() { this.loadRoot(); this.destroy.onDestroy(() => { this.detailRequest?.unsubscribe(); this.destinationRequest?.unsubscribe(); this.editionRequest?.unsubscribe(); this.branchRequests.forEach(x => x.unsubscribe()); }); }
  key(n: Node) { return `${n.kind}:${n.id}`; }
  nodeType(n: Node) { return n.parentKind === 'topics' && n.kind === 'topics' ? 'Alt konu' : this.labels[n.kind]; }
  branch(n?: Node): Branch | undefined { return this.branches()[n ? this.key(n) : 'root']; }
  empty(): Form { return { name:'',reason:'',parentId:null,gradeNumber:null,examCode:null,displayOrder:null,estimatedMinutes:null,code:'',officialTopicId:null,sourcePage:1,notes:'' }; }
  dirty() { return this.editing() && JSON.stringify(this.form) !== this.initial; }
  loadRoot(page = 1) { this.fetchBranch('root',this.rootScope,null,page); }
  changeRoot() { if (this.busy()) return; this.rootSearch = ''; this.loadRoot(); }
  expand(n: Node) {
    if (n.kind === 'outcomes') return;
    const key = this.key(n); const branch = this.branches()[key];
    if (branch) { this.branches.update(b => ({ ...b,[key]:{...branch,open:!branch.open} })); return; }
    this.loadChildren(n);
  }
  loadChildren(n: Node, page = 1) {
    const childKind: Kind = n.kind === 'groups' ? 'lessons' : n.kind === 'lessons' ? 'units' : 'topics';
    this.fetchBranch(this.key(n),childKind,n.id,page,n.kind === 'topics');
  }
  private fetchBranch(key: string,kind: Kind,parent: string | null,page: number,withOutcomes = false) {
    this.branchRequests.get(key)?.unsubscribe();
    this.branches.update(b => ({...b,[key]:{rows:page > 1 ? b[key]?.rows ?? [] : [],page,total:0,loading:true,open:true}}));
    const query = (k: Kind) => this.http.get<{data:Page}>(`${this.base}/children`,{params:this.params(k,parent,page,key === 'root' ? this.rootSearch : '')});
    const calls: Observable<{data:Page}>[] = withOutcomes ? [query(kind),query('outcomes')] : [query(kind)];
    const request = forkJoin(calls);
    this.branchRequests.set(key,request.pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: results => {
      this.branches.update(b => ({...b,[key]:{rows:[...(page > 1 ? b[key].rows : []),...results.flatMap(x => x.data.items)],page,total:results.reduce((s,x) => s+x.data.totalCount,0),loading:false,open:true}}));
    },error:() => this.branches.update(b => ({...b,[key]:{...b[key],loading:false,error:'Liste alınamadı. Yeniden deneyin.'}})) }));
  }
  select(n: Node) { if (this.busy()) return; if (this.dirty()) { this.pending.set(n); return; } this.open(n); }
  discard() { const n = this.pending(); this.pending.set(null); if (n) this.open(n); }
  private open(n: Node) {
    this.detailRequest?.unsubscribe(); this.error.set(''); this.message.set(''); this.editing.set(false); this.detailLoading.set(true);
    this.detailRequest = this.http.get<{data:Node}>(`${this.base}/${n.kind}/${n.id}`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next:r => { this.apply(r.data); this.detailLoading.set(false); },error:e => { this.detailLoading.set(false); this.fail(e); }
    });
  }
  private apply(n: Node) {
    this.selected.set(n); this.kind = n.kind; this.creating.set(false); this.editing.set(true);
    this.form = { ...this.empty(),name:n.name,parentId:n.parentId,gradeNumber:n.gradeNumber ?? null,examCode:n.examCode ?? null,displayOrder:n.displayOrder ?? null,estimatedMinutes:n.estimatedMinutes ?? null,
      code:n.code ?? '',officialTopicId:n.officialTopicId ?? null,sourcePage:n.sourcePage ?? 1,notes:n.notes ?? '' };
    this.initial = JSON.stringify(this.form); this.moveConfirmed = false; this.statusConfirmed = false; this.deleteConfirmation = ''; this.estimateConfirmation = ''; this.destinationId = ''; this.destinations.set([]);
    this.sourceTopicId = n.mappings?.[0]?.topicId ?? ''; this.destinationKind = n.kind === 'lessons' ? 'groups' : n.kind === 'units' ? 'lessons' : n.kind === 'outcomes' ? 'topics' : 'units';
    const estimate = n.estimates?.find(x => x.isPublished) ?? n.estimates?.[0];
    this.learningMinutes = estimate?.learningMinutes ?? 0; this.practiceMinutes = estimate?.practiceMinutes ?? 0; this.checkMinutes = estimate?.checkMinutes ?? 0;
    this.confidence = estimate?.confidence ?? 'Low'; this.durationEvidence = ''; this.publishDuration = false;
  }
  create(kind: Kind,parent?: Node) {
    if (this.busy() || this.dirty()) { this.error.set('Önce açık formdaki değişiklikleri kaydedin veya kaydı yeniden açın.'); return; }
    this.detailRequest?.unsubscribe(); this.detailLoading.set(false); this.kind = kind; this.selected.set(parent ?? null); this.creating.set(true); this.editing.set(true);
    this.form = {...this.empty(),parentId:parent?.id ?? null}; this.initial = JSON.stringify(this.form); this.error.set(''); this.message.set('');
    if (kind === 'outcomes') this.loadEditions();
  }
  cancel() { this.editing.set(false); this.creating.set(false); this.error.set(''); this.pending.set(null); }
  save() {
    if (this.busy() || !this.editing() || !this.form.name.trim() || this.form.reason.trim().length < 5) return;
    const n = this.selected(); const r = {...this.form,name:this.form.name.trim(),reason:this.form.reason.trim(),...(!this.creating() && n ? {fingerprint:n.fingerprint} : {})};
    this.write(this.creating() ? this.http.post<{data:Node}>(`${this.base}/${this.kind}`,r) : this.http.put<{data:Node}>(`${this.base}/${this.kind}/${n!.id}`,r),'Kayıt kaydedildi.');
  }
  searchDestinations(page = 1) {
    this.destinationRequest?.unsubscribe(); this.destinationPage = page; this.destinationId = ''; this.moveConfirmed = false; this.destinationLoading.set(true);
    const params = this.params(this.destinationKind,null,page,this.destinationSearch).set('all','true');
    this.destinationRequest = this.http.get<{data:Page}>(`${this.base}/children`,{params}).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next:r => {
      this.destinations.set(r.data.items.filter(x => x.id !== this.selected()?.id)); this.destinationTotal.set(r.data.totalCount); this.destinationLoading.set(false);
    },error:e => { this.destinationLoading.set(false); this.fail(e); } });
  }
  move() {
    const n = this.selected(); if (this.creating() || this.busy() || !n || !this.destinationId || !this.moveConfirmed || this.form.reason.trim().length < 5) return;
    this.write(this.http.post<{data:Node}>(`${this.base}/${n.kind}/${n.id}/move`,{destinationId:this.destinationId,fingerprint:n.fingerprint,reason:this.form.reason.trim(),sourceTopicId:this.sourceTopicId || null}),'Taşıma tamamlandı. Önceki planların bağlantıları korundu.');
  }
  setActive() {
    const n = this.selected(); if (!n || this.busy() || this.dirtyFields() || !this.statusConfirmed || this.form.reason.trim().length < 5) return;
    this.write(this.http.put<{data:Node}>(`${this.base}/${n.kind}/${n.id}/status`,{fingerprint:n.fingerprint,reason:this.form.reason.trim(),isActive:!n.isActive}),'Kullanım durumu güncellendi.');
  }
  dirtyFields() { const current = JSON.parse(this.initial || '{}') as Partial<Form>; return JSON.stringify({...this.form,reason:current.reason}) !== this.initial; }
  remove() {
    const n = this.selected(); if (!n?.canDelete || this.creating() || this.busy() || this.deleteConfirmation !== 'SİL' || this.form.reason.trim().length < 5) return;
    this.busy.set(true); this.error.set('');
    this.http.delete(`${this.base}/${n.kind}/${n.id}`,{body:{fingerprint:n.fingerprint,reason:this.form.reason.trim(),confirmId:n.id}}).pipe(takeUntilDestroyed(this.destroy),finalize(() => this.busy.set(false))).subscribe({
      next:() => { this.editing.set(false); this.selected.set(null); this.message.set('Kullanılmayan kayıt silindi.'); this.refreshTree(); },error:e => this.fail(e)
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
    this.write(this.http.put(`${this.outcomeBase}/mappings/${m.topicId}/${n.id}`,{approved:!m.isApproved,evidence:this.form.reason.trim()}).pipe(switchMap(() => this.http.get<{data:Node}>(`${this.base}/outcomes/${n.id}`))),'Konu kapsamı onayı güncellendi.');
  }
  saveDuration() {
    const n = this.selected(); const parts = [this.learningMinutes,this.practiceMinutes,this.checkMinutes]; const total = parts.reduce((s,x) => s+x,0);
    if (!n || this.busy() || this.form.reason.trim().length < 5 || this.durationEvidence.trim().length < 5) return;
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
    this.write(this.http.put(`${this.outcomeBase}/estimates/${estimate.id}/publication`,{reason:this.form.reason.trim()}).pipe(switchMap(() => this.http.get<{data:Node}>(`${this.base}/outcomes/${n.id}`))),'Süre sürümü yayınlandı.');
  }
  withdrawEstimate(e: Estimate) {
    const n = this.selected(); if (!n || this.busy() || this.estimateConfirmation !== e.id || this.form.reason.trim().length < 5) return;
    this.write(this.http.put<{data:Node}>(`${this.base}/outcomes/${n.id}/estimates/${e.id}/publication`,{fingerprint:n.fingerprint,reason:this.form.reason.trim(),isActive:false}),'Süre sürümü yayından kaldırıldı. Eski planların süreleri korundu.');
  }
  deleteEstimate(e: Estimate) {
    const n = this.selected(); if (!n || this.busy() || n.usedEstimateIds?.includes(e.id) || this.estimateConfirmation !== e.id || this.deleteConfirmation !== 'SİL' || this.form.reason.trim().length < 5) return;
    this.write(this.http.delete<{data:Node}>(`${this.base}/outcomes/${n.id}/estimates/${e.id}`,{body:{fingerprint:n.fingerprint,reason:this.form.reason.trim(),confirmId:e.id}}),'Kullanılmamış süre sürümü silindi.');
  }
  total(estimate: Estimate) { return estimate.learningMinutes + estimate.practiceMinutes + estimate.checkMinutes; }
  private write(request: Observable<{data:Node}>,message: string) {
    this.busy.set(true); this.error.set(''); this.message.set('');
    request.pipe(takeUntilDestroyed(this.destroy),finalize(() => this.busy.set(false))).subscribe({next:r => { this.apply(r.data); this.message.set(message); this.refreshTree(); },error:e => this.fail(e)});
  }
  private refreshTree() {
    const previous = this.branches(); const opened = Object.values(previous).flatMap(x => x.rows).filter(n => previous[this.key(n)]?.open);
    this.loadRoot(); for (const node of opened) this.loadChildren(node);
  }
  private params(kind: Kind,parent: string | null,page: number,search: string) { let p = new HttpParams().set('kind',kind).set('page',page); if (parent) p = p.set('parentId',parent); if (search.trim()) p = p.set('search',search.trim()); return p; }
  private fail(e: {error?:{message?:unknown}}) { this.error.set(typeof e?.error?.message === 'string' ? e.error.message : 'İşlem tamamlanamadı. Girdiğiniz bilgiler korundu. Yeniden deneyin.'); }
}
