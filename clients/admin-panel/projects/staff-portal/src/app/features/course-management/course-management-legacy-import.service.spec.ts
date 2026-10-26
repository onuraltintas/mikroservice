import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CourseManagementLegacyImportService } from './course-management-legacy-import.service';

describe('CourseManagementLegacyImportService', () => {
  let http: HttpTestingController;
  let service: CourseManagementLegacyImportService;
  const getProductAccessToken = vi.fn();

  beforeEach(() => {
    getProductAccessToken.mockReset().mockResolvedValue('temporary-source-token');
    TestBed.configureTestingModule({
      providers: [CourseManagementLegacyImportService, provideHttpClient(), provideHttpClientTesting(),
        { provide: StaffAuthService, useValue: { getProductAccessToken } }]
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(CourseManagementLegacyImportService);
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('reads the authorized Coaching roster page with the temporary source token', async () => {
    const page = service.getStudentPage('coaching', 2);
    await Promise.resolve();
    const request = http.expectOne('/api/institution/students?page=2&pageSize=100');
    expect(request.request.headers.get('Authorization')).toBe('Bearer temporary-source-token');
    expect(request.request.withCredentials).toBe(true);
    expect(getProductAccessToken).toHaveBeenCalledWith('coaching');
    request.flush({ items: [{ userId: 'source-student', firstName: 'Ayşe', lastName: 'Yılmaz',
      email: 'ayse@example.test', isActive: false }], totalCount: 101, pageNumber: 2, pageSize: 100 });

    await expect(page).resolves.toEqual({
      items: [{ sourceRecordId: 'source-student', displayName: 'Ayşe Yılmaz', email: 'ayse@example.test', isActive: false }],
      totalCount: 101, pageNumber: 2, pageSize: 100
    });
  });

  it('resolves the active Speed Reading institution then reads only its student page', async () => {
    const page = service.getStudentPage('speed-reading', 1);
    await Promise.resolve();
    const institution = http.expectOne('/api/institution/speed-reading/me');
    expect(institution.request.headers.get('Authorization')).toBe('Bearer temporary-source-token');
    institution.flush({ institutionId: 'institution-1' });
    await Promise.resolve();

    const members = http.expectOne('/api/speed-reading/institutions/institution-1/members?role=Student&pageNumber=1&pageSize=100');
    expect(members.request.headers.get('Authorization')).toBe('Bearer temporary-source-token');
    expect(members.request.withCredentials).toBe(true);
    expect(getProductAccessToken).toHaveBeenCalledWith('speed-reading');
    members.flush({ items: [{ userId: 'speed-student', firstName: 'Deniz', lastName: 'Kaya',
      email: null, role: 'Student', isActive: true }], totalCount: 1, pageNumber: 1, pageSize: 100 });

    await expect(page).resolves.toEqual({
      items: [{ sourceRecordId: 'speed-student', displayName: 'Deniz Kaya', email: null, isActive: true }],
      totalCount: 1, pageNumber: 1, pageSize: 100
    });
  });

  it('returns the active source institutions for an explicit owner selection', async () => {
    const importer = service as unknown as {
      getInstitutionPage(source: string, pageNumber: number): Promise<unknown>;
    };
    const institutionPage = importer.getInstitutionPage('coaching', 1);
    await Promise.resolve();
    const request = http.expectOne('/api/institutions?pageNumber=1&pageSize=100&isActive=true');
    expect(request.request.headers.get('Authorization')).toBe('Bearer temporary-source-token');
    expect(request.request.withCredentials).toBe(true);
    request.flush({ items: [
      { id: 'source-school', name: 'Kaynak okul', type: 'School', isActive: true },
      { id: 'source-institution', name: 'Kaynak dershane', type: 2, isActive: true },
      { id: 'inactive-institution', name: 'Kapalı kurum', type: 'StudyCenter', isActive: false }
    ], totalCount: 3, pageNumber: 1, pageSize: 100 });

    await expect(institutionPage).resolves.toEqual({
      items: [
        { sourceInstitutionId: 'source-school', name: 'Kaynak okul', institutionCategory: 'School' },
        { sourceInstitutionId: 'source-institution', name: 'Kaynak dershane', institutionCategory: 'PrivateCourse' }
      ],
      totalCount: 3, pageNumber: 1, pageSize: 100
    });
  });

  it('reads Coaching teachers by stable user id and never creates or activates a source membership', async () => {
    const getTeacherPage = service.getTeacherPage as unknown as (...args: unknown[]) => Promise<unknown>;
    const page = getTeacherPage.call(service, 'coaching', 'source-institution', 1);
    await Promise.resolve();

    const teachers = http.expectOne('/api/coaching-admin/institutions/source-institution/teachers?pageNumber=1&pageSize=100');
    expect(teachers.request.headers.get('Authorization')).toBe('Bearer temporary-source-token');
    expect(teachers.request.withCredentials).toBe(true);
    teachers.flush({ teachers: [{ userId: 'source-teacher', firstName: 'Ece', lastName: 'Yılmaz', email: 'ece@example.test' }], totalCount: 1 });

    await expect(page).resolves.toEqual({ items: [{ sourceUserId: 'source-teacher', displayName: 'Ece Yılmaz',
      email: 'ece@example.test', isActive: true }], totalCount: 1, pageNumber: 1, pageSize: 100 });
    expect(getProductAccessToken).toHaveBeenCalledTimes(1);
  });

  it('reads active Speed Reading teachers using the existing institution member API', async () => {
    const getTeacherPage = service.getTeacherPage as unknown as (...args: unknown[]) => Promise<unknown>;
    const page = getTeacherPage.call(service, 'speed-reading', 'reading-institution', 2);
    await Promise.resolve();

    const teachers = http.expectOne('/api/speed-reading/institutions/reading-institution/members?role=Teacher&isActive=true&pageNumber=2&pageSize=100');
    expect(teachers.request.headers.get('Authorization')).toBe('Bearer temporary-source-token');
    teachers.flush({ items: [{ userId: 'reading-teacher', firstName: 'Can', lastName: 'Demir', email: null,
      role: 'Teacher', isActive: true }], totalCount: 101, pageNumber: 2, pageSize: 100 });

    await expect(page).resolves.toEqual({ items: [{ sourceUserId: 'reading-teacher', displayName: 'Can Demir',
      email: null, isActive: true }], totalCount: 101, pageNumber: 2, pageSize: 100 });
  });

  it('requires an explicitly selected source institution before reading teachers', async () => {
    await expect(service.getTeacherPage('coaching', '', 1)).rejects.toThrow('Kaynak kurum seçilmelidir.');
    expect(getProductAccessToken).not.toHaveBeenCalled();
    http.expectNone(() => true);
  });
});
