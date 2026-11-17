import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Live test 16.11.2026 (#14/#48), user decision 17.11.2026: no erasure button; the student and the parent are told where to
 * ask for it. The address is the data controller's in the published KVKK text, so it is not written here a second time.
 */
@Component({
  selector: 'app-account-erasure-note',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section aria-labelledby="account-erasure-title" data-testid="account-erasure-note">
    <h2 id="account-erasure-title">Hesabımın silinmesi</h2>
    <p>Hesabının ve bilgilerinin silinmesini istersen, <a routerLink="/legal/kvkk">KVKK aydınlatma metnindeki</a> veri sorumlusu iletişim adresine yazabilirsin.</p>
  </section>`
})
export class AccountErasureNoteComponent {}
