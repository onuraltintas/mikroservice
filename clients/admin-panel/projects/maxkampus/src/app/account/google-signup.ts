import { Injectable } from '@angular/core';

const KEY = 'maxkampus.google-signup';
const INVITE_KEY = 'maxkampus.invite';

/**
 * A new Google account's registration token between the Google step and the form (5 minutes, Identity), and the
 * invitation a teacher came with, so a page reload or the e-mail verification does not lose them.
 */
@Injectable({ providedIn: 'root' })
export class GoogleSignup {
  start(token: string): void { this.write(KEY, token); }
  token(): string | null { return this.read(KEY); }
  clear(): void { this.write(KEY, null); }

  /** Kept across tabs: the e-mail verification link opens a new one. */
  rememberInvite(code: string | null): void {
    try {
      if (code) globalThis.localStorage?.setItem(INVITE_KEY, code);
      else globalThis.localStorage?.removeItem(INVITE_KEY);
    } catch { /* the invitation link still works when opened again */ }
  }
  invite(): string | null {
    try { return globalThis.localStorage?.getItem(INVITE_KEY) ?? null; } catch { return null; }
  }

  private read(key: string): string | null {
    try { return globalThis.sessionStorage?.getItem(key) ?? null; } catch { return null; }
  }
  private write(key: string, value: string | null): void {
    try {
      if (value) globalThis.sessionStorage?.setItem(key, value);
      else globalThis.sessionStorage?.removeItem(key);
    } catch { /* private mode: the flow still works within the page */ }
  }
}
