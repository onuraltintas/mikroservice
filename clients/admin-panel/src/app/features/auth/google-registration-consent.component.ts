import { Component, EventEmitter, Input, Output } from '@angular/core';
import { RegistrationLegalConsentComponent, RegistrationProduct } from './registration-legal-consent.component';
import { RegistrationLegalAcceptance } from '../../core/services/identity.service';
import { injectAuthCopy } from './auth-voice';

@Component({
  selector: 'app-google-registration-consent',
  standalone: true,
  imports: [RegistrationLegalConsentComponent],
  template: `
    <section role="region" [attr.aria-label]="copy.googleConsentLabel" class="consent">
      <div class="ds-auth__heading">
        <h2 class="ds-auth__title">{{ copy.googleConsentTitle }}</h2>
        <p class="ds-auth__lead">{{ copy.googleConsentIntro }}</p>
      </div>
      <app-registration-legal-consent [product]="product"
        (readinessChange)="ready = $event" (acceptancesChange)="acceptances = $event" />
      @if (error) { <p role="alert" class="ds-alert">{{ error }}</p> }
      <div class="consent__actions">
        <button type="button" class="ds-btn ds-btn--primary ds-btn--large" [disabled]="busy || !ready || acceptances.length !== 3"
          (click)="confirm.emit(acceptances)">
          {{ busy ? copy.googleConsentBusy : 'Onayla ve hesabımı oluştur' }}
        </button>
        <button type="button" class="ds-btn ds-btn--secondary ds-btn--large" [disabled]="busy" (click)="cancel.emit()">Vazgeç</button>
      </div>
      <p class="ds-field__hint">{{ copy.googleConsentTimeLimit }}</p>
    </section>
  `,
  styles: [`
    .consent { display: flex; flex-direction: column; gap: 18px; }
    .consent__actions { display: flex; flex-wrap: wrap; gap: 12px; }
    .consent__actions .ds-btn--primary { flex: 1 1 220px; }
    .ds-alert { margin: 0; }
    .ds-field__hint { margin: 0; }
  `]
})
export class GoogleRegistrationConsentComponent {
  @Input() product: RegistrationProduct = 'coaching';
  @Input() busy = false;
  @Input() error = '';
  @Output() confirm = new EventEmitter<RegistrationLegalAcceptance[]>();
  @Output() cancel = new EventEmitter<void>();
  /** PR #124 review L7: the login page renders this on eduivme.com too, so it follows the host's voice. */
  readonly copy = injectAuthCopy().copy;
  ready = false;
  acceptances: RegistrationLegalAcceptance[] = [];
}

