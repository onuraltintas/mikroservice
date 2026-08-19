import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EmailTemplateDto, EmailTemplateService } from '../../../core/services/email-template.service';

@Component({
  selector: 'app-email-templates',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="space-y-6"><header class="flex flex-wrap items-center justify-between gap-3"><div><h1 class="text-2xl font-bold text-gray-900 dark:text-white">E-posta Şablonları</h1><p class="text-sm text-gray-500">Gönderim kodunu değiştirmeden konu ve HTML gövdesini kontrollü şekilde yönetin.</p></div><button type="button" class="rounded-lg bg-indigo-600 px-4 py-2 text-white" (click)="creating.set(!creating())">{{ creating() ? 'Vazgeç' : 'Yeni şablon' }}</button></header>
      @if (error()) { <div class="rounded-lg bg-red-50 p-3 text-red-700" role="alert">{{ error() }}</div> }
      @if (creating()) { <form class="grid gap-3 rounded-xl border bg-white p-4 dark:border-gray-700 dark:bg-gray-800 md:grid-cols-2" (ngSubmit)="create()"><input class="rounded border p-2 dark:bg-gray-900" name="templateName" [(ngModel)]="createDraft.templateName" placeholder="Şablon adı (Auth_Welcome)" required maxlength="100"><input class="rounded border p-2 dark:bg-gray-900" name="createCategory" [(ngModel)]="createDraft.category" placeholder="Kategori" required maxlength="100"><input class="rounded border p-2 dark:bg-gray-900 md:col-span-2" name="createSubject" [(ngModel)]="createDraft.subject" placeholder="Konu" required maxlength="998"><textarea class="min-h-40 rounded border p-2 font-mono text-sm dark:bg-gray-900 md:col-span-2" name="createBody" [(ngModel)]="createDraft.body" placeholder="HTML gövdesi" required maxlength="100000"></textarea><div class="md:col-span-2"><button class="rounded bg-emerald-600 px-4 py-2 text-white" [disabled]="saving()">Oluştur</button></div></form> }
      <div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.5fr)]"><div class="space-y-2">@for (template of templates(); track template.id) { <button type="button" class="block w-full rounded-lg border p-3 text-left dark:border-gray-700" [class.border-indigo-500]="selected()?.id === template.id" (click)="select(template)"><div class="font-semibold">{{ template.templateName }}</div><div class="text-xs text-gray-500">{{ template.category }} · {{ template.isActive ? 'Aktif' : 'Pasif' }}</div></button> } @empty { <div class="rounded-lg border p-6 text-gray-500">Şablon bulunamadı.</div> }</div>
        @if (selected(); as template) { <form class="space-y-3 rounded-xl border bg-white p-4 dark:border-gray-700 dark:bg-gray-800" (ngSubmit)="save(template)"><div class="text-sm font-semibold">{{ template.templateName }}</div><input class="w-full rounded border p-2 dark:bg-gray-900" name="category" [(ngModel)]="edit.category" required maxlength="100"><input class="w-full rounded border p-2 dark:bg-gray-900" name="subject" [(ngModel)]="edit.subject" required maxlength="998"><textarea class="min-h-64 w-full rounded border p-2 font-mono text-sm dark:bg-gray-900" name="body" [(ngModel)]="edit.body" required maxlength="100000"></textarea><label class="flex items-center gap-2 text-sm"><input type="checkbox" name="isActive" [(ngModel)]="edit.isActive"> Aktif</label><button class="rounded bg-indigo-600 px-4 py-2 text-white" [disabled]="saving()">Kaydet</button></form> }
      </div>
    </section>
  `
})
export class EmailTemplatesComponent {
  private readonly service = inject(EmailTemplateService);
  private readonly platformId = inject(PLATFORM_ID);
  templates = signal<EmailTemplateDto[]>([]); selected = signal<EmailTemplateDto | null>(null); error = signal<string | null>(null); saving = signal(false);
  creating = signal(false);
  createDraft = { templateName: '', category: '', subject: '', body: '' };
  edit = { category: '', subject: '', body: '', isActive: true };
  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }
  load() { this.service.getAll().subscribe({ next: items => this.templates.set(items), error: () => this.error.set('Şablonlar yüklenemedi.') }); }
  select(template: EmailTemplateDto) { this.selected.set(template); this.edit = { category: template.category, subject: template.subject, body: template.body, isActive: template.isActive }; }
  save(template: EmailTemplateDto) { this.saving.set(true); this.service.update(template.id, this.edit).subscribe({ next: () => { this.saving.set(false); this.load(); }, error: () => { this.saving.set(false); this.error.set('Şablon kaydedilemedi.'); } }); }
  create() {
    if (!this.createDraft.templateName.trim()) return;
    this.saving.set(true);
    this.service.create(this.createDraft).subscribe({
      next: () => { this.saving.set(false); this.creating.set(false); this.createDraft = { templateName: '', category: '', subject: '', body: '' }; this.load(); },
      error: () => { this.saving.set(false); this.error.set('Şablon oluşturulamadı.'); }
    });
  }
}
