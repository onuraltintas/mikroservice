import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CurrentCoachingAgreement {
  documentId: string;
  documentVersion: string;
  locale: string;
  title: string;
  documentReference: string;
  contentSha256: string;
  effectiveAt: string;
  acknowledgedByCurrentStudent: boolean;
  acknowledgementId: string | null;
}

export interface CoachingAgreementAcknowledgement {
  acknowledgementId: string;
  agreementDocumentId: string;
  locale: string;
  acknowledgedAt: string;
  withdrawnAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class CoachingAgreementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/coaching-agreements`;

  getCurrent(locale = 'tr-TR'): Observable<CurrentCoachingAgreement> {
    return this.http.get<CurrentCoachingAgreement>(`${this.baseUrl}/current`, {
      params: new HttpParams().set('locale', locale),
      headers: new HttpHeaders().set('X-Skip-Error-Toast', 'true')
    });
  }

  acknowledge(agreementDocumentId: string): Observable<CoachingAgreementAcknowledgement> {
    return this.http.post<CoachingAgreementAcknowledgement>(
      `${this.baseUrl}/current/acknowledgements`,
      { agreementDocumentId }
    );
  }
}
