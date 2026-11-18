import { Component, EventEmitter, Input, Output } from '@angular/core';
import { RegistrationLegalConsentComponent } from './registration-legal-consent.component';
import { RegistrationLegalAcceptance } from '../../core/models/user.model';

@Component({
  selector: 'app-google-registration-consent',
  standalone: true,
  imports: [RegistrationLegalConsentComponent],
  template: `
    <section role="region" aria-label="Google kaydını tamamla" class="consent">
      <div class="ds-auth__heading">
        <h2 class="ds-auth__title">Kaydını tamamla</h2>
        <p class="ds-auth__lead">Google hesabın doğrulandı. Hesabını oluşturmak için aşağıdaki metinleri inceleyip onayla.</p>
      </div>
      <app-registration-legal-consent [product]="product"
        (readinessChange)="ready = $event" (acceptancesChange)="acceptances = $event" />
      @if (error) { <p role="alert" class="ds-alert">{{ error }}</p> }
      <div class="consent__actions">
        <button type="button" class="ds-btn ds-btn--primary ds-btn--large" [disabled]="busy || !ready || acceptances.length !== 3"
          (click)="confirm.emit(acceptances)">
          {{ busy ? 'Hesabın oluşturuluyor…' : 'Onayla ve hesabımı oluştur' }}
        </button>
        <button type="button" class="ds-btn ds-btn--secondary ds-btn--large" [disabled]="busy" (click)="cancel.emit()">Vazgeç</button>
      </div>
      <p class="ds-field__hint">Bu işlem 5 dakika içinde tamamlanmalı. Vazgeçersen hesap oluşturulmaz.</p>
    </section>
  `,
  styles: [`
    .consent { display: flex; flex-direction: column; gap: 18px; }
    .consent__actions { display: flex; flex-wrap: wrap; gap: 12px; }
    .consent__actions .ds-btn--primary { flex: 1 1 220px; }
    .ds-alert, .ds-field__hint { margin: 0; }
  `]
})
export class GoogleRegistrationConsentComponent {
  @Input() product: 'coaching' | 'speed-reading' = 'speed-reading';
  @Input() busy = false;
  @Input() error = '';
  @Output() confirm = new EventEmitter<RegistrationLegalAcceptance[]>();
  @Output() cancel = new EventEmitter<void>();
  ready = false;
  acceptances: RegistrationLegalAcceptance[] = [];
}
