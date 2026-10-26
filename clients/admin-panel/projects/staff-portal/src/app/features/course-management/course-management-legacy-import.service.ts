import { HttpBackend, HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { environment } from '../../../environments/environment';

export type LegacyStudentSource = 'coaching' | 'speed-reading';
export type LegacyInstitutionCategory = 'School' | 'PrivateCourse' | 'StudyCenter' | 'OnlinePlatform';

export interface LegacyStudentSourceRow {
  sourceRecordId: string;
  displayName: string;
  email: string | null;
  isActive: boolean;
}

export interface LegacyStudentSourcePage {
  items: LegacyStudentSourceRow[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface LegacyTeacherSourceRow {
  sourceUserId: string;
  displayName: string;
  email: string | null;
  isActive: boolean;
}

export interface LegacyTeacherSourcePage {
  items: LegacyTeacherSourceRow[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface LegacyInstitutionSourceInfo {
  sourceInstitutionId: string;
  name: string;
  institutionCategory: LegacyInstitutionCategory;
}

export interface LegacyInstitutionSourcePage {
  items: LegacyInstitutionSourceInfo[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

interface Page<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

interface CoachingStudent {
  userId: string;
  firstName: string;
  lastName: string;
  email: string | null;
  isActive: boolean;
}

interface SpeedReadingMember {
  userId: string;
  firstName: string;
  lastName: string;
  email: string | null;
  role: string;
  isActive: boolean;
}

interface SpeedReadingInstitution {
  institutionId: string;
}

interface IdentityInstitution {
  id: string;
  name: string;
  type: LegacyInstitutionCategory | number;
  isActive: boolean;
}

interface CoachingTeacher {
  userId: string;
  firstName: string;
  lastName: string;
  email: string | null;
}

interface CoachingTeacherPage {
  teachers: CoachingTeacher[];
  totalCount: number;
}

interface SpeedReadingTeacher extends SpeedReadingMember {
  role: string;
}

@Injectable({ providedIn: 'root' })
export class CourseManagementLegacyImportService {
  private readonly auth = inject(StaffAuthService);
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly baseUrl = environment.apiUrl;

  async getStudentPage(source: LegacyStudentSource, pageNumber: number): Promise<LegacyStudentSourcePage> {
    const token = await this.auth.getProductAccessToken(source);
    const headers = new HttpHeaders({ Authorization: `Bearer ${token}` });
    if (source === 'coaching') {
      const page = await firstValueFrom(this.http.get<Page<CoachingStudent>>(
        `${this.baseUrl}/institution/students`, {
          headers,
          params: { page: String(pageNumber), pageSize: '100' },
          withCredentials: true
        }));
      return {
        ...page,
        items: page.items.map(student => ({
          sourceRecordId: student.userId,
          displayName: `${student.firstName} ${student.lastName}`.trim(),
          email: student.email,
          isActive: student.isActive
        }))
      };
    }

    const institution = await firstValueFrom(this.http.get<SpeedReadingInstitution>(
      `${this.baseUrl}/institution/speed-reading/me`, { headers, withCredentials: true }));
    const page = await firstValueFrom(this.http.get<Page<SpeedReadingMember>>(
      `${this.baseUrl}/speed-reading/institutions/${encodeURIComponent(institution.institutionId)}/members`, {
        headers,
        params: { role: 'Student', pageNumber: String(pageNumber), pageSize: '100' },
        withCredentials: true
      }));
    return {
      ...page,
      items: page.items.map(student => ({
        sourceRecordId: student.userId,
        displayName: `${student.firstName} ${student.lastName}`.trim(),
        email: student.email,
        isActive: student.isActive
      }))
    };
  }

  async getInstitutionPage(source: LegacyStudentSource, pageNumber: number): Promise<LegacyInstitutionSourcePage> {
    const token = await this.auth.getProductAccessToken(source);
    const headers = new HttpHeaders({ Authorization: `Bearer ${token}` });
    const page = await firstValueFrom(this.http.get<Page<IdentityInstitution>>(
      `${this.baseUrl}/institutions`, {
        headers,
        params: { pageNumber: String(pageNumber), pageSize: '100', isActive: 'true' },
        withCredentials: true
      }));
    const items = page.items.filter(institution => institution.isActive).map(institution => ({
      sourceInstitutionId: institution.id,
      name: institution.name,
      institutionCategory: this.normalizeInstitutionCategory(institution.type)
    }));
    return {
      ...page,
      items
    };
  }

  async getTeacherPage(source: LegacyStudentSource, sourceInstitutionId: string,
    pageNumber: number): Promise<LegacyTeacherSourcePage> {
    if (!sourceInstitutionId) throw new Error('Kaynak kurum seçilmelidir.');
    const token = await this.auth.getProductAccessToken(source);
    const headers = new HttpHeaders({ Authorization: `Bearer ${token}` });
    if (source === 'coaching') {
      const page = await firstValueFrom(this.http.get<CoachingTeacherPage>(
        `${this.baseUrl}/coaching-admin/institutions/${encodeURIComponent(sourceInstitutionId)}/teachers`, {
          headers,
          params: { pageNumber: String(pageNumber), pageSize: '100' },
          withCredentials: true
        }));
      return {
        items: page.teachers.map(teacher => ({
          sourceUserId: teacher.userId,
          displayName: `${teacher.firstName} ${teacher.lastName}`.trim(),
          email: teacher.email,
          isActive: true
        })),
        totalCount: page.totalCount,
        pageNumber,
        pageSize: 100
      };
    }

    const page = await firstValueFrom(this.http.get<Page<SpeedReadingTeacher>>(
      `${this.baseUrl}/speed-reading/institutions/${encodeURIComponent(sourceInstitutionId)}/members`, {
        headers,
        params: { role: 'Teacher', isActive: 'true', pageNumber: String(pageNumber), pageSize: '100' },
        withCredentials: true
      }));
    return {
      ...page,
      items: page.items.map(teacher => ({
        sourceUserId: teacher.userId,
        displayName: `${teacher.firstName} ${teacher.lastName}`.trim(),
        email: teacher.email,
        isActive: teacher.isActive
      }))
    };
  }

  private normalizeInstitutionCategory(value: LegacyInstitutionCategory | number): LegacyInstitutionCategory {
    const categories: LegacyInstitutionCategory[] = ['School', 'PrivateCourse', 'StudyCenter', 'OnlinePlatform'];
    const category = typeof value === 'number' ? categories[value - 1] : value;
    if (!category || !categories.includes(category)) throw new Error('Kaynak kurum türü desteklenmiyor.');
    return category;
  }
}
