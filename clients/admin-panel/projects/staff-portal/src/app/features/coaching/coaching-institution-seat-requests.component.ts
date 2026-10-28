import { DatePipe } from '@angular/common';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { coachingErrorMessage, coachingMoney } from '../../../../../../shared/coaching-sales.api';
import { CoachingInstitutionService, CoachingInstitutionSubscription, CoachingSeatQuote, CoachingSeatRequest } from './coaching-institution.service';

/**
 * Roadmap O10b: the institution buys extra seats for the rest of its license. It sees the prorated price and the bank
 * details, pays by transfer and sends the reference; the platform admin approves and the capacity grows. Extra seats
 * end with the license and are not carried into a renewal.
 */
@Component({
  selector: 'staff-coaching-institution-seat-requests', standalone: true, imports: [DatePipe, FormsModule],
  template: `
    <section class="seat-requests" aria-labelledby="seat-requests-title">
      <h4 id="seat-requests-title">Ek koltuk</h4>
      <p class="muted">Lisansınızın kalan süresi için koltuk başına {{ money(subscription.extraSeatPrice) }} TRY (yıllık) üzerinden, kalan güne oranla ücretlendirilir.
        Ek koltuklar bu lisansla biter; yenilemeye taşınmaz.</p>
      @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
      @if (success()) { <p role="status" class="ok">{{ success() }}</p> }

      @if (pending(); as open) {
        <p class="pending" data-testid="seat-request-pending">{{ open.seats }} ek koltuk talebiniz ({{ money(open.amount) }} {{ open.currency }}, referans {{ open.paymentReference }}) inceleniyor.
          <button type="button" data-testid="seat-request-withdraw" [disabled]="busy()" (click)="withdraw(open)">Talebi geri çek</button></p>
      } @else {
        <form class="row" (ngSubmit)="calculate()">
          <label>Ek koltuk sayısı<input type="number" name="extraSeats" data-testid="seat-count" [(ngModel)]="seats" min="1" max="1000" required /></label>
          <button type="submit" [disabled]="busy()">Ücreti hesapla</button>
        </form>
        @if (quote(); as q) {
          <div class="quote" data-testid="seat-quote">
            <p><strong>{{ q.seats }} koltuk · {{ money(q.amount) }} {{ q.currency }}</strong>
              <span class="muted">({{ money(q.unitPrice) }} × {{ q.seats }} × {{ q.remainingDays }} / {{ q.durationDays }} gün)</span></p>
            @if (q.iban) {
              <p>Havale / EFT: <strong>{{ q.accountHolder }}</strong> · {{ q.bankName }} · <span class="iban">{{ q.iban }}</span></p>
              @if (q.paymentInstructions) { <p class="muted">{{ q.paymentInstructions }}</p> }
              <form class="grid" (ngSubmit)="send()">
                <label>Banka işlem referansı<input name="seatReference" [(ngModel)]="reference" maxlength="120" required autocomplete="off" /></label>
                <label>Not (isteğe bağlı)<input name="seatNote" [(ngModel)]="note" maxlength="1000" /></label>
                <button type="submit" data-testid="seat-request-send" [disabled]="busy() || !reference.trim()">Ödemeyi yaptım, talep gönder</button>
              </form>
            } @else { <p class="muted">Banka bilgileri şu anda yayımlanmıyor; ek koltuk için platform yöneticisiyle iletişime geçin.</p> }
          </div>
        }
      }

      @if (requests().length) {
        <ul>
          @for (item of requests(); track item.id) {
            <li [attr.data-testid]="'seat-request-' + item.id"><span>{{ item.seats }} ek koltuk · {{ money(item.amount) }} {{ item.currency }} · {{ item.createdAt | date:'dd.MM.yyyy' }}</span>
              <span>{{ statusLabel(item.status) }}@if (item.reviewNote) { <small>{{ item.reviewNote }}</small> }</span></li>
          }
        </ul>
      }
    </section>`,
  styles: [`
    .seat-requests { margin-top:1rem; padding-top:.8rem; border-top:1px solid #e8ece5; }
    h4 { margin:0 0 .4rem; } .muted, small { color:#687872; font-size:.85rem; } small { display:block; }
    .row, .grid { display:flex; flex-wrap:wrap; gap:.6rem; align-items:flex-end; margin:.5rem 0; }
    label { display:grid; gap:.25rem; font-size:.85rem; }
    input { border:1px solid #c9d2c5; border-radius:.45rem; padding:.45rem .6rem; font:inherit; }
    input[type=number] { width:7rem; }
    button { min-height:2.4rem; border:1px solid #9aa99b; border-radius:.5rem; background:#fffefa; padding:.35rem .7rem; color:#29473b; cursor:pointer; }
    button:disabled { opacity:.55; cursor:not-allowed; }
    .quote, .pending { padding:.6rem .75rem; border-radius:.6rem; background:#f1f5ee; }
    .iban { letter-spacing:.03em; } .error { color:#991b1b; } .ok { color:#166534; }
    ul { list-style:none; padding:0; } li { display:flex; justify-content:space-between; gap:.75rem; flex-wrap:wrap; padding:.5rem 0; border-top:1px solid #e8ece5; }
  `]
})
export class CoachingInstitutionSeatRequestsComponent implements OnInit {
  @Input({ required: true }) subscription!: CoachingInstitutionSubscription;
  private readonly service = inject(CoachingInstitutionService);
  readonly money = coachingMoney;
  readonly requests = signal<CoachingSeatRequest[]>([]);
  readonly quote = signal<CoachingSeatQuote | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  seats = 1;
  reference = '';
  note = '';

