import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import { CoachingStudyPlanningService, StudyPlan, StudyPlanPage, StudyPlanStatus, StudyTask, StudyTaskInput } from '../../../core/services/coaching-study-planning.service';

@Component({
  selector: 'app-student-study-plans',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './student-study-plans.component.html',
  styleUrl: './student-study-plans.component.scss'
})
export class StudentStudyPlansComponent implements OnInit {
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  readonly page = signal<StudyPlanPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
  readonly selected = signal<StudyPlan | null>(null);
  readonly busy = signal(false);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly knownDraftId = signal<string | null>(null);
  editing = false;
  dirty = false;
  filter: StudyPlanStatus | '' = '';
  editTitle = '';
  editTasks: StudyTaskInput[] = [];
  publishConfirmed = false;
  actualMinutes: Record<string, number> = {};
  targetDates: Record<string, string> = {};

  ngOnInit() { this.loadList(); }

  canLeavePage() {
    if (this.busy()) { this.error.set('İşlem sürüyor. Tamamlanmasını bekleyin.'); return false; }
    return !this.dirty || (typeof window !== 'undefined' && window.confirm('Kaydedilmemiş taslağınız var. Değişikliklerden vazgeçerek ayrılmak istiyor musunuz?'));
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent) {
    if (this.dirty || this.busy()) { event.preventDefault(); event.returnValue = ''; }
  }

  loadList(pageNumber = 1) {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    this.service.list(pageNumber, this.filter || undefined).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))
    ).subscribe({ next: page => {
      this.page.set(page);
      if (pageNumber === 1 && (!this.filter || this.filter === 'Draft')) {
        this.knownDraftId.set(page.items.find(plan => plan.status === 'Draft')?.id ?? null);
      }
    }, error: error => this.showError(error, 'Planlar yüklenemedi. Lütfen tekrar deneyin.') });
  }

  open(id: string) {
    if (!this.canLeaveEditor()) return;
    this.busy.set(true);
    this.error.set(null);
    this.service.get(id).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: plan => this.applyPlan(plan), error: error => this.showError(error, 'Plan açılamadı.') });
  }

  newDraft() {
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
    const selected = this.selected();
    if (this.busy() || !this.editing || (selected && selected.status !== 'Draft')) return;
    if (!this.editTitle.trim() || this.editTitle.length > 200 || this.editTasks.some(task =>
      !task.title.trim() || task.title.length > 200 || !task.plannedDate || !Number.isInteger(task.plannedMinutes)
      || task.plannedMinutes < 1 || task.plannedMinutes > 1440)) {
      this.error.set('Başlıkları, tarihleri ve süreleri kontrol edin. Süreler 1–1440 dakika olmalıdır.');
      return;
    }
    const input = { title: this.editTitle.trim(), tasks: this.editTasks.map(task => ({ ...task, title: task.title.trim() })) };
    this.mutate(selected ? this.service.replace(selected.id, selected.version, input) : this.service.create(input), 'Taslak kaydedildi. Henüz yayımlanmadı.');
  }

  publish() {
    const plan = this.selected();
    if (!plan || plan.status !== 'Draft' || !plan.tasks.length || !this.publishConfirmed || this.dirty || this.busy()) return;
    this.mutate(this.service.publish(plan.id, plan.version), 'Plan yayımlandı. Önceki planın geçmişi korundu.');
  }
  archiveDraft() {
    const plan = this.selected();
    if (!plan || plan.status !== 'Draft' || this.busy() || this.dirty || typeof window === 'undefined') return;
    if (!window.confirm('Taslak arşivlensin mi? Çalışmalar geçmişte korunacak; yeni bir taslak hazırlayabileceksiniz.')) return;
    this.mutate(this.service.archive(plan.id, plan.version), 'Taslak arşivlendi. Geçmiş korundu; yeni taslak hazırlayabilirsin.');
  }

  complete(task: StudyTask, actualMinutes: number) {
    const plan = this.selected();
    if (!plan || plan.status !== 'Active' || task.isCompleted || this.busy()) return;
    if (!Number.isInteger(actualMinutes) || actualMinutes < 1 || actualMinutes > 1440) {
      this.error.set('Gerçek çalışma süresini 1–1440 dakika arasında girin.');
      return;
    }
    this.mutate(this.service.complete(plan.id, task.id, plan.version, actualMinutes), 'Çalışma tamamlandı.');
  }

  reschedule(task: StudyTask, plannedDate: string) {
    const plan = this.selected();
    if (!plan || plan.status !== 'Active' || task.isCompleted || this.busy()) return;
    if (!plannedDate) { this.error.set('Yeni çalışma tarihini seçin.'); return; }
    this.mutate(this.service.reschedule(plan.id, task.id, plan.version, plannedDate), 'Çalışma tarihi güncellendi.');
  }

  statusLabel(status: StudyPlanStatus) { return { Draft: 'Taslak', Active: 'Aktif', Archived: 'Arşiv' }[status]; }

  private applyPlan(plan: StudyPlan) {
    this.selected.set(plan);
    this.editing = plan.status === 'Draft';
    this.editTitle = plan.title;
    this.editTasks = plan.tasks.map(({ plannedDate, title, plannedMinutes, topicId, isPinned }) => ({ plannedDate, title, plannedMinutes, topicId, isPinned }));
    this.actualMinutes = Object.fromEntries(plan.tasks.map(task => [task.id, task.actualMinutes ?? task.plannedMinutes]));
    this.targetDates = Object.fromEntries(plan.tasks.map(task => [task.id, task.plannedDate]));
    this.dirty = false;
    this.publishConfirmed = false;
    if (plan.status === 'Draft') this.knownDraftId.set(plan.id);
    else if (this.knownDraftId() === plan.id) this.knownDraftId.set(null);
  }

  private canLeaveEditor() {
    if (this.busy()) return false;
    if (this.dirty) { this.error.set('Önce taslağınızı kaydedin veya değişikliklerden vazgeçin.'); return false; }
    this.success.set(null);
    return true;
  }

  private mutate(request: Observable<StudyPlan>, message: string) {
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    request.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
      next: plan => { this.applyPlan(plan); this.success.set(message); this.loadList(this.page().pageNumber); },
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
