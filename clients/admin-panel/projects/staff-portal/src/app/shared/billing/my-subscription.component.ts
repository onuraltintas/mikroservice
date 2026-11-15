import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject, input } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BillingHttp, MY_SUBSCRIPTION_STYLES, MY_SUBSCRIPTION_TEMPLATE, MySubscriptionController } from '../../../../../../../shared/billing/my-subscription';
import { environment } from '../../../environments/environment';

/**
 * "Aboneliğim" for staff (a teacher or an institution's admin). The page itself is shared by every client application
 * in clients/shared/billing; this component gives it the staff portal's HTTP client. The staff session carries no
 * name, so the buyer writes it for the contract.
 */
@Component({
  selector: 'staff-my-subscription',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: MY_SUBSCRIPTION_TEMPLATE,
  styles: [MY_SUBSCRIPTION_STYLES]
})
export class StaffMySubscriptionComponent implements OnInit {
  private readonly httpClient = inject(HttpClient);
  private readonly changes = inject(ChangeDetectorRef);

  /** Product code in the billing catalog. */
  readonly product = input.required<string>();
  /** The roles the plans are offered for; the billing service checks them again. */
  readonly roles = input<readonly string[]>([]);
  c!: MySubscriptionController;

  ngOnInit(): void {
    const http: BillingHttp = {
      get: url => firstValueFrom(this.httpClient.get<never>(url)),
      post: (url, body) => firstValueFrom(this.httpClient.post<never>(url, body)),
      delete: url => firstValueFrom(this.httpClient.delete<never>(url)),
      blob: url => firstValueFrom(this.httpClient.get(url, { responseType: 'blob' }))
    };
    this.c = new MySubscriptionController(http, {
      product: this.product(),
      apiUrl: environment.apiUrl,
      roles: this.roles(),
      buyerName: '',
      changed: () => this.changes.markForCheck(),
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
