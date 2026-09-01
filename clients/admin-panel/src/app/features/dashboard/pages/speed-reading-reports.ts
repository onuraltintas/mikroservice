import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import {
  SpeedReadingAdminService,
  SpeedReadingReportSnapshot,
  SpeedReadingReportTemplate,
  SpeedReadingReportTemplateCreateRequest,
  SpeedReadingReportTemplateUpdateRequest,
  SpeedReadingScheduledReport,
  SpeedReadingScheduledReportCreateRequest,
  SpeedReadingScheduledReportUpdateRequest
} from '../../../core/services/speed-reading-admin.service';

type ReportTab = 'templates' | 'schedules' | 'snapshots';

@Component({
  selector: 'app-speed-reading-reports',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6">
      <header><p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Hızlı Okuma servisi</p><h1 class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Rapor yönetimi</h1><p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Rapor şablonlarını, dashboard snapshot'larını ve zamanlanmış raporları yönetin.</p></header>
      <nav class="flex flex-wrap gap-2" aria-label="Rapor sekmeleri">@for (tab of tabs; track tab.value) {<button type="button" (click)="selectTab(tab.value)" [attr.aria-pressed]="selectedTab() === tab.value" [class.bg-indigo-600]="selectedTab() === tab.value" [class.text-white]="selectedTab() === tab.value" class="rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 dark:border-gray-600 dark:text-gray-200">{{ tab.label }}</button>}</nav>
      @if (error()) {<div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div>}

      @if (selectedTab() === 'templates') {<section class="space-y-4"><div class="flex items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">Rapor şablonları</h2><p class="muted">Sistem şablonları salt okunur; özel şablonlar yönetilebilir.</p></div>@if (canManageReports()) {<button type="button" (click)="startTemplateCreate()" class="primary">Yeni şablon</button>}</div>@if (templateEditing()) {<form (ngSubmit)="saveTemplate()" class="form-card"><h3>{{ templateEditingId ? 'Şablonu düzenle' : 'Yeni şablon' }}</h3><div class="form-grid"><label>Ad<input [(ngModel)]="templateDraft.name" name="reportName" required maxlength="150" /></label><label>Tür<input type="number" [(ngModel)]="templateDraft.type" name="reportType" min="0" max="100" required /></label><label>Kategori<input type="number" [(ngModel)]="templateDraft.category" name="reportCategory" min="0" max="100" required /></label><label class="wide">Açıklama<textarea [(ngModel)]="templateDraft.description" name="reportDescription" required maxlength="1000"></textarea></label><label class="wide">Yapılandırma JSON<textarea [(ngModel)]="templateDraft.configurationJson" name="reportConfig" required maxlength="50000"></textarea></label>@if (templateEditingId) {<label class="check"><input type="checkbox" [(ngModel)]="templateUpdateDraft.isActive" name="reportActive" /> Aktif</label>}</div><div class="form-actions"><button type="button" (click)="cancelTemplateEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div></form>}<div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Ad</th><th>Tür</th><th>Kategori</th><th>Kaynak</th><th>Durum</th><th></th></tr></thead><tbody>@for (template of templates(); track template.id) {<tr><td><strong>{{ template.name }}</strong><div class="muted">{{ template.description }}</div></td><td>{{ template.type }}</td><td>{{ template.category }}</td><td>{{ template.isSystemTemplate ? 'Sistem' : 'Özel' }}</td><td>{{ template.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions">@if (canManageReports()) {<button type="button" (click)="startTemplateEdit(template)" [disabled]="template.isSystemTemplate">Düzenle</button><button type="button" (click)="deleteTemplate(template)" class="danger" [disabled]="template.isSystemTemplate">Sil</button>}</td></tr>} @empty {<tr><td colspan="6" class="empty">Şablon bulunamadı.</td></tr>}</tbody></table></div></div></section>}

      @if (selectedTab() === 'schedules') {<section class="space-y-4"><div class="flex items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">Zamanlanmış raporlar</h2><p class="muted">Rapor üretimi ve e-posta teslimat tercihlerini yönetin.</p></div>@if (canManageReports()) {<button type="button" (click)="startScheduleCreate()" class="primary">Yeni zamanlama</button>}</div>@if (scheduleEditing()) {<form (ngSubmit)="saveSchedule()" class="form-card"><h3>{{ scheduleEditingId ? 'Zamanlamayı düzenle' : 'Yeni zamanlama' }}</h3><div class="form-grid"><label>Şablon<select [(ngModel)]="scheduleDraft.reportTemplateId" name="scheduleTemplate" required [disabled]="!!scheduleEditingId"><option value="">Seçin</option>@for (template of templates(); track template.id) {<option [value]="template.id">{{ template.name }}</option>}</select></label><label>Sıklık<select [(ngModel)]="scheduleDraft.frequency" name="scheduleFrequency"><option [ngValue]="1">Günlük</option><option [ngValue]="2">Haftalık</option><option [ngValue]="3">Aylık</option></select></label><label>Haftanın günü<input type="number" [(ngModel)]="scheduleDraft.dayOfWeek" name="scheduleDayOfWeek" min="0" max="6" /></label><label>Ayın günü<input type="number" [(ngModel)]="scheduleDraft.dayOfMonth" name="scheduleDayOfMonth" min="1" max="31" /></label><label>Çalışma saati<input type="time" [(ngModel)]="scheduleDraft.deliveryTime" name="scheduleDeliveryTime" required /></label><label class="wide">E-posta alıcıları<input [(ngModel)]="scheduleDraft.emailRecipients" name="scheduleRecipients" placeholder="ornek@site.com, ikinci@site.com" maxlength="2000" /></label><label class="check"><input type="checkbox" [(ngModel)]="scheduleDraft.sendEmail" name="scheduleSendEmail" /> E-posta gönder</label><label class="check"><input type="checkbox" [(ngModel)]="scheduleDraft.saveToDashboard" name="scheduleSaveDashboard" /> Dashboard'a kaydet</label>@if (scheduleEditingId) {<label class="check"><input type="checkbox" [(ngModel)]="scheduleUpdateDraft.isActive" name="scheduleActive" /> Aktif</label>}</div><div class="form-actions"><button type="button" (click)="cancelScheduleEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div></form>}<div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Şablon</th><th>Sıklık</th><th>Sonraki çalışma</th><th>Başarı/başarısız</th><th>Durum</th><th></th></tr></thead><tbody>@for (schedule of schedules(); track schedule.id) {<tr><td>{{ schedule.reportTemplateName }}</td><td>{{ schedule.frequency }}</td><td>{{ schedule.nextRunAt ? (schedule.nextRunAt | date:'dd.MM.yyyy HH:mm') : '—' }}</td><td>{{ schedule.successCount }}/{{ schedule.failureCount }}</td><td>{{ schedule.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions">@if (canManageReports()) {<button type="button" (click)="startScheduleEdit(schedule)">Düzenle</button><button type="button" (click)="toggleSchedule(schedule)">{{ schedule.isActive ? 'Durdur' : 'Aktifleştir' }}</button><button type="button" (click)="deleteSchedule(schedule)" class="danger">Sil</button>}</td></tr>} @empty {<tr><td colspan="6" class="empty">Zamanlanmış rapor bulunamadı.</td></tr>}</tbody></table></div></div></section>}

      @if (selectedTab() === 'snapshots') {<section class="space-y-4"><div class="flex items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">Rapor snapshot'ları</h2><p class="muted">Şablondan manuel rapor görünümü üretin ve saklanan kayıtları yönetin.</p></div><div class="actions">@if (canManageReports()) {<select [(ngModel)]="snapshotTemplateId" name="snapshotTemplate"><option value="">Şablon seçin</option>@for (template of templates(); track template.id) {<option [value]="template.id">{{ template.name }}</option>}</select><button type="button" (click)="createSnapshot()" class="primary" [disabled]="!snapshotTemplateId || saving()">Snapshot üret</button>}</div></div><div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Şablon</th><th>Üretim</th><th>Aralık</th><th>Görüntüleme</th><th></th></tr></thead><tbody>@for (snapshot of snapshots(); track snapshot.id) {<tr><td>{{ snapshot.reportTemplateName }}</td><td>{{ snapshot.generatedAt | date:'dd.MM.yyyy HH:mm' }}</td><td>{{ snapshot.reportStartDate | date:'dd.MM.yyyy' }} – {{ snapshot.reportEndDate | date:'dd.MM.yyyy' }}</td><td>{{ snapshot.isViewed ? 'Görüldü' : 'Yeni' }}</td><td>@if (canManageReports()) {<button type="button" (click)="deleteSnapshot(snapshot)" class="danger">Sil</button>}</td></tr>} @empty {<tr><td colspan="5" class="empty">Snapshot bulunamadı.</td></tr>}</tbody></table></div></div></section>}
    </main>
  `,
  styles: [`
    :host { display: block; }
    .muted { color: rgb(107 114 128); font-size: .85rem; }
    .data-card, .form-card { border: 1px solid rgb(229 231 235); border-radius: .75rem; padding: 1rem; background: white; }
    .form-card { display: grid; gap: 1rem; }
    .form-grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); }
    label { display: grid; gap: .35rem; font-size: .875rem; font-weight: 500; color: rgb(55 65 81); }
    input, textarea, select { width: 100%; border: 1px solid rgb(209 213 219); border-radius: .5rem; padding: .55rem .7rem; background: transparent; font: inherit; color: inherit; }
    textarea { min-height: 5rem; resize: vertical; }
    .wide { grid-column: 1 / -1; }
    .check { display: flex; align-items: center; gap: .5rem; } .check input { width: auto; }
    .primary, .secondary, .danger { border-radius: .5rem; padding: .55rem .8rem; font-size: .875rem; font-weight: 600; }
    .primary { background: rgb(79 70 229); color: white; } .secondary { border: 1px solid rgb(209 213 219); } .danger { color: rgb(185 28 28); }
    .actions, .form-actions { display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; } .form-actions { justify-content: flex-end; }
    .actions select { min-width: 14rem; } .empty { color: rgb(107 114 128); padding: 1.25rem; text-align: center; }
    @media (prefers-color-scheme: dark) { .data-card, .form-card { background: rgb(17 24 39); border-color: rgb(55 65 81); } label { color: rgb(229 231 235); } input, textarea, select { border-color: rgb(75 85 99); } }
  `]
})
export class SpeedReadingReportsComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService);
  private readonly authService = inject(AuthService);

  readonly canManageReports = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingReportManage));

  readonly tabs: { value: ReportTab; label: string }[] = [
    { value: 'templates', label: 'Şablonlar' },
    { value: 'schedules', label: 'Zamanlamalar' },
    { value: 'snapshots', label: 'Snapshot’lar' }
  ];
  readonly selectedTab = signal<ReportTab>('templates');
  readonly templates = signal<SpeedReadingReportTemplate[]>([]);
  readonly schedules = signal<SpeedReadingScheduledReport[]>([]);
  readonly snapshots = signal<SpeedReadingReportSnapshot[]>([]);
  readonly templateEditing = signal(false);
  readonly scheduleEditing = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');

  templateEditingId: string | null = null;
  templateDraft: SpeedReadingReportTemplateCreateRequest = this.emptyTemplate();
  templateUpdateDraft: SpeedReadingReportTemplateUpdateRequest = { name: '', description: '', configurationJson: '{}', isActive: true };
  scheduleEditingId: string | null = null;
  scheduleDraft: SpeedReadingScheduledReportCreateRequest = this.emptySchedule();
  scheduleUpdateDraft: SpeedReadingScheduledReportUpdateRequest = this.emptyScheduleUpdate();
  snapshotTemplateId = '';

  ngOnInit(): void { this.loadTemplates(); this.loadSchedules(); this.loadSnapshots(); }

  selectTab(tab: ReportTab): void { this.selectedTab.set(tab); this.error.set(''); }
  loadTemplates(): void { this.service.getReportTemplates(undefined, undefined, 100).subscribe({ next: value => this.templates.set(value), error: () => this.error.set('Rapor şablonları yüklenemedi.') }); }
  loadSchedules(): void { this.service.getScheduledReports(100).subscribe({ next: value => this.schedules.set(value), error: () => this.error.set('Rapor zamanlamaları yüklenemedi.') }); }
  loadSnapshots(): void { this.service.getReportSnapshots(100).subscribe({ next: value => this.snapshots.set(value), error: () => this.error.set('Rapor snapshot’ları yüklenemedi.') }); }

  startTemplateCreate(): void { this.templateEditingId = null; this.templateDraft = this.emptyTemplate(); this.templateEditing.set(true); }
  startTemplateEdit(template: SpeedReadingReportTemplate): void {
    if (template.isSystemTemplate) return;
    this.templateEditingId = template.id;
    this.templateDraft = { name: template.name, description: template.description, type: Number(template.type) || 0, category: Number(template.category) || 0, configurationJson: template.configurationJson };
    this.templateUpdateDraft = { name: template.name, description: template.description, configurationJson: template.configurationJson, isActive: template.isActive };
    this.templateEditing.set(true);
  }
  cancelTemplateEdit(): void { this.templateEditing.set(false); this.templateEditingId = null; }
  saveTemplate(): void {
    const request: Observable<unknown> = this.templateEditingId
      ? this.service.updateReportTemplate(this.templateEditingId, { name: this.templateDraft.name, description: this.templateDraft.description, configurationJson: this.templateDraft.configurationJson, isActive: this.templateUpdateDraft.isActive })
      : this.service.createReportTemplate(this.templateDraft);
    this.saveRequest(request, () => { this.cancelTemplateEdit(); this.loadTemplates(); });
  }
  deleteTemplate(template: SpeedReadingReportTemplate): void { if (template.isSystemTemplate || !globalThis.confirm('Bu rapor şablonu silinsin mi?')) return; this.saveRequest(this.service.deleteReportTemplate(template.id), () => this.loadTemplates()); }

  startScheduleCreate(): void { this.scheduleEditingId = null; this.scheduleDraft = this.emptySchedule(); this.scheduleEditing.set(true); }
  startScheduleEdit(schedule: SpeedReadingScheduledReport): void {
    this.scheduleEditingId = schedule.id;
    this.scheduleDraft = { reportTemplateId: schedule.reportTemplateId, frequency: Number(schedule.frequency) || 1, dayOfWeek: schedule.dayOfWeek, dayOfMonth: schedule.dayOfMonth, deliveryTime: schedule.deliveryTime.slice(0, 5), sendEmail: schedule.sendEmail, saveToDashboard: schedule.saveToDashboard, emailRecipients: schedule.emailRecipients };
    this.scheduleUpdateDraft = { ...this.scheduleDraft, isActive: schedule.isActive };
    this.scheduleEditing.set(true);
  }
  cancelScheduleEdit(): void { this.scheduleEditing.set(false); this.scheduleEditingId = null; }
  saveSchedule(): void {
    const request: Observable<unknown> = this.scheduleEditingId
      ? this.service.updateScheduledReport(this.scheduleEditingId, { frequency: this.scheduleDraft.frequency, dayOfWeek: this.scheduleDraft.dayOfWeek, dayOfMonth: this.scheduleDraft.dayOfMonth, deliveryTime: this.scheduleDraft.deliveryTime, isActive: this.scheduleUpdateDraft.isActive, sendEmail: this.scheduleDraft.sendEmail, saveToDashboard: this.scheduleDraft.saveToDashboard, emailRecipients: this.scheduleDraft.emailRecipients })
      : this.service.createScheduledReport(this.scheduleDraft);
    this.saveRequest(request, () => { this.cancelScheduleEdit(); this.loadSchedules(); });
  }
  toggleSchedule(schedule: SpeedReadingScheduledReport): void { this.saveRequest(this.service.updateScheduledReportStatus(schedule.id, !schedule.isActive), () => this.loadSchedules()); }
  deleteSchedule(schedule: SpeedReadingScheduledReport): void { if (!globalThis.confirm('Bu rapor zamanlaması silinsin mi?')) return; this.saveRequest(this.service.deleteScheduledReport(schedule.id), () => this.loadSchedules()); }

  createSnapshot(): void { if (!this.snapshotTemplateId) return; this.saveRequest(this.service.createReportSnapshot({ reportTemplateId: this.snapshotTemplateId, data: {} }), () => this.loadSnapshots()); }
  deleteSnapshot(snapshot: SpeedReadingReportSnapshot): void { if (!globalThis.confirm('Bu rapor snapshot’ı silinsin mi?')) return; this.saveRequest(this.service.deleteReportSnapshot(snapshot.id), () => this.loadSnapshots()); }

  private saveRequest(request: Observable<unknown>, onSuccess: () => void): void { this.saving.set(true); this.error.set(''); request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: onSuccess, error: () => this.error.set('Rapor işlemi tamamlanamadı.') }); }
  private emptyTemplate(): SpeedReadingReportTemplateCreateRequest { return { name: '', description: '', type: 0, category: 0, configurationJson: '{}' }; }
  private emptySchedule(): SpeedReadingScheduledReportCreateRequest { return { reportTemplateId: '', frequency: 1, dayOfWeek: null, dayOfMonth: null, deliveryTime: '09:00', sendEmail: false, saveToDashboard: true, emailRecipients: null }; }
  private emptyScheduleUpdate(): SpeedReadingScheduledReportUpdateRequest { return { ...this.emptySchedule(), isActive: true }; }
}
