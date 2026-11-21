import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject, input, output } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BillingHttp, MY_SUBSCRIPTION_STYLES, MY_SUBSCRIPTION_TEMPLATE, MySubscriptionController } from '../../../../../../shared/billing/my-subscription';
import { environment } from '../../environments/environment';

/**
 * "Aboneliğim" of Kurs Yönetimi on maxkampus.com: the page every eduivme product shares (clients/shared/billing). The
 * maxkampüs plans are the owner's own plans (buyer type "Bireysel" in billing), so they are offered; "siz" is used.
 */
@Component({
  selector: 'mk-plan-subscription',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: MY_SUBSCRIPTION_TEMPLATE,
  // maxkampüs colours instead of the shared page's default blue (20.11.2026 live test: "Satın al" was blue).
  styles: [`:host { --bill-accent: var(--ds-primary); --bill-on-accent: var(--ds-on-primary); --bill-surface: var(--ds-surface);
    --bill-soft: var(--ds-soft); --bill-text: var(--ds-text); --bill-muted: var(--ds-muted); --bill-border: var(--ds-field); }`, MY_SUBSCRIPTION_STYLES,
    // B design (21.11.2026), maxkampüs only: the shared billing cards take the panel's raised card; fields keep their stronger border.
    `.bill-card { border-color: color-mix(in srgb, var(--ds-line) 70%, transparent); border-radius: var(--ds-radius-card, 16px);
      box-shadow: var(--ds-shadow-raised, var(--ds-shadow)); }
    .bill-btn, .bill-form input, .bill-form select { border-radius: 10px; } .bill-alert { border-radius: 12px; }`]
})
export class PlanSubscriptionComponent implements OnInit {
  private readonly httpClient = inject(HttpClient);
  private readonly changes = inject(ChangeDetectorRef);
  readonly roles = input<readonly string[]>([]);
  readonly buyerName = input('');
  /** After every change: the host checks whether the panel can open now. */
  readonly changed = output<void>();
  c!: MySubscriptionController;

  ngOnInit(): void {
    const http: BillingHttp = {
      get: url => firstValueFrom(this.httpClient.get<never>(url)),
      post: (url, body) => firstValueFrom(this.httpClient.post<never>(url, body)),
      delete: url => firstValueFrom(this.httpClient.delete<never>(url)),
      blob: url => firstValueFrom(this.httpClient.get(url, { responseType: 'blob' }))
    };
    this.c = new MySubscriptionController(http, {
      product: 'course-management',
      apiUrl: environment.apiUrl,
      roles: this.roles(),
      buyerName: this.buyerName(),
      voice: 'siz',
      individualPlans: true,
      changed: () => { this.changes.markForCheck(); this.changed.emit(); },
      save: (blob, fileName) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        link.click();
        URL.revokeObjectURL(url);
      }
    });
    void this.c.load();
  }
}
