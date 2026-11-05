import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Success screen for an account whose e-mail address is already verified: sign in now. */
@Component({
    selector: 'app-registration-success',
    standalone: true,
    imports: [RouterLink],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
        <section role="status" aria-live="polite"
            class="flex flex-col gap-4 rounded-2xl border border-emerald-200 bg-emerald-50 p-6 text-emerald-900 dark:border-emerald-900/40 dark:bg-emerald-900/20 dark:text-emerald-100">
            <h3 class="text-xl font-bold">{{ title() }}</h3>
            <p class="leading-relaxed">{{ message() }}</p>
            <a routerLink="/auth/login" [queryParams]="loginQueryParams()"
                class="inline-flex items-center justify-center rounded-xl bg-blue-600 px-6 py-3 font-bold text-white hover:bg-blue-700">Giriş yap</a>
        </section>
    `
})
export class RegistrationSuccessComponent {
    readonly title = input.required<string>();
    readonly message = input.required<string>();
    readonly loginQueryParams = input<Record<string, string>>({});
}
