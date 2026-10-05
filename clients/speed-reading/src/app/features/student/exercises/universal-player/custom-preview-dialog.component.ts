import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { applyCustomPreviewSettings } from './custom-preview-settings';

@Component({
  standalone: true,
  imports: [FormsModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Özel ayarlarla dene</h2>
    <mat-dialog-content>
      <p>Bu ayarlar yalnız bu önizlemeye uygulanır. Kayıtlı egzersiz ve ilerlemeniz değişmez.</p>
      <label for="preview-speed">Okuma hızı (kelime/dakika)</label>
      <input id="preview-speed" type="number" min="20" max="1500" step="1" [(ngModel)]="speedWpm">
      <small>20–1500 kelime/dakika</small>
      @if (configuration['engineType'] !== 'text_fade') {
      <label for="preview-chunk">Kelime grubu</label>
      <input id="preview-chunk" type="number" min="1" max="10" step="1" [(ngModel)]="chunkSize">
      <small>Her adımda 1–10 kelime</small>
      }
      @if (error) { <p role="alert">{{ error }}</p> }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button type="button" (click)="reset()">Varsayılana dön</button>
      <button type="button" mat-dialog-close>İptal</button>
      <button type="button" class="primary" (click)="submit()">Denemeyi başlat</button>
    </mat-dialog-actions>
  `,
  styles: [`label{display:block;font-weight:600;margin:18px 0 6px}input{width:100%;padding:10px;border:1px solid #aebbc9;border-radius:8px;font:inherit;box-sizing:border-box}small{display:block;margin-top:5px;color:#52647b}p{line-height:1.6}button{padding:10px 14px;border:1px solid #dce2ef;border-radius:8px;cursor:pointer;background:transparent;font:inherit}.primary{background:var(--primary-blue,#1976d2);color:white}button:focus-visible,input:focus-visible{outline:3px solid #1976d2;outline-offset:2px}[role=alert]{color:#b42318}`]
})
export class CustomPreviewDialogComponent {
  readonly configuration = inject<Record<string, unknown>>(MAT_DIALOG_DATA);
  private readonly dialog = inject(MatDialogRef<CustomPreviewDialogComponent>);
  speedWpm = 200;
  chunkSize = 1;
  error = '';
  constructor() { this.reset(); }
  reset(): void {
    const nested = (this.configuration['engineConfig'] ?? {}) as Record<string, unknown>;
    const pacer = (nested['pacer'] ?? {}) as Record<string, unknown>;
    const fading = (nested['fading'] ?? {}) as Record<string, unknown>;
    this.speedWpm = Number(nested['targetWpm'] ?? nested['wpm'] ?? this.configuration['targetWpm'] ?? this.configuration['wpm'] ?? pacer['speedWpm'] ?? fading['speedWpm'] ?? 200);
    this.chunkSize = Number(nested['chunkSize'] ?? this.configuration['chunkSize'] ?? pacer['chunkSize'] ?? 1);
    this.error = '';
  }
  submit(): void {
    try {
      const values = { speedWpm: this.speedWpm, chunkSize: this.chunkSize };
      applyCustomPreviewSettings(this.configuration, values, { roles: ['Teacher'], preview: true });
      this.dialog.close(values);
    } catch (error) { this.error = error instanceof Error ? error.message : 'Ayarları kontrol edin.'; }
  }
}
