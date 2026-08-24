export interface DeletedRecord {
    id: string;
    entityType: string;
    displayName: string;
    deletedAt: Date;
    deletedBy: string;
    metadata: Record<string, any>;
}

export interface RestoreRecordRequest {
    entityType: string;
    id: string;
}

export interface HardDeleteRecordRequest {
    entityType: string;
    id: string;
}

export interface EntityTypeDto {
    typeName: string;
    displayName: string;
    deletedCount: number;
}
