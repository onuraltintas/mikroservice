import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { RouterLink } from '@angular/router';
import { siteForHost } from '../../../../../shared/eduivme-sites';
import { AUTH_PAGES } from './auth-page-content';

/**
 * Redesign 18.11.2026 (tuval: "Giriş: maxkoç", "Giriş: eduivme"): the frame of every sign-in and sign-up page.
 * The left side tells what the product does on this address; the page's own form goes inside.
 * The brand follows the address: maxkoç on the student and parent sites, eduivme elsewhere (localhost: student).
 */
@Component({
  selector: 'app-auth-shell',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './auth-shell.component.html',
  styleUrl: './auth-shell.component.scss'
})
export class AuthShellComponent {
  readonly page = AUTH_PAGES[siteForHost(inject(DOCUMENT).location?.hostname) ?? 'student'];
  /** A sign-up form needs more room than the sign-in form. */
  readonly wide = input(false);
  /** Sign-up pages tell what the chosen account gets instead of the address's sample. */
  readonly headline = input<string | null>(null);
  readonly sub = input<string | null>(null);
  readonly points = input<readonly string[] | null>(null);
  readonly featuresTestId = input('login-features');
}
