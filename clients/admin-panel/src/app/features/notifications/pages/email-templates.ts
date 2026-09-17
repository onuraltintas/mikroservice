import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { EmailTemplateDto, EmailTemplateService } from '../../../core/services/email-template.service';

type BodyMode = 'visual' | 'preview' | 'source';
type BodyTarget = 'create' | 'edit';

@Component({
  selector: 'app-email-templates',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="space-y-6">
      <header class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">E-posta Şablonları</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">
            Konu ve içerikleri görsel olarak düzenleyin; HTML kaynağı yalnızca gerektiğinde açılır.
          </p>
        </div>
        <div class="flex gap-2">
          <button type="button" class="rounded-lg border px-4 py-2" [disabled]="loading()" (click)="load()">
            {{ loading() ? 'Yükleniyor…' : 'Yenile' }}
          </button>
          <button type="button" class="rounded-lg bg-indigo-600 px-4 py-2 text-white" (click)="toggleCreating()">
            {{ creating() ? 'Vazgeç' : 'Yeni şablon' }}
          </button>
        </div>
      </header>

      @if (error()) {
        <div class="rounded-lg bg-red-50 p-3 text-red-700 dark:bg-red-950/30 dark:text-red-200" role="alert">
          {{ error() }}
        </div>
      }

      @if (creating()) {
        <form class="template-form rounded-xl border bg-white p-5 dark:border-gray-700 dark:bg-gray-800" (ngSubmit)="create()">
          <div class="form-heading">
            <div>
              <h2>Yeni e-posta şablonu</h2>
              <p>Şablon adını ve kategorisini belirleyin, ardından içeriği görsel düzenleyicide hazırlayın.</p>
            </div>
            <span class="step-badge">Yeni kayıt</span>
          </div>

          <div class="form-grid">
            <label>
              Şablon adı
              <input class="form-control" name="templateName" [(ngModel)]="createDraft.templateName" placeholder="Auth_Welcome" required maxlength="100">
            </label>
            <label>
              Kategori
              <input class="form-control" name="createCategory" [(ngModel)]="createDraft.category" placeholder="Kimlik doğrulama" required maxlength="100">
            </label>
            <label class="wide">
              E-posta konusu
              <input class="form-control" name="createSubject" [(ngModel)]="createDraft.subject" placeholder="Hoş geldiniz, {{ '{{FirstName}}' }}" required maxlength="998">
            </label>
          </div>

          <div class="editor-shell">
            <div class="editor-heading">
              <div>
                <h3>Gövde içeriği</h3>
                <p>Görsel düzenleyicide metni doğrudan değiştirin. Yer tutucular gönderim sırasında gerçek değerlerle doldurulur.</p>
              </div>
              <div class="mode-tabs" role="tablist" aria-label="Yeni şablon gövde görünümü">
                <button type="button" [class.active]="createBodyMode() === 'visual'" [attr.aria-pressed]="createBodyMode() === 'visual'" (click)="switchBodyMode('visual', 'create')">Görsel düzenle</button>
                <button type="button" [class.active]="createBodyMode() === 'preview'" [attr.aria-pressed]="createBodyMode() === 'preview'" (click)="switchBodyMode('preview', 'create')">Önizleme</button>
                <button type="button" [class.active]="createBodyMode() === 'source'" [attr.aria-pressed]="createBodyMode() === 'source'" (click)="switchBodyMode('source', 'create')">HTML kaynağı</button>
              </div>
            </div>

            @if (createBodyMode() === 'visual') {
              <div class="editor-toolbar" aria-label="Biçimlendirme araçları">
                <button type="button" (click)="formatVisual('bold', 'create', createVisualEditor)" title="Kalın">Kalın</button>
                <button type="button" (click)="formatVisual('italic', 'create', createVisualEditor)" title="İtalik">İtalik</button>
                <button type="button" (click)="formatVisual('underline', 'create', createVisualEditor)" title="Altı çizili">Altı çizili</button>
                <button type="button" (click)="formatVisual('insertUnorderedList', 'create', createVisualEditor)" title="Madde işaretli liste">Liste</button>
              </div>
              <div #createVisualEditor class="visual-editor" contenteditable="true" role="textbox" aria-label="Görsel e-posta gövdesi" aria-multiline="true" data-placeholder="E-posta metnini buraya yazın…" [innerHTML]="visualCreateBody" (input)="onVisualInput($event, 'create')"></div>
            } @else if (createBodyMode() === 'preview') {
              <iframe class="email-preview" [attr.srcdoc]="previewHtml(createDraft.body)" sandbox title="Yeni şablon e-posta önizlemesi"></iframe>
            } @else {
              <textarea class="source-editor" name="createBody" [(ngModel)]="createDraft.body" placeholder="HTML gövdesi" required maxlength="100000"></textarea>
            }
            <p class="editor-hint"><strong>Önizleme:</strong> Yer tutucular örnek değerlerle gösterilir. Gönderim kodunu değiştirmeden yalnızca içerik kaydedilir.</p>
          </div>

          <div class="form-actions">
            <button class="rounded bg-emerald-600 px-4 py-2 text-white" [disabled]="saving()">Oluştur</button>
          </div>
        </form>
      }

      <div class="template-layout">
        <div class="template-list">
          <div class="list-heading">
            <div>
              <h2>Kayıtlı şablonlar</h2>
              <p>{{ templates().length }} şablon · Gönderim kodları korunur</p>
            </div>
          </div>
          @for (template of templates(); track template.id) {
            <button type="button" class="template-card" [class.selected]="selected()?.id === template.id" (click)="select(template)">
              <div class="font-semibold">{{ template.templateName }}</div>
              <div class="text-xs text-gray-500">{{ categoryLabel(template.category) }} · {{ template.isActive ? 'Aktif' : 'Pasif' }}</div>
              <div class="template-subject">{{ template.subject }}</div>
            </button>
          } @empty {
            <div class="rounded-lg border p-6 text-gray-500">
              {{ loading() ? 'Şablonlar yükleniyor…' : 'Şablon bulunamadı.' }}
            </div>
          }
        </div>

        @if (selected(); as template) {
          <form class="template-form rounded-xl border bg-white p-5 dark:border-gray-700 dark:bg-gray-800" (ngSubmit)="save(template)">
            <div class="form-heading">
              <div>
                <div class="eyebrow">Şablon düzenleme</div>
                <h2>{{ template.templateName }}</h2>
                <p>Gönderim kodu sabittir. Konu, kategori, içerik ve aktiflik durumunu yönetin.</p>
              </div>
              <span class="status-badge" [class.inactive]="!edit.isActive">{{ edit.isActive ? 'Aktif' : 'Pasif' }}</span>
            </div>

            <div class="form-grid">
              <label>
                Kategori
                <input class="form-control" name="category" [(ngModel)]="edit.category" required maxlength="100">
              </label>
              <label>
                E-posta konusu
                <input class="form-control" name="subject" [(ngModel)]="edit.subject" required maxlength="998">
              </label>
            </div>

            <div class="editor-shell">
              <div class="editor-heading">
                <div>
                  <h3>Gövde içeriği</h3>
                  <p>Varsayılan görünüm görsel düzenleyicidir. Kod görünümü yalnızca ileri düzey değişiklikler içindir.</p>
                </div>
                <div class="mode-tabs" role="tablist" aria-label="Şablon gövde görünümü">
                  <button type="button" [class.active]="editBodyMode() === 'visual'" [attr.aria-pressed]="editBodyMode() === 'visual'" (click)="switchBodyMode('visual', 'edit')">Görsel düzenle</button>
                  <button type="button" [class.active]="editBodyMode() === 'preview'" [attr.aria-pressed]="editBodyMode() === 'preview'" (click)="switchBodyMode('preview', 'edit')">Önizleme</button>
                  <button type="button" [class.active]="editBodyMode() === 'source'" [attr.aria-pressed]="editBodyMode() === 'source'" (click)="switchBodyMode('source', 'edit')">HTML kaynağı</button>
                </div>
              </div>

              @if (editBodyMode() === 'visual') {
                <div class="editor-toolbar" aria-label="Biçimlendirme araçları">
                  <button type="button" (click)="formatVisual('bold', 'edit', editVisualEditor)" title="Kalın">Kalın</button>
                  <button type="button" (click)="formatVisual('italic', 'edit', editVisualEditor)" title="İtalik">İtalik</button>
                  <button type="button" (click)="formatVisual('underline', 'edit', editVisualEditor)" title="Altı çizili">Altı çizili</button>
                  <button type="button" (click)="formatVisual('insertUnorderedList', 'edit', editVisualEditor)" title="Madde işaretli liste">Liste</button>
                </div>
                <div #editVisualEditor class="visual-editor" contenteditable="true" role="textbox" aria-label="Görsel e-posta gövdesi" aria-multiline="true" [innerHTML]="visualEditBody" (input)="onVisualInput($event, 'edit')"></div>
              } @else if (editBodyMode() === 'preview') {
                <iframe class="email-preview" [attr.srcdoc]="previewHtml(edit.body)" sandbox title="E-posta şablonu önizlemesi"></iframe>
              } @else {
                <textarea class="source-editor" name="body" [(ngModel)]="edit.body" required maxlength="100000"></textarea>
              }
              <p class="editor-hint"><strong>Örnek değerler:</strong> {{ '{{FirstName}}' }} gibi yer tutucular önizlemede örnek verilerle gösterilir ve kaydedilen içerikte aynen korunur.</p>
            </div>

            <label class="active-toggle">
              <input type="checkbox" name="isActive" [(ngModel)]="edit.isActive">
              <span>Bu şablon gönderimlerde kullanılabilir</span>
            </label>
            <div class="form-actions">
              <button class="rounded bg-indigo-600 px-4 py-2 text-white" [disabled]="saving()">{{ saving() ? 'Kaydediliyor…' : 'Kaydet' }}</button>
            </div>
          </form>
        }
      </div>
    </section>
  `,
  styles: [`
    .template-layout { display: grid; gap: 1rem; grid-template-columns: minmax(15rem, .7fr) minmax(0, 1.5fr); align-items: start; }
    .template-list { display: grid; gap: .65rem; }
    .list-heading, .form-heading, .editor-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
    .list-heading h2, .form-heading h2, .editor-heading h3 { margin: 0; color: var(--ui-text); font-size: 1.05rem; font-weight: 700; }
    .list-heading p, .form-heading p, .editor-heading p { margin: .25rem 0 0; color: var(--ui-text-muted); font-size: .82rem; line-height: 1.45; }
    .template-card { width: 100%; border: 1px solid var(--ui-border); border-radius: .85rem; padding: 1rem; text-align: left; background: var(--ui-surface); color: var(--ui-text); }
    .template-card.selected { border-color: var(--ui-brand); box-shadow: 0 0 0 2px color-mix(in srgb, var(--ui-brand) 18%, transparent); }
    .template-subject { margin-top: .55rem; overflow: hidden; color: var(--ui-text-muted); font-size: .78rem; text-overflow: ellipsis; white-space: nowrap; }
    .template-form { display: grid; gap: 1.15rem; color: var(--ui-text); }
    .form-heading { padding-bottom: .85rem; border-bottom: 1px solid var(--ui-border); }
    .eyebrow { margin-bottom: .25rem; color: var(--ui-brand); font-size: .72rem; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
    .step-badge, .status-badge { flex: none; border-radius: 999px; padding: .35rem .65rem; background: color-mix(in srgb, var(--ui-brand) 12%, transparent); color: var(--ui-brand); font-size: .72rem; font-weight: 700; }
    .status-badge.inactive { background: color-mix(in srgb, var(--ui-text-muted) 14%, transparent); color: var(--ui-text-muted); }
    .form-grid { display: grid; gap: 1rem; grid-template-columns: repeat(2, minmax(0, 1fr)); }
    label { display: grid; gap: .4rem; color: var(--ui-text); font-size: .84rem; font-weight: 600; }
    .wide { grid-column: 1 / -1; }
    .form-control, .source-editor { width: 100%; border: 1px solid var(--ui-border-strong); border-radius: .6rem; padding: .65rem .75rem; background: var(--ui-surface); color: var(--ui-text); font: inherit; }
    .form-control:focus, .source-editor:focus, .visual-editor:focus { outline: 2px solid var(--ui-brand); outline-offset: 1px; border-color: var(--ui-brand); }
    .editor-shell { display: grid; gap: .75rem; border: 1px solid var(--ui-border); border-radius: .85rem; padding: 1rem; background: var(--ui-surface-muted); }
    .editor-heading { align-items: center; }
    .mode-tabs { display: flex; flex-wrap: wrap; gap: .3rem; padding: .25rem; border: 1px solid var(--ui-border); border-radius: .6rem; background: var(--ui-surface); }
    .mode-tabs button, .editor-toolbar button { border: 1px solid transparent; border-radius: .45rem; padding: .42rem .62rem; background: transparent; color: var(--ui-text-muted); font-size: .75rem; font-weight: 700; }
    .mode-tabs button.active { border-color: color-mix(in srgb, var(--ui-brand) 38%, transparent); background: color-mix(in srgb, var(--ui-brand) 12%, transparent); color: var(--ui-brand); }
    .editor-toolbar { display: flex; flex-wrap: wrap; gap: .35rem; border-bottom: 1px solid var(--ui-border); padding-bottom: .55rem; }
    .editor-toolbar button { border-color: var(--ui-border); background: var(--ui-surface); }
    .visual-editor { min-height: 20rem; overflow: auto; border: 1px solid var(--ui-border-strong); border-radius: .65rem; padding: 1rem; background: #fff; color: #172033; line-height: 1.55; }
    .visual-editor:empty::before { content: attr(data-placeholder); color: #94a3b8; pointer-events: none; }
    .source-editor { min-height: 20rem; resize: vertical; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: .78rem; line-height: 1.55; }
    .email-preview { display: block; width: 100%; min-height: 30rem; border: 1px solid var(--ui-border-strong); border-radius: .65rem; background: #fff; }
    .editor-hint { margin: 0; color: var(--ui-text-muted); font-size: .75rem; line-height: 1.45; }
    .active-toggle { display: flex; align-items: center; gap: .55rem; font-weight: 600; }
    .form-actions { display: flex; justify-content: flex-end; gap: .6rem; }
    @media (max-width: 900px) { .template-layout { grid-template-columns: 1fr; } }
    @media (max-width: 640px) { .form-grid { grid-template-columns: 1fr; } .wide { grid-column: auto; } .editor-heading { align-items: flex-start; flex-direction: column; } .mode-tabs { width: 100%; } .mode-tabs button { flex: 1; } }
  `]
})
export class EmailTemplatesComponent {
  private readonly service = inject(EmailTemplateService);
  private readonly platformId = inject(PLATFORM_ID);

  templates = signal<EmailTemplateDto[]>([]);
  selected = signal<EmailTemplateDto | null>(null);
  error = signal<string | null>(null);
  saving = signal(false);
  loading = signal(false);
  creating = signal(false);
  createBodyMode = signal<BodyMode>('visual');
  editBodyMode = signal<BodyMode>('visual');

  createDraft = { templateName: '', category: '', subject: '', body: '' };
  edit = { category: '', subject: '', body: '', isActive: true };
  visualCreateBody = '';
  visualEditBody = '';

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getAll().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: items => {
        this.templates.set(items);
        const current = this.selected();
        if (current) {
          const refreshed = items.find(item => item.id === current.id);
          if (refreshed) {
            this.select(refreshed);
          } else {
            this.selected.set(null);
          }
        }
      },
      error: () => {
        this.templates.set([]);
        this.selected.set(null);
        this.error.set('Şablonlar yüklenemedi. Lütfen tekrar deneyin.');
      }
    });
  }

  toggleCreating(): void {
    const next = !this.creating();
    this.creating.set(next);
    if (next) {
      this.createDraft = { templateName: '', category: '', subject: '', body: '' };
      this.visualCreateBody = '';
      this.createBodyMode.set('visual');
    }
  }

  select(template: EmailTemplateDto): void {
    this.selected.set(template);
    this.edit = {
      category: template.category,
      subject: template.subject,
      body: template.body,
      isActive: template.isActive
    };
    this.visualEditBody = this.extractBody(template.body);
    this.editBodyMode.set('visual');
    this.error.set(null);
  }

  switchBodyMode(mode: BodyMode, target: BodyTarget): void {
    if (mode === 'visual') {
      if (target === 'edit') {
        this.visualEditBody = this.extractBody(this.edit.body);
      } else {
        this.visualCreateBody = this.extractBody(this.createDraft.body);
      }
    }

    if (target === 'edit') {
      this.editBodyMode.set(mode);
    } else {
      this.createBodyMode.set(mode);
    }
  }

  onVisualInput(event: Event, target: BodyTarget): void {
    const editor = event.currentTarget as HTMLElement | null;
    if (editor) {
      this.updateVisualBody(editor, target);
    }
  }

  formatVisual(command: string, target: BodyTarget, editor: HTMLElement): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    editor.focus();
    document.execCommand(command, false);
    this.updateVisualBody(editor, target);
  }

  previewHtml(body: string): string {
    const source = body?.trim()
      ? body
      : '<div style="padding:48px;text-align:center;font-family:Arial,sans-serif;color:#64748b">Önizleme için içerik ekleyin.</div>';

    return this.replacePreviewValues(source)
      .replace(/<script\b[\s\S]*?<\/script>/gi, '')
      .replace(/\son[a-z]+\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]+)/gi, '')
      .replace(/\s(href|src)\s*=\s*(["'])\s*javascript:[\s\S]*?\2/gi, ' $1="#"');
  }

  categoryLabel(category: string): string {
    const labels: Record<string, string> = {
      auth: 'Kimlik doğrulama',
      coaching: 'Koçluk',
      support: 'Destek',
      notification: 'Bildirim'
    };
    return labels[category.trim().toLowerCase()] ?? category;
  }

  save(template: EmailTemplateDto): void {
    this.saving.set(true);
    this.error.set(null);
    this.service.update(template.id, this.edit).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Şablon kaydedilemedi. Lütfen alanları kontrol edip tekrar deneyin.')
    });
  }

  create(): void {
    if (!this.createDraft.templateName.trim() || !this.createDraft.category.trim() || !this.createDraft.subject.trim() || !this.createDraft.body.trim()) {
      this.error.set('Şablon adı, kategori, konu ve gövde alanları zorunludur.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.service.create(this.createDraft).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.creating.set(false);
        this.createDraft = { templateName: '', category: '', subject: '', body: '' };
        this.visualCreateBody = '';
        this.load();
      },
      error: () => this.error.set('Şablon oluşturulamadı. Lütfen alanları kontrol edip tekrar deneyin.')
    });
  }

  private updateVisualBody(editor: HTMLElement, target: BodyTarget): void {
    const fragment = editor.innerHTML;
    if (target === 'edit') {
      this.visualEditBody = fragment;
      this.edit.body = this.mergeBody(this.edit.body, fragment);
    } else {
      this.visualCreateBody = fragment;
      this.createDraft.body = this.mergeBody(this.createDraft.body, fragment);
    }
  }

  private extractBody(source: string): string {
    const match = source.match(/<body\b[^>]*>([\s\S]*?)<\/body\s*>/i);
    return match ? match[1].trim() : source;
  }

  private mergeBody(source: string, fragment: string): string {
    const match = source.match(/([\s\S]*?<body\b[^>]*>)([\s\S]*?)(<\/body\s*>[\s\S]*)/i);
    return match ? `${match[1]}${fragment}${match[3]}` : fragment;
  }

  private replacePreviewValues(source: string): string {
    const values: Record<string, string> = {
      FirstName: 'Ayşe',
      LastName: 'Yılmaz',
      Email: 'ayse.yilmaz@example.com',
      CourseName: 'Hızlı Okuma Başlangıç Programı',
      Subject: 'Destek talebiniz',
      Date: '17.09.2026',
      PasswordSetupUrl: 'https://masterhizliokuma.com/auth/setup-password',
      VerificationLink: 'https://masterhizliokuma.com/auth/verify-email',
      ResetLink: 'https://masterhizliokuma.com/auth/reset-password'
    };

    return source.replace(/\{\{\s*([A-Za-z0-9_]+)\s*\}\}/g, (placeholder, key: string) => values[key] ?? placeholder);
  }
}
