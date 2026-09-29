export type StaffProduct = 'coaching' | 'speed-reading';

export interface StaffProductAccess {
  product: StaffProduct;
  roles: string[];
}

export interface AuthSessionResponse {
  accessToken: string;
  tokenType: string;
  expiresInMinutes: number;
}

export interface StaffMfaSetupResponse {
  secret: string;
  otpAuthUri: string;
  setupToken: string;
}

export interface LoginResponse extends Partial<AuthSessionResponse> {
  requiresMfa?: boolean;
  mfaEnrollmentRequired?: boolean;
  mfaChallengeToken?: string | null;
}

export interface StaffLoginResult {
  requiresMfa: boolean;
  mfaEnrollmentRequired: boolean;
  mfaChallengeToken: string | null;
}
