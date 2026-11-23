export type StaffProduct = 'coaching' | 'speed-reading' | 'course-management';

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
  /** Staff sign-in (23.11.2026): the product the service opened. */
  product?: StaffProduct | null;
}

export interface StaffLoginResult {
  requiresMfa: boolean;
  mfaEnrollmentRequired: boolean;
  mfaChallengeToken: string | null;
  /** The product the session (or the second-factor step) is for. */
  product: StaffProduct;
}

/** The address the portal runs on: the service opens the product where the account has this role. */
export type StaffWorkspace = 'teacher' | 'institution';
