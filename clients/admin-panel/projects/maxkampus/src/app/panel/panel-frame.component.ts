import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { MaxkampusApi, MyBilling } from '../core/api';
import { MaxkampusSession } from '../core/session.service';
import { BrandMarkComponent } from '../site/brand-mark.component';

/** The signed-in frame of maxkampus.com: brand, the owner's plan page, sign-out. */
@Component({
  selector: 'mk-panel-frame',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, BrandMarkComponent],
  template: `
    <header class="bar">
      <a routerLink="/panel" class="brand" aria-label="maxkampüs paneli"><mk-brand-mark [size]="30" /><strong>maxkampüs</strong></a>
      <nav aria-label="Hesap">
        <a routerLink="/panel" routerLinkActive="on" [routerLinkActiveOptions]="{ exact: true }" ariaCurrentWhenActive="page">Panel</a>
        @if (session.role() === 'owner') { <a routerLink="/plan" routerLinkActive="on" ariaCurrentWhenActive="page">Planım ve ödemeler</a> }
        <a routerLink="/iletisim">Destek</a>
        <button type="button" (click)="signOut()">Çıkış yap</button>
      </nav>
    </header>
    @if (switched(); as where) {
      <p class="switched" role="status">
        @if (where === 'here') {
          <span>Bu tarayıcıda şimdi <strong>{{ session.displayName() }}</strong> ({{ roleName() }}) hesabıyla girdiniz; önceki hesabın oturumu kapandı.
            İki hesabı aynı anda kullanmak için birini gizli pencerede açın.</span>
        } @else {
          <span>Bu tarayıcının başka bir sekmesinde başka bir maxkampüs hesabıyla giriş yapıldı. Sayfayı yenilediğinizde o hesap açılır.</span>
        }
        <button type="button" (click)="switched.set(null)" aria-label="Bilgiyi kapat">Tamam</button>
      </p>
    }
    @if (planEnding(); as ending) {
      <p class="ending" [class.urgent]="ending.days <= 1" role="status">
        <span>{{ ending.days <= 0 ? 'Planınız bugün sona eriyor.' : ending.days === 1 ? 'Planınız yarın sona eriyor.' : 'Planınız ' + ending.date + ' tarihinde sona eriyor (' + ending.days + ' gün kaldı).' }}
          Yenilemezseniz panel salt okunur olur; kayıtlarınız durur.</span>
        <a routerLink="/plan">Planı yenile</a>
      </p>
    }
    <ng-content />
  `,
  styles: `
    :host { display: block; min-height: 100dvh; background: var(--ds-bg); }
    .bar { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; padding: 10px 20px; background: var(--ds-surface); border-bottom: 1px solid var(--ds-line); }
    .brand { display: inline-flex; align-items: center; gap: 10px; color: var(--ds-text); text-decoration: none; font-size: 17px; }
    nav { display: flex; flex-wrap: wrap; gap: 4px; }
    nav a, nav button { display: inline-flex; align-items: center; min-height: 44px; padding: 0 12px; border: 0; border-radius: 8px; background: transparent; color: var(--ds-text); font: inherit; text-decoration: none; cursor: pointer; }
    nav a:hover, nav button:hover { background: var(--ds-soft); }
    nav a.on { font-weight: 600; box-shadow: inset 0 -3px 0 var(--ds-accent); }
    .switched { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; margin: 0; padding: 10px 20px;
      background: var(--ds-soft); color: var(--ds-text); border-bottom: 1px solid var(--ds-line); line-height: 1.5; }
    .ending { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; margin: 0; padding: 10px 20px;
      background: var(--mk-soft); color: var(--mk-on-soft); border-bottom: 1px solid var(--ds-line); line-height: 1.5; }
    .ending.urgent { background: var(--ds-warning-soft); color: var(--ds-text); font-weight: 500; }
    .ending a { display: inline-flex; align-items: center; min-height: 40px; padding: 0 14px; border-radius: 8px; background: var(--ds-text); color: var(--ds-surface); text-decoration: none; font-weight: 600; }
    .switched button { min-height: 40px; padding: 0 14px; border: 1px solid var(--ds-field); border-radius: 8px; background: var(--ds-surface); color: var(--ds-text); font: inherit; cursor: pointer; }
  `
})
export class PanelFrameComponent implements OnInit {
  readonly session = inject(MaxkampusSession);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  /**
   * One maxkampüs session per browser: signing in with another account (a teacher's, say) replaces the owner's. The panel
   * says so instead of quietly showing another account's screens (20.11.2026 live test).
   */
  readonly switched = signal<'here' | 'elsewhere' | null>(null);
  /** The owner sees when the plan ends within 14 days (20.11.2026: only e-mails said so). A renewal already paid counts. */
  readonly planEnding = signal<{ days: number; date: string } | null>(null);
  private readonly api = inject(MaxkampusApi);

  ngOnInit(): void {
    if (this.session.role() === 'owner') void this.api.myBilling().then(billing => this.planEnding.set(planEndingSoon(billing, Date.now()))).catch(() => undefined);
    const current = this.session.userId();
    const before = readLastAccount();
    if (current && before && before !== current) this.switched.set('here');
    if (current) writeLastAccount(current);
    const onStorage = (event: StorageEvent) => {
      if (event.key === LAST_ACCOUNT_KEY && event.newValue && event.newValue !== this.session.userId()) this.switched.set('elsewhere');
    };
    globalThis.addEventListener?.('storage', onStorage);
    this.destroyRef.onDestroy(() => globalThis.removeEventListener?.('storage', onStorage));
  }

  roleName(): string { return this.session.role() === 'owner' ? 'kurum sahibi' : 'öğretmen'; }

  async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigateByUrl('/');
  }
}

const LAST_ACCOUNT_KEY = 'mk_last_account';
function readLastAccount(): string | null { try { return globalThis.localStorage?.getItem(LAST_ACCOUNT_KEY) ?? null; } catch { return null; } }
function writeLastAccount(userId: string): void { try { globalThis.localStorage?.setItem(LAST_ACCOUNT_KEY, userId); } catch { /* private mode */ } }

/** Days until the last paid period ends, when that is 14 days or fewer away; null otherwise. */
export function planEndingSoon(billing: MyBilling | null, now: number): { days: number; date: string } | null {
  if (!billing?.requireSubscription) return null;
  const ends = billing.subscriptions.map(item => new Date(item.endsAt).getTime()).filter(time => !Number.isNaN(time) && time > now);
  if (!ends.length) return null;
  const last = Math.max(...ends);
  const days = Math.ceil((last - now) / 86_400_000) - 1;
  if (days > 14) return null;
  const date = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', timeZone: 'Europe/Istanbul' }).format(new Date(last - 1));
  return { days: Math.max(0, days), date };
}