  ngOnInit(): void { void this.loadRequests(); }

  pending(): CoachingSeatRequest | null {
    return this.requests().find(item => item.status === 'Pending' && item.subscriptionId === this.subscription.subscriptionId) ?? null;
  }

  statusLabel(status: CoachingSeatRequest['status']): string {
    return ({ Pending: 'İnceleniyor', Approved: 'Onaylandı, koltuklar eklendi', Rejected: 'Reddedildi', Withdrawn: 'Geri çekildi' } as const)[status] ?? status;
  }

  async calculate(): Promise<void> {
    const seats = Math.trunc(Number(this.seats));
    if (!(seats >= 1 && seats <= 1000)) { this.error.set('Ek koltuk sayısı 1–1000 arasında olmalıdır.'); return; }
    await this.run(async () => { this.quote.set(await firstValueFrom(this.service.getSeatQuote(this.subscription.subscriptionId, seats))); return null; },
      'Ücret hesaplanamadı.');
  }

  async send(): Promise<void> {
    const quote = this.quote();
    if (!quote || !this.reference.trim()) return;
    await this.run(async () => {
      const created = await firstValueFrom(this.service.createSeatRequest({ subscriptionId: quote.subscriptionId, seats: quote.seats, paymentReference: this.reference, note: this.note }));
      this.requests.update(items => [created, ...items]);
      this.quote.set(null); this.reference = ''; this.note = ''; this.seats = 1;
      return 'Ek koltuk talebiniz alındı. Ödeme doğrulanınca koltuklar lisansınıza eklenir.';
    }, 'Ek koltuk talebi gönderilemedi.');
  }

  async withdraw(request: CoachingSeatRequest): Promise<void> {
    await this.run(async () => {
      const updated = await firstValueFrom(this.service.withdrawSeatRequest(request.id));
      this.requests.update(items => items.map(item => item.id === updated.id ? updated : item));
      return 'Ek koltuk talebiniz geri çekildi.';
    }, 'Talep geri çekilemedi.');
  }

  private async loadRequests(): Promise<void> {
    try { this.requests.set(await firstValueFrom(this.service.getSeatRequests())); } catch { this.error.set('Ek koltuk talepleri yüklenemedi.'); }
  }

  private async run(action: () => Promise<string | null>, fallback: string): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(null); this.success.set(null);
    try { const message = await action(); if (message) this.success.set(message); }
    catch (response) { this.error.set(coachingErrorMessage(response, fallback)); }
    finally { this.busy.set(false); }
  }
}
