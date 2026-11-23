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

  // Canlı giriş 23.11.2026: örnek kartların uydurma olmayan, açıkça örnek veri olduğu görülen içerikleri.
  readonly week = [
    { label: 'Pt', value: 60 }, { label: 'Sa', value: 85 }, { label: 'Ça', value: 40 }, { label: 'Pe', value: 95 },
    { label: 'Cu', value: 70 }, { label: 'Ct', value: 50 }, { label: 'Pz', value: 20 }
  ] as const;
  readonly roster = [{ name: 'Ece', done: 86 }, { name: 'Mert', done: 64 }, { name: 'Zeynep', done: 31 }] as const;
  readonly report = [
    { label: 'Tem', net: 52 }, { label: 'Ağu', net: 58 }, { label: 'Eyl', net: 61 }, { label: 'Eki', net: 66 }, { label: 'Kas', net: 72 }
  ] as const;

  /** Maddeler ve kişiler sırayla dört tondan birini alır (ds-tone-1…4). */
  tone(index: number): string { return `ds-tone-${(index % 4) + 1}`; }
}
