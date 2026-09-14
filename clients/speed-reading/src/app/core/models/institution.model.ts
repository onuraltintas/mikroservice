export interface Institution {
  id: string;
  name: string;
  code?: string;
  contactEmail: string;
  phoneNumber?: string;
  address?: string;
  city?: string;
  district?: string;
  createdAt: Date;
  isActive: boolean;
  teacherCount: number;
  studentCount: number;
  licenseType?: number;
  maxStudents?: number;
  maxTeachers?: number;
  subscriptionStartDate?: Date;
  subscriptionEndDate?: Date;
}

export interface CreateInstitutionRequest {
  name: string;
  contactEmail: string;
}

export interface UpdateInstitutionRequest {
  name?: string;
  address?: string;
  city?: string;
  district?: string;
  phone?: string;
  email?: string;
  website?: string;
}
