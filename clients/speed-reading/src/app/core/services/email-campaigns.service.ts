import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  EmailCampaign,
  EmailCampaignDetail,
  CreateCampaignRequest,
  UpdateCampaignRequest,
  SendCampaignRequest,
  CampaignStats,
  CampaignStatus
} from '../models/email-campaign.model';

@Injectable({
  providedIn: 'root'
})
export class EmailCampaignsService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/v1/email-campaigns`;

  getCampaigns(status?: CampaignStatus): Observable<EmailCampaign[]> {
    let params = new HttpParams();
    if (status !== undefined) {
      params = params.set('status', status.toString());
    }
    return this.http.get<EmailCampaign[]>(this.API_URL, { params });
  }

  getCampaignDetail(id: string): Observable<EmailCampaignDetail> {
    return this.http.get<EmailCampaignDetail>(`${this.API_URL}/${id}`);
  }

  createCampaign(request: CreateCampaignRequest): Observable<EmailCampaign> {
    return this.http.post<EmailCampaign>(this.API_URL, request);
  }

  updateCampaign(id: string, request: UpdateCampaignRequest): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/${id}`, request);
  }

  deleteCampaign(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }

  sendCampaign(id: string, request: SendCampaignRequest): Observable<{ message: string; totalRecipients?: number }> {
    return this.http.post<{ message: string; totalRecipients?: number }>(`${this.API_URL}/${id}/send`, request);
  }

  getCampaignStats(id: string): Observable<CampaignStats> {
    return this.http.get<CampaignStats>(`${this.API_URL}/${id}/stats`);
  }
}
