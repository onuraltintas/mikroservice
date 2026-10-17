import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, HostListener, Injector, Input, OnChanges, OnInit, ViewChild, afterNextRender, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, Subject, finalize, takeUntil } from 'rxjs';
import { CoachingStudyPlanningService, StudyPlan, StudyPlanPage, StudyPlanStatus, StudyTask, StudyTaskInput } from '../../../core/services/coaching-study-planning.service';
import {clock} from './weekly-calendar';
import {StudyPlanCalendarComponent} from './study-plan-calendar.component';
import {CalendarMove,calendarTaskOverdue} from './study-plan-calendar';

@Component({
  selector: 'app-student-study-plans',
  standalone: true,
  imports: [CommonModule, FormsModule, StudyPlanCalendarComponent],
  templateUrl: './student-study-plans.component.html',
  styleUrl: './student-study-plans.component.scss'
})
export class StudentStudyPlansComponent implements OnInit, OnChanges {
  readonly clock=clock;taskPage=1;
  readonly planView=signal<'calendar'|'list'>('list');readonly selectedTaskIds=signal<string[]>([]);
  private readonly injector=inject(Injector);
  @ViewChild('calendarTaskDetail') private calendarTaskDetail?:ElementRef<HTMLElement>;
  selectCalendarTasks(ids:string[]){this.selectedTaskIds.set(ids);afterNextRender(()=>this.calendarTaskDetail?.nativeElement.focus(),{injector:this.injector});}
  visibleTasks(){const plan=this.selected();if(this.planView()==='calendar')return plan?.tasks.filter(t=>this.selectedTaskIds().includes(t.id))??[];const tasks=this.filteredTasks();return plan?.weeklySource?tasks.slice((this.taskPage-1)*50,this.taskPage*50):tasks;}
  @Input() autoOpenActive=false;
  @Input() initialPlanId:string|null=null;
  @Input() taskFilter:'all'|'pending'|'completed'|'overdue'='all';
  filteredTasks(){return this.selected()?.tasks.filter(t=>this.taskFilter==='all'||this.taskFilter==='completed'&&t.isCompleted||this.taskFilter==='pending'&&!t.isCompleted||this.taskFilter==='overdue'&&this.isOverdue(t))??[];}
  isOverdue(task:StudyTask){return this.selected()?.status==='Active'&&calendarTaskOverdue(task,this.selected()?.weeklySource?.timeZoneId);}
  changeTaskFilter(){this.taskPage=1;this.planView.set('list');this.selectedTaskIds.set([]);}
  taskTitles:Record<string,string>={};
  correctTitle(task:StudyTask){const p=this.selected();if(!this.staffStudentId||this.readOnly||this.busy()||p?.status!=='Active'||task.isCompleted)return;
    const title=this.taskTitles[task.id]?.trim();if(!title||title.length>200){this.error.set('Çalışma başlığı 1–200 karakter olmalıdır.');return;}
    this.mutate(this.service.correctTitle(p.id,task.id,p.version,title,this.staffStudentId),'Çalışma başlığı düzeltildi. Kayıtlar korundu.');}
  @Input() staffStudentId: string | null = null;
  @Input() readOnly = false;
  private readonly scopeChanged = new Subject<void>();
  private initialized = false;
  private get studentArgument(): [string] | [] { return this.staffStudentId ? [this.staffStudentId] : []; }
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute, { optional: true });
  private readonly router = inject(Router, { optional: true });
  private requestedPlanId = this.route?.snapshot.queryParamMap.get('plan') ?? null;
  readonly page = signal<StudyPlanPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
  readonly selected = signal<StudyPlan | null>(null);
  readonly busy = signal(false);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly completionError = signal<{ taskId: string; field: 'minutes' | 'questions'; message: string } | null>(null);
  readonly success = signal<string | null>(null);
  readonly knownDraftId = signal<string | null>(null);
  editing = false;
  dirty = false;
  filter: StudyPlanStatus | '' = '';
  editTitle = '';
  editTasks: StudyTaskInput[] = [];
  publishConfirmed = false;
  actualMinutes: Record<string, number> = {};
  actualQuestions: Record<string, number | null> = {};
  moveCalendar(move:CalendarMove){const plan=this.selected();if(!plan||this.readOnly||this.busy()||plan.status==='Archived')return;
    this.mutate(this.service.move(plan.id,plan.version,move.taskIds,move.plannedDate,move.startMinute,...this.studentArgument),'Dersin günü ve saati güncellendi. Kazanımlar korundu.');}
  setDraftTime(task:StudyTaskInput,value:string){task.plannedStartMinute=value?Number(value.slice(0,2))*60+Number(value.slice(3)):null;this.dirty=true;}
  targetDates: Record<string, string> = {};

  ngOnInit() { this.initialized = true; this.loadList(); }
  ngOnChanges() {
    if (!this.initialized) return;
    this.scopeChanged.next();
    this.selected.set(null); this.page.set({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
    this.knownDraftId.set(null); this.filter = ''; this.editing = false; this.dirty = false;
    this.busy.set(false); this.loading.set(false); this.success.set(null); this.error.set(null);
    this.loadList();
  }

  canLeavePage() {
    if (this.busy()) { this.error.set('İşlem sürüyor. Tamamlanmasını bekleyin.'); return false; }
    return !(this.dirty || this.hasUnsavedTaskInputs()) || (typeof window !== 'undefined' && window.confirm('Kaydedilmemiş değişiklikleriniz var. Değişikliklerden vazgeçerek ayrılmak istiyor musunuz?'));
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent) {
    if (this.dirty || this.hasUnsavedTaskInputs() || this.busy()) { event.preventDefault(); event.returnValue = ''; }
  }

  loadList(pageNumber = 1) {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    this.service.list(pageNumber, this.filter || undefined, ...this.studentArgument).pipe(
      takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))
    ).subscribe({ next: page => {
      this.page.set(page);
      if(this.staffStudentId&&this.initialPlanId&&!this.selected()&&!this.editing){this.open(this.initialPlanId);}
      else if (!this.staffStudentId && this.requestedPlanId) {
        const id = this.requestedPlanId; this.requestedPlanId = null; this.open(id);
      } else if ((!this.staffStudentId||this.autoOpenActive) && !this.selected() && !this.editing && !this.filter) {
        const active = page.items.find(plan => plan.status === 'Active');
        if (active) this.open(active.id);
        else this.service.list(1, 'Active',...this.studentArgument).pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef))
          .subscribe({ next: plans => { if (plans.items.length && !this.selected() && !this.editing) this.open(plans.items[0].id); },
            error: error => this.showError(error, 'Aktif plan yüklenemedi. Lütfen tekrar deneyin.') });
      }
      if (pageNumber === 1 && (!this.filter || this.filter === 'Draft')) {
        this.knownDraftId.set(page.items.find(plan => plan.status === 'Draft')?.id ?? null);
      }
    }, error: error => this.showError(error, 'Planlar yüklenemedi. Lütfen tekrar deneyin.') });
  }

  open(id: string) {
    if (!this.canLeaveEditor()) return;
    this.busy.set(true);
    this.error.set(null);
    this.service.get(id, ...this.studentArgument).pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: plan => this.applyPlan(plan), error: error => this.showError(error, 'Plan açılamadı.') });
  }

  newDraft() {
    if (this.readOnly) return;
    if (!this.canLeaveEditor()) return;
    const draftId = this.selected()?.status === 'Draft' ? this.selected()!.id : this.knownDraftId();
    if (draftId) { this.open(draftId); return; }
    this.selected.set(null);
    this.editing = true;
    this.editTitle = '';
    this.editTasks = [];
    this.dirty = false;
    this.publishConfirmed = false;
    this.error.set(null);
    this.success.set(null);
    this.addTask();
  }

  addTask() {
    if (this.busy() || this.editTasks.length >= 500) return;
    const today = new Date();
    const plannedDate = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
    this.editTasks = [...this.editTasks, { plannedDate, title: '', plannedMinutes: 30, topicId: null, isPinned: false }];
    this.dirty = true;
  }

  removeTask(index: number) {
    if (this.busy()) return;
    this.editTasks = this.editTasks.filter((_, i) => i !== index);
    this.dirty = true;
  }

  discardChanges() {
    if (this.busy()) return;
    const plan = this.selected();
    if (plan) this.applyPlan(plan);
    else { this.editing = false; this.editTasks = []; this.dirty = false; }
    this.error.set(null);
  }

  saveDraft() {
    if (this.readOnly) return;
    const selected = this.selected();
    if (this.busy() || !this.editing || (selected && selected.status !== 'Draft')) return;
    if (!this.editTitle.trim() || this.editTitle.length > 200 || this.editTasks.some(task =>
      !task.title.trim() || task.title.length > 200 || !task.plannedDate || !Number.isInteger(task.plannedMinutes)
      || task.plannedMinutes < 1 || task.plannedMinutes > 1440 || (task.sourceName?.trim().length ?? 0) > 300
      || (task.plannedStartMinute!=null&&(!Number.isInteger(task.plannedStartMinute)||task.plannedStartMinute<0||task.plannedStartMinute+task.plannedMinutes>1440))
      || (task.plannedQuestions != null && (!Number.isInteger(task.plannedQuestions) || task.plannedQuestions < 0 || task.plannedQuestions > 100000)))) {
      this.error.set('Başlık, tarih, süre, kaynak ve soru sayılarını kontrol edin. Süre 1–1440 dakika, soru sayısı 0–100000 olmalıdır.');
      return;
    }
    const input = { title: this.editTitle.trim(), tasks: this.editTasks.map(task => ({ ...task, title: task.title.trim(), sourceName: task.sourceName?.trim() || null })) };
    this.mutate(selected ? this.service.replace(selected.id, selected.version, input, ...this.studentArgument) : this.service.create(input, ...this.studentArgument), 'Taslak kaydedildi. Henüz yayımlanmadı.');
  }

  publish() {
    if (this.readOnly) return;
    const plan = this.selected();
    if (!plan || plan.status !== 'Draft' || !plan.tasks.length || !this.publishConfirmed || this.dirty || this.busy()) return;
    this.mutate(this.service.publish(plan.id, plan.version, ...this.studentArgument), 'Plan yayımlandı. Önceki planın geçmişi korundu.');
  }
  archiveDraft() {
    if (this.readOnly) return;
    const plan = this.selected();
    if (!plan || plan.status !== 'Draft' || this.busy() || this.dirty || typeof window === 'undefined') return;
    if (!window.confirm('Taslak arşivlensin mi? Çalışmalar geçmişte korunacak; yeni bir taslak hazırlayabileceksiniz.')) return;
    this.mutate(this.service.archive(plan.id, plan.version, ...this.studentArgument), 'Taslak arşivlendi. Geçmiş korundu; yeni taslak hazırlayabilirsin.');
  }

  complete(task: StudyTask, actualMinutes: number, actualQuestions: number | null = null) {
    if (this.staffStudentId || this.readOnly) return;
    const plan = this.selected();
    if (!plan || plan.status !== 'Active' || task.isCompleted || this.busy()) return;
    if (!Number.isInteger(actualMinutes) || actualMinutes < 1 || actualMinutes > 1440) {
      this.completionError.set({ taskId: task.id, field: 'minutes', message: 'Gerçek çalışma süresini 1–1440 dakika arasında girin.' });
      return;
    }
    if (actualQuestions != null && (!Number.isInteger(actualQuestions) || actualQuestions < 0 || actualQuestions > 100000)) {
      this.completionError.set({ taskId: task.id, field: 'questions', message: 'Gerçek soru sayısını 0–100000 arasında tam sayı olarak girin.' }); return;
    }
    this.completionError.set(null);
    this.mutate(this.service.complete(plan.id, task.id, plan.version, actualMinutes, actualQuestions), 'Çalışma tamamlandı.');
  }

  reschedule(task: StudyTask, plannedDate: string) {
    if (this.readOnly) return;
    const plan = this.selected();
    if (!plan || plan.status !== 'Active' || task.isCompleted || this.busy()) return;
    if (!plannedDate) { this.error.set('Yeni çalışma tarihini seçin.'); return; }
    this.mutate(this.service.reschedule(plan.id, task.id, plan.version, plannedDate, ...this.studentArgument), 'Çalışma tarihi güncellendi.');
  }

  statusLabel(status: StudyPlanStatus) { return { Draft: 'Taslak', Active: 'Aktif', Archived: 'Arşiv' }[status]; }

  private applyPlan(plan: StudyPlan, preserveInputs = false) {
    const previous = this.selected();
    if(previous?.id!==plan.id||!!previous?.weeklySource!==!!plan.weeklySource){this.planView.set(this.taskFilter!=='all'?'list':plan.weeklySource?'calendar':'list');this.selectedTaskIds.set([]);}
    const keepInputs = preserveInputs && previous?.id === plan.id && plan.status === 'Active';
    const minutes = this.actualMinutes; const questions = this.actualQuestions; const dates = this.targetDates;
    const titles=this.taskTitles;
    this.selected.set(plan);
    this.editing = plan.status === 'Draft' && !this.readOnly && !plan.weeklySource;
    this.taskPage=1;
    this.editTitle = plan.title;
    this.editTasks = plan.tasks.map(({ plannedDate, title, plannedMinutes, topicId, isPinned, sourceName, plannedQuestions,plannedStartMinute }) =>
      ({ plannedDate, title, plannedMinutes, topicId, isPinned, sourceName: sourceName ?? null, plannedQuestions: plannedQuestions ?? null,plannedStartMinute:plannedStartMinute??null }));
    this.actualMinutes = Object.fromEntries(plan.tasks.map(task => [task.id, keepInputs && !task.isCompleted && Object.hasOwn(minutes, task.id) ? minutes[task.id] : task.actualMinutes ?? task.plannedMinutes]));
    this.actualQuestions = Object.fromEntries(plan.tasks.map(task => [task.id, keepInputs && !task.isCompleted && Object.hasOwn(questions, task.id) ? questions[task.id] : task.actualQuestions ?? null]));
    this.targetDates = Object.fromEntries(plan.tasks.map(task => [task.id, keepInputs && !task.isCompleted && dates[task.id] && dates[task.id] !== previous?.tasks.find(old => old.id === task.id)?.plannedDate ? dates[task.id] : task.plannedDate]));
    this.taskTitles=Object.fromEntries(plan.tasks.map(t=>[t.id,keepInputs&&!t.isCompleted&&titles[t.id]&&titles[t.id]!==previous?.tasks.find(old=>old.id===t.id)?.title?titles[t.id]:t.title]));
    this.dirty = false;
    this.publishConfirmed = false;
    this.completionError.set(null);
    if (plan.status === 'Draft') this.knownDraftId.set(plan.id);
    else if (this.knownDraftId() === plan.id) this.knownDraftId.set(null);
    if (!this.staffStudentId && this.route && this.router) {
      void this.router.navigate([], { relativeTo: this.route, queryParams: { plan: plan.id }, queryParamsHandling: 'merge', replaceUrl: true });
    }
  }

  private hasUnsavedTaskInputs() {
    const plan = this.selected();
    return !this.readOnly && plan?.status === 'Active' && plan.tasks.some(task => !task.isCompleted && (
      this.actualMinutes[task.id] !== (task.actualMinutes ?? task.plannedMinutes)
      || (this.actualQuestions[task.id] ?? null) !== (task.actualQuestions ?? null)
      || this.targetDates[task.id] !== task.plannedDate || !!this.staffStudentId&&this.taskTitles[task.id]!==task.title));
  }

  private canLeaveEditor() {
    if (this.busy()) return false;
    if (this.dirty) { this.error.set('Önce taslağınızı kaydedin veya değişikliklerden vazgeçin.'); return false; }
    if (this.hasUnsavedTaskInputs() && (typeof window === 'undefined' || !window.confirm('Kaydedilmemiş çalışma bilgileriniz var. Değişikliklerden vazgeçmek istiyor musunuz?'))) return false;
    this.success.set(null);
    return true;
  }

  private mutate(request: Observable<StudyPlan>, message: string) {
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    request.pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
      next: plan => { this.applyPlan(plan, true); this.success.set(message); this.loadList(this.page().pageNumber); },
      error: error => this.showError(error, 'İşlem tamamlanamadı. Lütfen tekrar deneyin.')
    });
  }

  private showError(error: unknown, fallback: string) {
    const response = error as { status?: number; error?: { message?: unknown } };
    const message = response?.error?.message;
    this.error.set(typeof message === 'string' ? message : fallback);
    if (response?.status === 409) this.error.update(value => `${value} Planı yeniden açarak güncel durumu kontrol edin.`);
  }
}
