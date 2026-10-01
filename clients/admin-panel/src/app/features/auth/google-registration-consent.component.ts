import { Component, EventEmitter, Input, Output } from '@angular/core';
import { RegistrationLegalConsentComponent, RegistrationProduct } from './registration-legal-consent.component';
import { RegistrationLegalAcceptance } from '../../core/services/identity.service';

@Component({
  selector: 'app-google-registration-consent',
  standalone: true,
  imports: [RegistrationLegalConsentComponent],
  template: `
    <section role="region" aria-label="Google kaydını tamamlayın" class="rounded-xl border border-indigo-200 bg-white p-6 dark:bg-slate-900">
      <h2 class="text-xl font-semibold">Kaydınızı tamamlayın</h2>
      <p class="my-3">Google hesabınız doğrulandı. Hesabınızı oluşturmak için aşağıdaki metinleri inceleyip onaylayın.</p>
      <app-registration-legal-consent [product]="product"
        (readinessChange)="ready = $event" (acceptancesChange)="acceptances = $event" />
      @if (error) { <p role="alert" class="my-3 text-red-600">{{ error }}</p> }
      <div class="mt-4 flex gap-3">
        <button type="button" [disabled]="busy || !ready || acceptances.length !== 3"
          (click)="confirm.emit(acceptances)" class="rounded-lg bg-indigo-600 px-4 py-3 text-white disabled:opacity-50">
          {{ busy ? 'Hesabınız oluşturuluyor…' : 'Onayla ve hesabımı oluştur' }}
        </button>
        <button type="button" [disabled]="busy" (click)="cancel.emit()" class="rounded-lg border px-4 py-3">Vazgeç</button>
      </div>
      <p class="mt-3 text-sm">Bu işlem 5 dakika içinde tamamlanmalıdır. Vazgeçerseniz hesap oluşturulmaz.</p>
    </section>
  `
})
export class GoogleRegistrationConsentComponent {
  @Input() product: RegistrationProduct = 'coaching';
  @Input() busy = false;
  @Input() error = '';
  @Output() confirm = new EventEmitter<RegistrationLegalAcceptance[]>();
  @Output() cancel = new EventEmitter<void>();
  ready = false;
  acceptances: RegistrationLegalAcceptance[] = [];
}

