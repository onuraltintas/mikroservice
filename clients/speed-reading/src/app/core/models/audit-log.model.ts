export interface AuditLog {
  id: string;
  userId: string;
  userName: string;
  userEmail: string;
  action: string;
  entityType: string | null;
  entityId: string | null;
  entityName: string | null;
  ipAddress: string;
  timestamp: Date;
  serviceName?: string;
  httpMethod?: string;
  statusCode?: number;
  requestPath?: string;
  correlationId?: string;
  userAgent?: string;
  changedFieldsJson?: string | null;
}

export interface AuditLogDetail extends AuditLog {
  oldValues: string | null;
  newValues: string | null;
  userAgent: string;
  additionalInfo: string | null;
}

export interface AuditLogListResponse {
  logs: AuditLog[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface AuditLogFilters {
  pageNumber?: number;
  pageSize?: number;
  userId?: string;
  action?: string;
  entityType?: string;
  startDate?: Date;
  endDate?: Date;
}

export interface AuditLogStats {
  totalLogs: number;
  uniqueUsers: number;
  topActions: ActionCount[];
  topEntities: EntityCount[];
}

export interface ActionCount {
  action: string;
  count: number;
}

export interface EntityCount {
  entityType: string;
  count: number;
}
