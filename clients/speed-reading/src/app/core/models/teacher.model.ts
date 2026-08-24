export interface Teacher {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  institutionId?: string;
  institutionName?: string;
  studentCount: number;
  lastLoginAt?: Date;
  isActive: boolean;
  createdAt: Date;
}

export interface CreateTeacherRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  institutionId?: string;
}

export interface UpdateTeacherRequest {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  institutionId?: string;
  isActive: boolean;
}
