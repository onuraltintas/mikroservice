export interface AuthResponse {
  id: string;
  token: string;
  refreshToken: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  dateOfBirth?: string | null; // ISO date string from backend
  ageGroupId?: string | null;
  ageGroupName?: string | null;
  currentLevel?: number;
  targetWPM?: number;
  targetComprehension?: number;
  dailyGoalMinutes?: number;
  learningStyle?: string;
  institutionId?: string;
  institutionName?: string;
  requiresMfa?: boolean;
  mfaEnrollmentRequired?: boolean;
  mfaChallengeToken?: string | null;
}

export interface RegisterInstitutionRequest {
  Email: string;
  Password: string;
  FirstName: string;
  LastName: string;
  InstitutionName: string;
  InstitutionType: number;
  Phone?: string;
  City: string;
}

export interface RegisterTeacherRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  institutionCode?: string;
  acceptTerms: boolean;
  acceptKVKK: boolean;
}

export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  dateOfBirth?: Date;
  ageGroupId?: string;
  ageGroupName?: string;
  currentLevel: number;
  targetWPM: number;
  targetComprehension: number;
  dailyGoalMinutes: number;
  learningStyle: 'visual' | 'auditory' | 'kinesthetic';
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

// User Management Models
export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  institutionId?: string;
  institutionName?: string;
  dateOfBirth?: Date;
  ageGroupId?: string;
  learningStyle?: 'visual' | 'auditory' | 'kinesthetic' | string;
  roles: string[];
  isActive: boolean;
  emailConfirmed: boolean;
  mfaEnabled?: boolean;
  lastLoginAt?: Date;
  createdAt?: Date;
  updatedAt?: Date;
}

export interface UserDetailDto extends UserDto {
  dateOfBirth?: Date;
  ageGroupId?: string;
  ageGroupName?: string;
  currentLevel?: number;
  targetWPM?: number;
  targetComprehension?: number;
  dailyGoalMinutes?: number;
  learningStyle?: 'visual' | 'auditory' | 'kinesthetic';
}

export interface CreateUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  phoneNumber?: string;
}

export interface CreateUserResponse {
  userId: string;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  institutionId?: string;
  email?: string;
  dateOfBirth?: string;
  isActive?: boolean;
  roles?: string[];
}

export interface UpdateUserProfileRequest {
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  institutionId?: string | null;
  bio?: string | null;
  avatarUrl?: string | null;
  teacherTitle?: string | null;
  teacherExperienceYears?: number | null;
  teacherSubjects?: string[] | null;
  studentGradeLevel?: number | null;
  studentBirthDate?: string | null;
  studentLearningStyle?: string | null;
}

export interface UserSessionDto {
  id: string;
  createdAt: string;
  expiresAt: string;
  createdByIp?: string | null;
  isPersistent: boolean;
  mfaVerifiedAt?: string | null;
}

export interface AssignRoleRequest {
  roleName: string;
}

export interface Institution {
  id: string;
  name: string;
  code?: string;
  type?: string;
  contactEmail?: string;
  phoneNumber?: string;
  address?: string;
  city?: string;
  district?: string;
}

export interface RoleDto {
  id: string;
  name: string;
  normalizedName: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
