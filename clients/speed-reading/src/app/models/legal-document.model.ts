export enum LegalDocumentType {
    TermsOfService = 1,
    PrivacyPolicy = 2,
    KVKK = 3,
    CookiePolicy = 4
}

export interface LegalDocument {
    id: string;
    type: LegalDocumentType;
    version: string;
    language: string;
    title: string;
    content: string;
    effectiveDate: string;
    isActive: boolean;
    createdAt: string;
    updatedAt?: string;
}

export interface UserLegalAcceptance {
    id: string;
    userId: string;
    legalDocumentId: string;
    documentType: LegalDocumentType;
    documentVersion: string;
    acceptedAt: string;
    ipAddress?: string;
    userAgent?: string;
}

export interface CreateLegalDocumentDto {
    type: LegalDocumentType;
    version: string;
    language: string;
    title: string;
    content: string;
    effectiveDate: string;
    isActive: boolean;
}

export interface AcceptLegalDocumentDto {
    legalDocumentId: string;
    documentType: LegalDocumentType;
}
