import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CoachingChurnRow, CoachingLicenseRow, CoachingManagementService } from '../../../core/services/coaching-management.service';

const AUDIENCES: Record<string, string> = { Individual: 'Bireysel', Teacher: 'Öğretmen', Institution: 'Kurum' };

/** Turkey calendar day of an instant, as yyyy-MM-dd. */
function turkeyDay(value: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(value));
}

/** Roadmap D6: edit a license's end date and (institution) seat capacity. */
@Component({
  selector: 'app-coaching-license-edit', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <form class="license-edit" (ngSubmit)="save()" [attr.aria-label]="'Lisansı düzenle: ' + title">
      <label>Bitiş günü<input type="date" name="endDay" [(ngModel)]="endDay" required data-testid="license-end"></label>
      @if (seatsEditable) {
        <label>Koltuk sayısı<input type="number" name="seats" min="1" max="100000" [(ngModel)]="seats" data-testid="license-seats"></label>
      }
      <button type="submit" class="primary" [disabled]="saving()">{{ saving() ? 'Kaydediliyor…' : 'Lisansı güncelle' }}</button>
      <button type="button" class="secondary" (click)="cancelled.emit()">Vazgeç</button>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <p class="muted">Lisans seçilen günün sonunda (Türkiye saati) biter; son ödenen dönem de birlikte değişir.{{ seatsEditable ? ' Koltuk sayısı plandaki koltuklardan ve kullanımdaki koltuklardan az olamaz.' : '' }}</p>
    </form>`,
  styles: [`.license-edit{display:flex;flex-wrap:wrap;gap:.5rem .75rem;align-items:flex-end;padding:.5rem 0}
    label{display:flex;flex-direction:column;gap:.25rem;font-size:.85rem} .muted{flex-basis:100%;margin:0} .error{flex-basis:100%;margin:0;color:#b91c1c}`]
})
export class CoachingLicenseEditComponent implements OnChanges {
  private readonly service = inject(CoachingManagementService);
  @Input({ required: true }) licenseId!: string;
  @Input({ required: true }) endDate!: string;
  @Input() seatCapacity: number | null = null;
  @Input() seatsEditable = false;
  @Input() title = '';
  @Output() readonly saved = new EventEmitter<CoachingLicenseRow>();
  @Output() readonly cancelled = new EventEmitter<void>();
  endDay = ''; seats: number | null = null;
  readonly saving = signal(false); readonly error = signal('');

  ngOnChanges(): void { this.endDay = turkeyDay(this.endDate); this.seats = this.seatCapacity; this.error.set(''); }

  async save(): Promise<void> {
    if (!this.endDay) { this.error.set('Bitiş günü seçin.'); return; }
    const change: { endDate?: string; seatCapacity?: number } = {};
    if (this.endDay !== turkeyDay(this.endDate)) change.endDate = `${this.endDay}T23:59:00`;
    if (this.seatsEditable && this.seats !== null && this.seats !== this.seatCapacity) change.seatCapacity = Number(this.seats);
    if (!Object.keys(change).length) { this.cancelled.emit(); return; }
    this.saving.set(true); this.error.set('');
    try { this.saved.emit(await firstValueFrom(this.service.updateLicense(this.licenseId, change))); }
    catch (error) { this.error.set(error instanceof HttpErrorResponse && typeof error.error?.message === 'string' ? error.error.message : 'Lisans güncellenemedi.'); }
    finally { this.saving.set(false); }
  }
}

/** Roadmap D6: ending and ended licenses, Excel payments export and the churn report. */
@Component({
  selector: 'app-coaching-license-tools', standalone: true, imports: [CommonModule, FormsModule, CoachingLicenseEditComponent],
  template: `
    <section class="space-y-4" aria-labelledby="license-tools-heading">
      <div><h2 id="license-tools-heading" class="text-lg font-semibold">Lisans takibi</h2><p class="muted">30 gün içinde bitecek ve son 30 günde biten lisanslar, ödemelerin Excel dökümü ve yenilememe oranı.</p></div>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @for (list of lists; track list.window) {
        <div class="data-card overflow-x-auto">
          <h3 class="font-semibold">{{ list.title }}</h3>
          <table class="data-table" [attr.data-testid]="'licenses-' + list.window"><thead><tr><th>Sahip</th><th>Plan</th><th>Bitiş</th><th>Kalan gün</th><th>Koltuk</th><th></th></tr></thead><tbody>
            @for (row of rows()[list.window]; track row.id) {
              <tr><td>{{ row.ownerName || row.ownerEmail || row.institutionId || row.userId }}<div class="muted">{{ audience(row.audience) }}</div></td><td>{{ row.planName }}</td>
                <td>{{ row.endDate | date:'dd.MM.yyyy' }}</td><td [class.warn]="row.daysLeft <= 7">{{ row.daysLeft }}</td>
                <td>{{ row.seatCapacity === null ? '—' : row.assignedSeats + ' / ' + row.seatCapacity }}</td>
                <td><button type="button" class="secondary" (click)="editing.set(editing() === row.id ? null : row.id)">Düzenle</button></td></tr>
              @if (editing() === row.id) {
                <tr><td colspan="6"><app-coaching-license-edit [licenseId]="row.id" [endDate]="row.endDate" [seatCapacity]="row.seatCapacity" [seatsEditable]="row.audience === 'Institution'" [title]="row.ownerName || row.planName" (saved)="afterSave()" (cancelled)="editing.set(null)" /></td></tr>
              }
            } @empty { <tr><td colspan="6" class="empty">Kayıt yok.</td></tr> }
          </tbody></table>
        </div>
      }
      <form class="data-card export" (ngSubmit)="exportPayments()" aria-label="Ödemeleri Excel olarak indir">
        <h3 class="font-semibold">Ödemeler (Excel)</h3>
        <label>Başlangıç<input type="date" name="exportFrom" [(ngModel)]="exportFrom"></label>
        <label>Bitiş<input type="date" name="exportTo" [(ngModel)]="exportTo"></label>
        <button type="submit" class="primary" [disabled]="exporting()" data-testid="export-payments">{{ exporting() ? 'Hazırlanıyor…' : 'Excel indir' }}</button>
      </form>
      <div class="data-card overflow-x-auto">
        <h3 class="font-semibold">Yenilememe oranı (son 12 ay)</h3>
        <p class="muted">Ay içinde biten ödenmiş dönemlerden 30 gün içinde yenilenmeyenlerin oranı. Deneme süreleri sayılmaz; 30 günü dolmamış dönemler "bekliyor" sayılır.</p>
        <table class="data-table" data-testid="churn"><thead><tr><th>Ay</th><th>Tür</th><th>Biten</th><th>Yenilenen</th><th>Yenilenmeyen</th><th>Bekliyor</th><th>Oran</th></tr></thead><tbody>
          @for (row of churn(); track row.month + row.audience) {
            @if (row.ended) { <tr><td>{{ row.month }}</td><td>{{ audience(row.audience) }}</td><td>{{ row.ended }}</td><td>{{ row.renewed }}</td><td>{{ row.notRenewed }}</td><td>{{ row.pending }}</td><td>{{ row.churnRate === null ? '—' : '%' + row.churnRate }}</td></tr> }
          }
          @if (!hasChurn()) { <tr><td colspan="7" class="empty">Son 12 ayda biten ödenmiş dönem yok.</td></tr> }
        </tbody></table>
      </div>
    </section>`,
  styles: [`.data-card{border:1px solid var(--ui-border);border-radius:.75rem;background:var(--ui-surface);padding:1rem}
    .data-table{width:100%;text-align:left;font-size:.875rem}.data-table th,.data-table td{border-bottom:1px solid var(--ui-border);padding:.5rem .75rem;vertical-align:top}
    .muted{color:var(--ui-text-muted);font-size:.8rem}.empty{padding:1.5rem;text-align:center;color:var(--ui-text-muted)}.warn{color:#b45309;font-weight:700}.error{color:#b91c1c}
    .export{display:flex;flex-wrap:wrap;gap:.5rem .75rem;align-items:flex-end}.export h3{flex-basis:100%}.export label{display:flex;flex-direction:column;gap:.25rem;font-size:.85rem}
    input{border:1px solid var(--ui-border-strong);border-radius:.5rem;background:transparent;padding:.4rem .6rem;color:inherit}
    .primary,.secondary{border-radius:.5rem;padding:.45rem .75rem;font-size:.85rem}.primary{background:var(--ui-brand);color:var(--ui-brand-contrast)}.secondary{border:1px solid var(--ui-border-strong)}`]
})
export class CoachingLicenseToolsComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  readonly lists = [{ window: 'ending' as const, title: '30 gün içinde bitecek lisanslar' }, { window: 'ended' as const, title: 'Son 30 günde biten lisanslar' }];
  readonly rows = signal<Record<'ending' | 'ended', CoachingLicenseRow[]>>({ ending: [], ended: [] });
  readonly churn = signal<CoachingChurnRow[]>([]);
  readonly editing = signal<string | null>(null);
  readonly error = signal('');
  readonly exporting = signal(false);
  exportFrom = ''; exportTo = '';

  ngOnInit(): void { this.load(); }

  audience(value: string): string { return AUDIENCES[value] ?? value; }
  hasChurn(): boolean { return this.churn().some(row => row.ended > 0); }

  load(): void {
    for (const list of this.lists)
      this.service.licenseWindow(list.window).subscribe({
        next: items => this.rows.update(current => ({ ...current, [list.window]: items })),
        error: () => this.error.set('Lisans listeleri yüklenemedi.')
      });
    this.service.churn(12).subscribe({ next: rows => this.churn.set(rows), error: () => this.error.set('Yenilememe raporu yüklenemedi.') });
  }

  afterSave(): void { this.editing.set(null); this.load(); }

  async exportPayments(): Promise<void> {
    if (this.exportFrom && this.exportTo && this.exportTo < this.exportFrom) { this.error.set('Başlangıç tarihi bitişten sonra olamaz.'); return; }
    this.exporting.set(true); this.error.set('');
    try {
      const blob = await firstValueFrom(this.service.exportPayments(this.exportFrom, this.exportTo));
      const url = URL.createObjectURL(blob); const link = document.createElement('a');
      link.href = url; link.download = `kocluk-odemeleri-${this.exportFrom || 'tum'}-${this.exportTo || 'bugun'}.xlsx`;
      link.click(); setTimeout(() => URL.revokeObjectURL(url), 0);
    } catch { this.error.set('Ödeme dökümü indirilemedi.'); }
    finally { this.exporting.set(false); }
  }
}
