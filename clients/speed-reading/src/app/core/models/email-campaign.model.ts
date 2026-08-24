export interface EmailCampaign {
  id: string;
  name: string;
  subject: string;
  status: CampaignStatus;
  targetRoles: string | null;
  targetInstitutionId: string | null;
  includeAllUsers: boolean;
  scheduledFor: Date | null;
  sentAt: Date | null;
  totalRecipients: number;
  sentCount: number;
  failedCount: number;
  openedCount: number;
  clickedCount: number;
  createdAt: Date;
}

export interface EmailCampaignDetail extends EmailCampaign {
  body: string;
  plainTextBody: string | null;
}

export interface CreateCampaignRequest {
  name: string;
  subject: string;
  body: string;
  plainTextBody?: string;
  targetRoles?: string;
  targetInstitutionId?: string;
  includeAllUsers: boolean;
  includeSubscribers: boolean;
  scheduledFor?: Date;
}

export interface UpdateCampaignRequest {
  name: string;
  subject: string;
  body: string;
  plainTextBody?: string;
  targetRoles?: string;
  targetInstitutionId?: string;
  includeAllUsers: boolean;
  includeSubscribers: boolean;
  scheduledFor?: Date;
}

export interface SendCampaignRequest {
  sendNow: boolean;
}

export interface CampaignStats {
  totalRecipients: number;
  sentCount: number;
  failedCount: number;
  openedCount: number;
  clickedCount: number;
  pendingCount: number;
}

export enum CampaignStatus {
  Draft = 0,
  Scheduled = 1,
  Sending = 2,
  Sent = 3,
  Cancelled = 4,
  Failed = 5
}

export const CampaignStatusLabels: Record<CampaignStatus, string> = {
  [CampaignStatus.Draft]: 'Taslak',
  [CampaignStatus.Scheduled]: 'Zamanlanmış',
  [CampaignStatus.Sending]: 'Gönderiliyor',
  [CampaignStatus.Sent]: 'Gönderildi',
  [CampaignStatus.Cancelled]: 'İptal Edildi',
  [CampaignStatus.Failed]: 'Başarısız'
};
