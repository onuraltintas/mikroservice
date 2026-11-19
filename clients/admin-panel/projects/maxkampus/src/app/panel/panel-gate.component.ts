import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CoursePanelComponent } from '../../../../staff-portal/src/app/features/course-management/panel/course-panel.component';
import { GoogleSignup } from '../account/google-signup';
import { MaxkampusApi, MyBilling } from '../core/api';
import { Seo } from '../core/seo';
import { MaxkampusSession } from '../core/session.service';
import { NoticesComponent } from './notices.component';
import { PanelFrameComponent } from './panel-frame.component';
import { PlanSubscriptionComponent } from './plan-subscription.component';

/** True when billing asks for a plan and the owner has none running now. */
export function needsPlan(billing: MyBilling | null): boolean {
  return !!billing?.requireSubscription && !billing.subscriptions.some(item => item.isCurrent);
}

/**
 * /panel: the institution panel. An owner without a running plan goes to /plan first (no trial, user decision
 * 19.11.2026); a teacher works under the institution's plan. A teacher's waiting invitation is opened first.
 */
@Component({
  selector: 'mk-panel-gate',
  standalone: true,
  imports: [PanelFrameComponent, NoticesComponent, CoursePanelComponent],
  template: `
    <mk-panel-frame>
      @if (ready()) {
        <mk-notices [audience]="session.role() === 'owner' ? 'owners' : 'teachers'" />
        <staff-course-panel [role]="session.role() === 'owner' ? 'institution' : 'teacher'" />
      } @else {
        <p class="mk-status" role="status"><span class="ds-spinner" aria-hidden="true"></span> maxkampüs açılıyor</p>
      }
    </mk-panel-frame>
  `
})
export class PanelGateComponent implements OnInit {
  readonly session = inject(MaxkampusSession);
  private readonly api = inject(MaxkampusApi);
  private readonly router = inject(Router);
  private readonly signup = inject(GoogleSignup);
  private readonly seo = inject(Seo);
  readonly ready = signal(false);

  async ngOnInit(): Promise<void> {
    this.seo.set({ title: 'Panel', path: '/panel', noindex: true });
    const invite = this.signup.invite();
    if (invite) { await this.router.navigateByUrl(`/davet/${invite}`); return; }
    if (this.session.role() === 'owner') {
      const billing = await this.api.myBilling().catch(() => null);
      if (needsPlan(billing)) { await this.router.navigateByUrl('/plan'); return; }
    }
    this.ready.set(true);
  }
}

/** /plan: choose a plan and pay by bank transfer; "Ödemeniz bekleniyor" until the payment is approved. */
@Component({
  selector: 'mk-plan',
  standalone: true,
  imports: [PanelFrameComponent, PlanSubscriptionComponent, RouterLink],
  template: `
    <mk-panel-frame>
      <div class="mk-wrap page">
        <header class="head">
          <h1>{{ active() ? 'Planım ve ödemeler' : waiting() ? 'Ödemeniz bekleniyor' : 'Planınızı seçin' }}</h1>
          <p class="mk-muted">{{ active()
            ? 'Planınız etkin. Süre, ödemeler ve belgeler burada.'
            : waiting()
              ? 'Ödemeniz hesaba geçince onaylıyoruz; genellikle aynı iş günü. Onaylandığında e-posta gelir ve panel açılır.'
              : 'Panel, ödemeniz onaylanınca açılır. Havale ya da EFT ile ödersiniz; ödeme kodunuzu açıklamaya yazın. Planınızı sonra değiştirebilirsiniz.' }}</p>
          @if (active()) { <a routerLink="/panel" class="ds-btn ds-btn--primary">Panele git</a> }
        </header>
        <mk-plan-subscription [roles]="session.roles()" [buyerName]="session.displayName()" (changed)="check()" />
      </div>
    </mk-panel-frame>
  `,
  styles: `
    .page { padding-top: 28px; padding-bottom: 48px; max-width: 1040px; }
    .head { display: flex; flex-direction: column; gap: 8px; align-items: flex-start; margin-bottom: 24px; }
    h1 { margin: 0; font-size: 30px; } .head p { margin: 0; max-width: 46em; }
  `
})
export class PlanComponent implements OnInit {
  readonly session = inject(MaxkampusSession);
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  private readonly billing = signal<MyBilling | null>(null);
  readonly active = computed(() => !!this.billing() && !needsPlan(this.billing()));
  readonly waiting = computed(() => (this.billing()?.pendingOrders ?? []).some(order => order.paidDeclaredAt));

  ngOnInit(): void {
    this.seo.set({ title: 'Planınızı seçin', path: '/plan', noindex: true });
    void this.check();
  }

  async check(): Promise<void> { this.billing.set(await this.api.myBilling().catch(() => this.billing())); }
}
