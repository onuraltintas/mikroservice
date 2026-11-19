import { Component, inject, output } from '@angular/core';
import { GoogleSigninButtonModule, SocialAuthService } from '@abacritt/angularx-social-login';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

/** ID tokens already handed over: the library replays its last sign-in to every new subscriber. */
const used = new Set<string>();

/** Google's own "Continue with Google" button (no One Tap). Emits the ID token Identity checks, once. */
@Component({
  selector: 'mk-google-button',
  standalone: true,
  imports: [GoogleSigninButtonModule],
  template: `<div class="google"><asl-google-signin-button type="standard" size="large" text="continue_with" shape="rectangular" logo_alignment="center" [width]="320" /></div>`,
  styles: `.google { display: flex; justify-content: center; min-height: 44px; }`
})
export class GoogleButtonComponent {
  readonly token = output<string>();
  constructor() {
    inject(SocialAuthService).authState.pipe(takeUntilDestroyed()).subscribe(user => {
      if (!user?.idToken || used.has(user.idToken)) return;
      used.add(user.idToken);
      this.token.emit(user.idToken);
    });
  }
}
