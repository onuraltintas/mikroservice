export enum AnnouncementPriority {
  Low = 1,
  Normal = 2,
  High = 3,
  Urgent = 4
}

export enum AnnouncementAudience {
  All = 1,
  Students = 2,
  Teachers = 3,
  Admins = 4,
  Institution = 5,
  Custom = 6
}

export enum AnnouncementDisplayType {
  Banner = 1,
  Modal = 2,
  Notification = 3,
  Toast = 4,
  Sidebar = 5
}

export interface AnnouncementDto {
  id: string;
  title: string;
  content: string;
  plainTextContent?: string;
  priority: AnnouncementPriority;
  displayType: AnnouncementDisplayType;
  icon?: string;
  colorTheme?: string;
  actionUrl?: string;
  actionText?: string;
  isPinned: boolean;
  startDate: string;
  expiresAt?: string;
  createdAt: string;
  hasViewed: boolean;
  hasDismissed: boolean;
  hasClicked: boolean;
}

export interface AnnouncementDetailDto extends AnnouncementDto {
  targetAudience: AnnouncementAudience;
  targetInstitutionId?: string;
  targetRoles?: string;
  isActive: boolean;
  sendEmailNotification: boolean;
  createInAppNotification: boolean;
  emailCampaignId?: string;
  viewCount: number;
  clickCount: number;
  updatedAt?: string;
  createdBy: string;
}

export interface AnnouncementStatsDto {
  announcementId: string;
  totalViewCount: number;
  uniqueViewCount: number;
  totalClickCount: number;
  uniqueClickCount: number;
  dismissCount: number;
  viewRate: number;
  clickThroughRate: number;
  dismissRate: number;
  firstViewedAt?: string;
  lastViewedAt?: string;
}

export interface CreateAnnouncementRequest {
  title: string;
  content: string;
  plainTextContent?: string;
  priority: AnnouncementPriority;
  targetAudience: AnnouncementAudience;
  targetInstitutionId?: string;
  targetRoles?: string[];
  isPinned: boolean;
  startDate?: string;
  expiresAt?: string;
  displayType: AnnouncementDisplayType;
  icon?: string;
  colorTheme?: string;
  actionUrl?: string;
  actionText?: string;
  sendEmailNotification: boolean;
  createInAppNotification: boolean;
}

export interface UpdateAnnouncementRequest {
  title: string;
  content: string;
  plainTextContent?: string;
  priority: AnnouncementPriority;
  targetAudience: AnnouncementAudience;
  targetInstitutionId?: string;
  targetRoles?: string[];
  isPinned: boolean;
  isActive: boolean;
  startDate?: string;
  expiresAt?: string;
  displayType: AnnouncementDisplayType;
  icon?: string;
  colorTheme?: string;
  actionUrl?: string;
  actionText?: string;
}
