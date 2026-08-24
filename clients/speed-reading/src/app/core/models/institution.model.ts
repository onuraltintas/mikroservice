export interface Institution {
  id: string;
  name: string;
  code?: string;
  contactEmail: string;
  phoneNumber?: string;
  address?: string;
  cityId?: string;
  districtId?: string;
  city?: any;
  district?: any;
  createdAt: Date;
  isActive: boolean;
  teacherCount: number;
  studentCount: number;
}

export interface CreateInstitutionRequest {
  name: string;
  contactEmail: string;
}

export interface UpdateInstitutionRequest {
  id: string;
  name: string;
  contactEmail: string;
  phoneNumber?: string;
  address?: string;
  cityId?: string;
  districtId?: string;
  isActive: boolean;
}
