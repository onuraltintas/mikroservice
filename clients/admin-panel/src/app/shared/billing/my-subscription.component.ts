import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject, input } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BillingHttp, MY_SUBSCRIPTION_STYLES, MY_SUBSCRIPTION_TEMPLATE, MySubscriptionController } from '../../../../../shared/billing/my-subscription';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';

/**
 * "Aboneliğim" of one product. The page itself (controller, template, styles) is shared by every client application
 * in clients/shared/billing; this component only gives it this application's session and HTTP client.
 */
@Component({
  selector: 'app-my-subscription',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: MY_SUBSCRIPTION_TEMPLATE,
  // Live test 19.11.2026 (C6): the panel takes the product's own colours, light and dark (green buttons in Hızlı Okuma).
  styles: [`:host { --bill-accent: var(--ui-brand); --bill-on-accent: var(--ui-brand-contrast); --bill-surface: var(--ui-surface); --bill-soft: var(--ui-surface-muted); --bill-text: var(--ui-text); --bill-muted: var(--ui-text-muted); --bill-border: var(--ui-border); }`, MY_SUBSCRIPTION_STYLES]
})
export class MySubscriptionComponent implements OnInit {
  private readonly httpClient = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly changes = inject(ChangeDetectorRef);

  /** Product code in the billing catalog. */
  readonly product = input.required<string>();
  c!: MySubscriptionController;

  ngOnInit(): void {
    const user = this.auth.userProfile();
    const http: BillingHttp = {
      get: url => firstValueFrom(this.httpClient.get<never>(url)),
      post: (url, body) => firstValueFrom(this.httpClient.post<never>(url, body)),
      delete: url => firstValueFrom(this.httpClient.delete<never>(url)),
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
