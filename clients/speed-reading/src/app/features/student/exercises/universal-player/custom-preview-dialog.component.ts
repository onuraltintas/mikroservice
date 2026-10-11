import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { applyCustomPreviewSettings, getCustomPreviewControls, PreviewControl } from './custom-preview-settings';
import { caseInsensitiveField, mergeCaseInsensitiveRecords, recordOrEmpty } from './engines/reading-pacer-safety';

@Component({
  standalone: true,
  imports: [FormsModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Özel ayarlarla dene</h2>
    <mat-dialog-content>
      @if (isRsvp) {
        <p>300 ms sabitleme işareti yalnız başlangıçta gösterilir. Gösterim temposu gerçek okuma hızı ölçümü değildir.</p>
        <p aria-live="polite">{{ rsvpSummary }}</p>
      }
      <p>Bu ayarlar yalnız bu önizlemeye uygulanır. Kayıtlı egzersiz ve ilerlemeniz değişmez.</p>
      @for (control of controls; track control.key) {
      <label [for]="'preview-' + control.key">{{ control.label }}</label>
      @if (control.options) {
      <select [id]="'preview-' + control.key" [(ngModel)]="values[control.key]">
        @for (option of control.options; track option.value) { <option [value]="option.value">{{ option.label }}</option> }
      </select>
      } @else {
      <input [id]="'preview-' + control.key" type="number" [min]="control.min" [max]="control.max" [step]="control.step ?? 1" [(ngModel)]="values[control.key]">
      <small>{{ control.min }}–{{ control.max }}</small>
      }
      }
      @if (error) { <p role="alert">{{ error }}</p> }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button type="button" (click)="reset()">Varsayılana dön</button>
      <button type="button" mat-dialog-close>İptal</button>
      <button type="button" class="primary" [disabled]="!controls.length" (click)="submit()">Denemeyi başlat</button>
    </mat-dialog-actions>
  `,
  styles: [`label{display:block;font-weight:600;margin:18px 0 6px}input,select{width:100%;padding:10px;border:1px solid #aebbc9;border-radius:8px;font:inherit;box-sizing:border-box}small{display:block;margin-top:5px;color:#52647b}p{line-height:1.6}button{padding:10px 14px;border:1px solid #dce2ef;border-radius:8px;cursor:pointer;background:transparent;font:inherit}.primary{background:var(--primary-blue,#1976d2);color:white}button:focus-visible,input:focus-visible,select:focus-visible{outline:3px solid #1976d2;outline-offset:2px}[role=alert]{color:#b42318}mat-dialog-actions{gap:8px;flex-wrap:wrap}`]
})
export class CustomPreviewDialogComponent {
  readonly configuration = inject<Record<string, unknown>>(MAT_DIALOG_DATA);
  get isRsvp(): boolean {
    const type = String(caseInsensitiveField(this.configuration, 'exerciseTypeName') ?? '').toLowerCase();
    const mode = String(caseInsensitiveField(recordOrEmpty(caseInsensitiveField(this.configuration, 'engineConfig')), 'mode')
      ?? caseInsensitiveField(this.configuration, 'mode') ?? '').toLowerCase();
    return type !== 'tachistoscope' && (type === 'rsvp' || mode === 'rsvp');
  }
  private readonly dialog = inject(MatDialogRef<CustomPreviewDialogComponent>);
  controls: PreviewControl[] = [];
  values: Record<string, number | string> = {};
  get speedWpm(): number { return this.values['speedWpm'] as number; }
  set speedWpm(value: number) { this.values['speedWpm'] = value; }
  get chunkSize(): number { return this.values['chunkSize'] as number; }
  set chunkSize(value: number) { this.values['chunkSize'] = value; }
  error = '';
  get rsvpSummary(): string {
    const duration = Number(this.values['displayDurationMs']);
    const gap = Number(this.values['intervalMs']);
    if (!Number.isFinite(duration) || !Number.isFinite(gap) || duration < 50 || duration > 5000 || gap < 0 || gap > 10000)
      return 'Süre ayarlarını kontrol edin.';
    const settings = recordOrEmpty(caseInsensitiveField(this.configuration, 'engineConfig'));
    const content = mergeCaseInsensitiveRecords(this.configuration, settings, 'content');
    const visuals = mergeCaseInsensitiveRecords(this.configuration, settings, 'visuals');
    const fixation = visuals['showfixation'] === false ? 0 : 300;
    const tempo = Math.round(60000 / (duration + gap));
    const minimum = Number(content['minwordcount']), maximum = Number(content['maxwordcount']);
    if (!(minimum > 0 && maximum >= minimum)) return `Akış temposu: ${tempo} kelime/dk. Toplam süre seçilen metnin uzunluğuna bağlıdır.`;
    const seconds = (count: number) => ((fixation + count * duration + (count - 1) * gap) / 1000)
      .toLocaleString('tr-TR', { maximumFractionDigits: 1 });
    return `Akış temposu: ${tempo} kelime/dk. Tahmini gösterim süresi: ${seconds(minimum)}–${seconds(maximum)} saniye (${minimum}–${maximum} kelime). Soru çözme süresi dahil değildir.`;
  }
  constructor() { this.reset(); }
  reset(): void {
    this.controls = getCustomPreviewControls(this.configuration);
    this.values = Object.fromEntries(this.controls.map(control => [control.key, control.value]));
    this.error = this.controls.length ? '' : 'Bu egzersizde özel ayarlar desteklenmiyor.';
  }
  submit(): void {
    if (!this.controls.length) return;
    try {
      const values = { ...this.values };
      applyCustomPreviewSettings(this.configuration, values, { roles: ['Teacher'], preview: true });
      this.dialog.close(values);
    } catch (error) { this.error = error instanceof Error ? error.message : 'Ayarları kontrol edin.'; }
  }
}
