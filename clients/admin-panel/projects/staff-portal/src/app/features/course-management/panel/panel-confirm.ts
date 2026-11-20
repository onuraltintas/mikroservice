import { Component, ElementRef, Injectable, effect, inject, signal, viewChild } from '@angular/core';

export interface PanelConfirmOptions {
  title?: string;
  confirmText?: string;
  cancelText?: string;
  /** Red confirm button, for an action that cancels or removes something. */
  danger?: boolean;
}

interface PanelConfirmRequest extends Required<PanelConfirmOptions> {
  message: string;
  resolve: (answer: boolean) => void;
}

/**
 * The panel's own yes/no question. The browser's confirm() box froze the page, ignored the panel's look and
 * read badly on a phone (20.11.2026 live test).
 */
@Injectable({ providedIn: 'root' })
export class PanelConfirm {
  readonly request = signal<PanelConfirmRequest | null>(null);

  ask(message: string, options: PanelConfirmOptions = {}): Promise<boolean> {
    this.request()?.resolve(false);
    return new Promise<boolean>(resolve => this.request.set({
      message,
      title: options.title ?? 'Emin misiniz?',
      confirmText: options.confirmText ?? 'Evet',
      cancelText: options.cancelText ?? 'Vazgeç',
      danger: options.danger ?? false,
      resolve
    }));
  }

  answer(value: boolean): void {
    const current = this.request();
    if (!current) return;
    this.request.set(null);
    current.resolve(value);
  }
}

@Component({
  selector: 'staff-panel-confirm-dialog',
  standalone: true,
  styleUrl: './panel.scss',
  styles: `.btn.btn-danger { background: var(--ds-danger); } .btn.btn-danger:hover:not(:disabled) { background: var(--ds-danger); filter: brightness(.92); }`,
  template: `
    @if (confirm.request(); as question) {
      <div class="dialog-backdrop" (click)="confirm.answer(false)">
        <section class="dialog" role="alertdialog" aria-modal="true" aria-labelledby="panel-confirm-title" aria-describedby="panel-confirm-text"
          (click)="$event.stopPropagation()" (keydown.escape)="confirm.answer(false)" style="max-width: 460px">
          <h2 id="panel-confirm-title" style="margin: 0">{{ question.title }}</h2>
          <p id="panel-confirm-text" style="margin: 0">{{ question.message }}</p>
          <div class="dialog-actions">
            <button #cancel type="button" class="btn-secondary" (click)="confirm.answer(false)">{{ question.cancelText }}</button>
            <button type="button" class="btn" [class.btn-danger]="question.danger" (click)="confirm.answer(true)">{{ question.confirmText }}</button>
          </div>
        </section>
      </div>
    }
  `
})
export class PanelConfirmDialogComponent {
  readonly confirm = inject(PanelConfirm);
  private readonly cancel = viewChild<ElementRef<HTMLButtonElement>>('cancel');

  constructor() {
    // Focus the safe answer each time a question opens, so Enter never confirms by accident.
    effect(() => this.cancel()?.nativeElement.focus());
  }
}
