import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export type StaffInvitationProduct = 'coaching' | 'speed-reading';

export interface StaffPendingInvitation {
  invitationId: string;
  email: string;
  role: string;
  createdAt: string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class StaffInvitationsService {
  private readonly http = inject(HttpClient);

  getPending(product: StaffInvitationProduct): Observable<StaffPendingInvitation[]> {
    return this.http.get<StaffPendingInvitation[]>(`${this.getUrl(product)}/sent-pending`);
  }

  cancel(product: StaffInvitationProduct, invitationId: string): Observable<void> {
    return this.http.post<void>(
      `${this.getUrl(product)}/${encodeURIComponent(invitationId)}/cancel`,
      {},
    );
  }

  private getUrl(product: StaffInvitationProduct): string {
    const prefix = product === 'speed-reading' ? '/speed-reading' : '';
    return `${environment.apiUrl}${prefix}/invitations`;
  }
}
