import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, Injector, Input, OnChanges, ViewChild, afterNextRender, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, Subscription, finalize, takeUntil } from 'rxjs';
import { CoachingWeeklyReviewService, DecisionSave, ReflectionSave, WeeklyDecision, WeeklyReview } from '../../../core/services/coaching-weekly-review.service';
import { calendarDate, dateKey } from './study-plan-calendar';
import { WeeklyResultsComponent } from './weekly-results.component';
import { WeeklyDecisionWorkComponent } from './weekly-decision-work.component';

export function shiftWeek(day: string, days: number) { const date = calendarDate(day); date.setDate(date.getDate() + days); return dateKey(date); }
export function mondayOf(day: string) { const date = calendarDate(day); return shiftWeek(day, -((date.getDay() + 6) % 7)); }

@Component({ selector: 'app-weekly-review', standalone: true, imports: [CommonModule, FormsModule, RouterLink, WeeklyDecisionWorkComponent, WeeklyResultsComponent],
  templateUrl: './weekly-review.component.html', styleUrl: './weekly-review.component.scss' })
export class WeeklyReviewComponent implements OnChanges {
  private readonly toaster = coachingToaster();
  @Input() staffStudentId: string | null = null;
  @Input() readOnly = false;
  @Input() initialWeek: string | null = null;
  @Input() decisionId: string | null = null;
  private readonly api = inject(CoachingWeeklyReviewService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly scopeChanged = new Subject<void>();
  private request?: Subscription;
  private pendingDecisionId: string | null = null;
  readonly data = signal<WeeklyReview | null>(null);
  readonly loading = signal(false); readonly saving = signal(false); readonly error = signal(''); readonly success = signal('');
  readonly stale = signal(false);
  week = shiftWeek(mondayOf(new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())), -7);
  decisionEffects:Record<string,{effect:string;note:string}>={};
  readonly effectOptions=[{value:"Helped",label:"İşe yaradı"},{value:"PartlyHelped",label:"Kısmen işe yaradı"},{value:"DidNotHelp",label:"İşe yaramadı"},{value:"NotTried",label:"Uygulanmadı"}];
  reviewNote="";
  dirty = false;
  reflection: ReflectionSave = { expectedVersion: null, successes: '', obstacles: '', nextFocus: '', rating: null };
  editor: { id: string; week: string; body: DecisionSave; dateLocked?: boolean } | null = null;
  postponement: { row: WeeklyDecision; dueOn: string; reason: string } | null = null;
  @ViewChild('workEditor') private workEditor?: WeeklyDecisionWorkComponent;
  readonly workDecision = signal<WeeklyDecision | null>(null);
  readonly statuses = [{ code: 'Planned', name: 'Planlandı' }, { code: 'InProgress', name: 'Devam ediyor' }, { code: 'Completed', name: 'Tamamlandı' }, { code: 'Cancelled', name: 'İptal' }];
  readonly shift = shiftWeek;
  ngOnInit() { this.load(); }
  ngOnChanges() {
    this.scopeChanged.next(); this.request?.unsubscribe(); this.data.set(null); this.reviewNote=""; this.editor = null; this.postponement = null; this.dirty = false;
    this.workDecision.set(null);
    this.reflection = { expectedVersion: null, successes: '', obstacles: '', nextFocus: '', rating: null };
    this.saving.set(false); this.stale.set(false); this.error.set(''); this.success.set('');
    if (this.initialWeek) this.week = this.initialWeek;
    this.pendingDecisionId = this.decisionId; this.focusedDecision.set(null);
    // Angular invokes ngOnChanges before ngOnInit; later scope changes must reload as well.
    if (this.initialized) this.load();
  }
  private initialized = false;
  canRefresh() { return !this.saving() && !this.dirty && (this.workEditor?.canRefresh() ?? true); }
  async canLeavePage() { return !this.saving() && (await this.workEditor?.canLeavePage() ?? true) && (!this.dirty || await this.toaster.confirm('Kaydedilmemiş haftalık değerlendirmeniz veya kararınız var. Kaydetmeden ayrılmak istiyor musunuz?')); }
  async changeWeek(day: string) {
    if (!day || !await this.canLeavePage()) return;
    this.reviewNote=""; this.week = mondayOf(day); this.pendingDecisionId = null; this.focusedDecision.set(null); this.editor = null; this.postponement = null; this.workDecision.set(null); this.dirty = false; this.success.set(''); this.load();
  }
  async refresh() { if (!await this.canLeavePage()) return; this.reviewNote=""; this.editor = null; this.postponement = null; this.workDecision.set(null); this.dirty = false; this.load(this.data()?.decisionPage ?? 1); }
  async page(value: number) { if (!await this.canLeavePage()) return; this.reviewNote=""; this.editor = null; this.postponement = null; this.workDecision.set(null); this.dirty = false; this.load(value); }
  load(page = 1) {
    this.initialized = true; this.request?.unsubscribe(); this.loading.set(true); this.error.set('');
    const focus = this.pendingDecisionId;
    const request = focus ? this.api.get(this.staffStudentId, this.week, page, focus) : this.api.get(this.staffStudentId, this.week, page);
    this.request = request.pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: value => {
        this.data.set(value); this.week = value.weekStart; this.stale.set(false);
        this.focusedDecision.set(focus); this.pendingDecisionId = null;
        if (this.focusedDecision()) afterNextRender(() => {
          const card = this.host.nativeElement.querySelector<HTMLElement>('.focused-decision');
          card?.scrollIntoView?.({block:'center'});
        }, {injector:this.injector});
        this.decisionEffects=Object.fromEntries((value.results?.decisions??[]).map(row=>{
          const saved=value.reflection?.results?.decisions.find(x=>x.decisionId===row.decisionId);
          return [row.decisionId,{effect:saved?.effect??'',note:saved?.effectNote??''}];
        }));
        this.reflection = { expectedVersion: value.reflection?.version ?? null, successes: value.reflection?.successes ?? '', obstacles: value.reflection?.obstacles ?? '', nextFocus: value.reflection?.nextFocus ?? '', rating: value.reflection?.rating ?? null };
      }, error: e => { this.data.set(null); this.error.set(e.error?.message ?? 'Haftalık değerlendirme yüklenemedi. Tekrar deneyin.'); }
    });
  }
  acknowledgeReflection() {
    const view=this.data();
    if(this.editor||this.postponement||this.workDecision()||!this.staffStudentId||this.readOnly||!view?.canDecide||!view.needsTeacherReview||!view.reflection||this.saving()||this.stale()||this.reviewNote.trim().length<5)return;
    this.saving.set(true);this.error.set('');
    this.api.reviewReflection(this.staffStudentId,this.week,view.reflection.version,this.reviewNote.trim(),...(view.results?[view.results.fingerprint]:[]))
      .pipe(takeUntil(this.scopeChanged),takeUntilDestroyed(this.destroyRef),finalize(()=>this.saving.set(false))).subscribe({
        next:()=>{this.reviewNote='';this.dirty=false;this.success.set('İnceleme kaydedildi.');this.load();},error:e=>this.failed(e)
      });
  }
  saveReflection() {
    if (!this.data()?.canReflect || this.readOnly || this.staffStudentId || this.saving() || this.stale()) return;
    this.saving.set(true); this.error.set(''); this.success.set('');
    const context=this.data()?.results;
    const decisionEffects=Object.entries(this.decisionEffects).filter(([,value])=>value.effect).map(([decisionId,value])=>({decisionId,effect:value.effect,note:value.note.trim()}));
    if(decisionEffects.some(x=>x.note.length<5||x.note.length>1000)){this.saving.set(false);this.error.set('Seçilen her karar sonucu için en az 5 karakterlik not yaz.');return;}
    const body=context?{...this.reflection,expectedResultsFingerprint:context.fingerprint,decisionEffects}:{...this.reflection};
    this.api.reflect(this.week, body).pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: value => { this.reflection.expectedVersion = value.version; this.dirty = false; this.success.set('Haftalık değerlendirmen kaydedildi.'); this.load(); }, error: e => this.failed(e)
    });
  }
  newDecisionMinDate(week: string) {
    const nextWeek = shiftWeek(week, 7);
    const today = this.data()?.today ?? nextWeek;
    return today > nextWeek ? today : nextWeek;
  }
  previousDecisionOptions() {
    const view=this.data();if(!view?.reflection)return [];
    return [...new Map([...(view.reflection.results?.decisions??[]),...(view.results?.decisions??[])].filter(x=>x.sourceWeekStart<this.week).map(x=>[x.decisionId,x])).values()];
  }
  async newDecision(previousDecisionId:string|null=null) {
    if (!this.data()?.canDecide || this.readOnly || !await this.canLeavePage()) return;
    this.reviewNote="";
    this.workDecision.set(null);
    this.postponement = null;
    this.editor = { id: crypto.randomUUID(), week: this.week, body: { expectedVersion: null, title: '', note: '', dueOn: this.newDecisionMinDate(this.week), status: 'Planned',...(this.data()?.reflection?{sourceReflectionVersion:this.data()!.reflection!.version,previousDecisionId}:{}),...(this.data()?.results?{expectedResultsFingerprint:this.data()!.results!.fingerprint}:{}) } };
    this.dirty = true; this.stale.set(false); this.error.set('');
  }
  async editDecision(row: WeeklyDecision) {
    if (!row.canEdit || this.readOnly || !await this.canLeavePage()) return;
    this.reviewNote="";
    this.workDecision.set(null);
    this.postponement = null;
    this.editor = { id: row.id, week: row.reviewWeekStart, dateLocked: true, body: { expectedVersion: row.version, title: row.title, note: row.note, dueOn: row.dueOn, status: row.status } };
    this.dirty = false; this.stale.set(false); this.error.set('');
  }
  async cancelEdit() { if (await this.canLeavePage()) { this.editor = null; this.postponement = null; this.dirty = false; this.stale.set(false); } }
  saveDecision() {
    const editor = this.editor;
    if (!editor || !this.staffStudentId || !this.data()?.canDecide || this.readOnly || this.saving() || this.stale()) return;
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.api.decide(this.staffStudentId, editor.week, editor.id, { ...editor.body }).pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.editor = null; this.postponement = null; this.dirty = false; this.success.set('Karar kaydedildi; öğrenci de görebilir.'); this.load(this.data()?.decisionPage ?? 1); }, error: e => this.failed(e)
    });
  }
  async startPostponement(row: WeeklyDecision) {
    if (!row.canEdit || !['Planned','InProgress'].includes(row.status) || this.readOnly || !this.staffStudentId || !this.data()?.canDecide || !await this.canLeavePage()) return;
    this.reviewNote=""; this.editor=null; this.workDecision.set(null); this.dirty=false; this.stale.set(false); this.error.set('');
    this.postponement={row,dueOn:row.isOverdue?shiftWeek(this.data()!.today,1):row.dueOn,reason:''};
  }
  deadlineMinDate(row:WeeklyDecision) {
    const tomorrow=shiftWeek(this.data()!.today,1),nextWeek=shiftWeek(row.reviewWeekStart,7);
    return tomorrow>nextWeek?tomorrow:nextWeek;
  }
  savePostponement() {
    const form=this.postponement;
    if (!form || !this.staffStudentId || !this.data()?.canDecide || this.readOnly || this.saving() || this.stale()) return;
    if(form.reason.trim().length<5||form.reason.trim().length>1000||form.dueOn<=this.data()!.today||form.dueOn<shiftWeek(form.row.reviewWeekStart,7)||form.dueOn===form.row.dueOn){this.error.set('Gelecekteki farklı bir son tarih ve en az 5 karakterlik değişiklik gerekçesi girin.');return;}
    this.saving.set(true); this.error.set(''); this.success.set('');
    const change=form.row.isOverdue?this.api.postpone.bind(this.api):this.api.changeDeadline.bind(this.api);
    change(this.staffStudentId,form.row.reviewWeekStart,form.row.id,
      {expectedVersion:form.row.version,dueOn:form.dueOn,reason:form.reason.trim()})
      .pipe(takeUntil(this.scopeChanged),takeUntilDestroyed(this.destroyRef),finalize(()=>this.saving.set(false))).subscribe({
        next:()=>{this.postponement=null;this.dirty=false;this.success.set('Kararın son tarihi gerekçesiyle değiştirildi. Bağlı çalışma ve ödev tarihleri korunuyor.');this.load(this.data()?.decisionPage??1);},
        error:e=>this.failed(e)
      });
  }
  private failed(e: { status?: number; error?: { message?: string } }) {
    if (e.status === 409) this.stale.set(true);
    if (e.status === 403 || e.status === 404) { this.data.set(null); this.editor = null; this.postponement = null; this.dirty = false; this.reflection = { expectedVersion: null, successes: '', obstacles: '', nextFocus: '', rating: null }; }
    this.error.set(e.error?.message ?? 'Kayıt tamamlanamadı. Girdilerin korunuyor; tekrar deneyebilirsin.');
  }
  status(code: string) { return this.statuses.find(s => s.code === code)?.name ?? code; }
  async openWork(row: WeeklyDecision) {
    if (!row.canEdit || this.readOnly || !this.staffStudentId || !await this.canLeavePage()) return;
    this.reviewNote="";this.editor=null;this.postponement=null;this.dirty=false;this.workDecision.set(row);
  }
  closeWork() { this.workDecision.set(null); this.success.set('İlgili işlerin güncel durumu yüklendi.'); this.load(this.data()?.decisionPage ?? 1); }
  workStatus(code: string) { return ({Pending:'Bekliyor',Completed:'Tamamlandı',Archived:'Plan arşivlendi',Assigned:'Atandı',InProgress:'Devam ediyor',Submitted:'Teslim edildi',Graded:'Değerlendirildi',Cancelled:'İptal edildi',Unavailable:'Erişilemiyor'} as Record<string,string>)[code] ?? code; }
  readonly focusedDecision = signal<string | null>(null);
  private readonly feedback = coachingFeedback(this.success, this.error);
}
