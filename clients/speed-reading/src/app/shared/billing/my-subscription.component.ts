import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject, input } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BillingHttp, MY_SUBSCRIPTION_STYLES, MY_SUBSCRIPTION_TEMPLATE, MySubscriptionController } from '../../../../../shared/billing/my-subscription';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';

/**
 * "Aboneliğim" of one product. The page itself (controller, template, styles) is shared by every client application
 * in clients/shared/billing; this component only gives it this application's session and HTTP client.
 */
@Component({
  selector: 'app-my-subscription',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: MY_SUBSCRIPTION_TEMPLATE,
  // Live test 19.11.2026 (C6): the panel takes the product's own colours, light and dark (green buttons in Hızlı Okuma).
  styles: [`:host { --bill-accent: var(--sp-primary); --bill-on-accent: var(--sp-on-primary); --bill-surface: var(--sp-surface); --bill-soft: var(--sp-surface-2); --bill-text: var(--sp-text-1); --bill-muted: var(--sp-text-2); --bill-border: var(--sp-border); }`, MY_SUBSCRIPTION_STYLES]
})
export class MySubscriptionComponent implements OnInit {
  private readonly httpClient = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly changes = inject(ChangeDetectorRef);

  /** Product code in the billing catalog. */
  readonly product = input.required<string>();
  c!: MySubscriptionController;

  ngOnInit(): void {
    const user = this.auth.currentUserValue;
    // The billing panel shows its own errors next to the action; no second toast (live test 19.11.2026, B4).
    const headers = { 'X-Skip-Error-Toast': 'true' };
    const http: BillingHttp = {
      get: url => firstValueFrom(this.httpClient.get<never>(url, { headers })),
      post: (url, body) => firstValueFrom(this.httpClient.post<never>(url, body, { headers })),
      delete: url => firstValueFrom(this.httpClient.delete<never>(url, { headers })),
      blob: url => firstValueFrom(this.httpClient.get(url, { responseType: 'blob' }))
    };
    this.c = new MySubscriptionController(http, {
      product: this.product(),
      apiUrl: environment.apiUrl,
      roles: user?.roles ?? [],
      buyerName: [user?.firstName, user?.lastName].filter(Boolean).join(' '),
      changed: () => this.changes.markForCheck(),
      save: saveBillingFile
    });
    void this.c.load();
  }
}

export function saveBillingFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
