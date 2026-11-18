import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, input, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Success screen for an account whose e-mail address is already verified: sign in now. */
@Component({
    selector: 'app-registration-success',
    standalone: true,
    imports: [RouterLink],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
        <section role="status" aria-live="polite" class="success">
            <h3 #heading tabindex="-1" class="success__title focus-visible:outline-2">{{ title() }}</h3>
            <p class="success__message">{{ message() }}</p>
            <a routerLink="/auth/login" [queryParams]="loginQueryParams()" class="ds-btn ds-btn--primary ds-btn--large ds-btn--block">Giriş yap</a>
        </section>
    `,
  styles: [`
        .success { display: flex; flex-direction: column; gap: 14px; padding: 20px; border: 1px solid currentColor; border-radius: var(--ds-radius);
            background: var(--ds-success-soft); color: var(--ds-success); }
        .success__title { margin: 0; color: var(--ds-text); font-size: 20px; font-weight: 700; border-radius: 4px; }
        .success__title:focus { outline: 3px solid var(--ds-focus, currentColor); outline-offset: 4px; }
        .success__message { margin: 0; color: var(--ds-text); font-size: 15px; line-height: 1.6; }
    `]
})
export class RegistrationSuccessComponent {
    readonly title = input.required<string>();
    readonly message = input.required<string>();
    readonly loginQueryParams = input<Record<string, string>>({});
    private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');

    constructor() {
        // The form this panel replaces is hidden; move focus here instead of letting it fall to <body>.
        afterNextRender(() => this.heading().nativeElement.focus());
    }
}
