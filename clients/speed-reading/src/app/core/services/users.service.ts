import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  UserDto,
  UserDetailDto,
  CreateUserRequest,
  CreateUserResponse,
  UpdateUserRequest,
  AssignRoleRequest,
  Institution,
  PagedResult,
  RoleDto,
  UpdateUserProfileRequest,
  UserSessionDto
} from '../models/user.model';

/**
 * Users Service - Refactored for ApiResponse<T> compatibility
 * 
 * CHANGES:
 * - All HTTP calls work with backend's ApiResponse<T> format
 * - ApiResponseInterceptor automatically unwraps responses
 * - Service receives clean typed data (UserDto[], Institution[], etc.)
 * - PagedResult handling remains for backward compatibility
 */
@Injectable({
  providedIn: 'root'
})
export class UsersService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/v1/users`;
  private readonly accessManagementOptions = {
    headers: new HttpHeaders({ 'X-Skip-Forbidden-Redirect': 'true' })
  };

  /**
   * Get all users with optional filtering
   * Backend returns: ApiResponse<PagedResult<UserDto>>
   * Service receives: PagedResult<UserDto> (auto-unwrapped)
   * Then extracts items array
   */
  getUsers(searchTerm?: string, role?: string, isActive?: boolean): Observable<UserDto[]> {
    return this.getUsersPage(1, 100, searchTerm, role, isActive).pipe(
      map(response => response.items)
    );
  }

  getUsersPage(
    page = 1,
    pageSize = 25,
    searchTerm?: string,
    role?: string,
    isActive?: boolean
  ): Observable<PagedResult<UserDto>> {
    let params = new HttpParams();
    params = params.set('page', page.toString());
    params = params.set('pageSize', pageSize.toString());
    if (searchTerm) {
      params = params.set('search', searchTerm);
    }
    if (role) {
      params = params.set('role', role);
    }
    if (isActive !== undefined) {
      params = params.set('isActive', isActive.toString());
    }
    return this.http.get<PagedResult<UserDto> | UserDto[] | any>(this.API_URL, { params }).pipe(
      map(response => {
        if (Array.isArray(response)) {
          return {
            items: response.map(user => this.normalizeUser(user)),
            totalCount: response.length,
            pageNumber: page,
            pageSize,
            totalPages: Math.ceil(response.length / pageSize),
            hasPreviousPage: page > 1,
            hasNextPage: false
          };
        }

        return {
          items: (response?.items ?? []).map((user: any) => this.normalizeUser(user)),
          totalCount: response?.totalCount ?? 0,
          pageNumber: response?.pageNumber ?? page,
          pageSize: response?.pageSize ?? pageSize,
          totalPages: response?.totalPages ?? 0,
          hasPreviousPage: response?.hasPreviousPage ?? page > 1,
          hasNextPage: response?.hasNextPage ?? false
        };
      })
    );
  }

  /**
   * Get user by ID
   * Backend returns: ApiResponse<UserDetailDto>
   * Service receives: UserDetailDto (auto-unwrapped)
   */
  getUserById(id: string): Observable<UserDetailDto> {
    return this.http.get<UserDetailDto>(`${this.API_URL}/${id}`).pipe(
      map(user => this.normalizeUser(user) as UserDetailDto)
    );
  }

  getMyProfile(): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.API_URL}/me`).pipe(
      map(user => this.normalizeUser(user))
    );
  }

  /**
   * Create new user
   * Backend returns: ApiResponse<CreateUserResponse>
   * Service receives: CreateUserResponse (auto-unwrapped)
   */
  createUser(request: CreateUserRequest, suppressForbiddenRedirect = false): Observable<CreateUserResponse> {
    const options = suppressForbiddenRedirect ? this.accessManagementOptions : {};
    return this.http.post<CreateUserResponse>(this.API_URL, {
      email: request.email,
      firstName: request.firstName,
      lastName: request.lastName,
      phoneNumber: request.phoneNumber,
      role: request.role
    }, options);
  }

  /**
   * Update existing user
   * Backend returns: ApiResponse<UserDto>
   * Service receives: UserDto (auto-unwrapped)
   */
  updateUser(id: string, request: UpdateUserRequest): Observable<UserDto> {
    return this.http.put<UserDto>(`${this.API_URL}/${id}`, request);
  }

  updateUserProfile(id: string, request: UpdateUserProfileRequest): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/${id}/profile`, request);
  }

  /**
   * Delete user
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  deleteUser(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }

  /**
   * Assign role to user
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  assignRole(userId: string, request: AssignRoleRequest): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${userId}/roles`, request);
  }

  /**
   * Remove role from user
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  removeRole(userId: string, roleName: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${userId}/roles/${roleName}`);
  }

  /**
   * Activate user
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  activateUser(userId: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${userId}/activate`, {});
  }

  /**
   * Restore (un-delete) a soft-deleted user
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  restoreUser(userId: string): Observable<void> {
    return this.activateUser(userId);
  }

  /**
   * Deactivate user
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  deactivateUser(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${userId}`, {
      params: new HttpParams().set('permanent', 'false')
    });
  }

  /**
   * Confirm user's email (Admin only)
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  confirmEmail(userId: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${userId}/confirm-email`, {});
  }

  /**
   * Revoke email confirmation (Admin only)
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  revokeEmailConfirmation(userId: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${userId}/revoke-email-confirmation`, {});
  }

  /**
   * Get all institutions (for dropdown)
   * Backend returns: ApiResponse<Institution[]>
   * Service receives: Institution[] (auto-unwrapped)
   */
  getInstitutions(): Observable<Institution[]> {
    return this.http.get<any>(`${environment.apiUrl}/v1/institutions`, {
      params: new HttpParams().set('pageNumber', '1').set('pageSize', '100')
    }).pipe(
      map(response => (Array.isArray(response) ? response : (response?.items ?? []))
        .map((institution: any) => ({
          id: institution.id,
          name: institution.name,
          code: institution.code,
          contactEmail: institution.email ?? institution.contactEmail ?? '',
          phoneNumber: institution.phone ?? institution.phoneNumber,
          address: institution.address,
          city: institution.city,
          district: institution.district,
          createdAt: institution.createdAt
            ? new Date(institution.createdAt)
            : new Date(institution.subscriptionStartDate ?? 0),
          isActive: institution.isActive,
          teacherCount: institution.teacherCount ?? 0,
          studentCount: institution.studentCount ?? 0
        })))
    );
  }

  /**
   * Get available roles
   * Backend returns: ApiResponse<string[]>
   * Service receives: string[] (auto-unwrapped)
   */
  getAvailableRoles(): Observable<RoleDto[]> {
    return this.http.get<RoleDto[]>(`${environment.apiUrl}/v1/roles`);
  }

  /**
   * Admin Reset Password Override
   * Backend returns: ApiResponse<void>
   */
  adminResetPassword(
    userId: string,
    newPassword: string,
    suppressForbiddenRedirect = false
  ): Observable<void> {
    const options = suppressForbiddenRedirect ? this.accessManagementOptions : {};
    return this.http.post<void>(`${this.API_URL}/${userId}/change-password`, { password: newPassword }, options);
  }

  getSessions(userId: string): Observable<UserSessionDto[]> {
    return this.http.get<UserSessionDto[] | { items?: UserSessionDto[] }>(
      `${this.API_URL}/${userId}/sessions`,
      this.accessManagementOptions
    ).pipe(
      map(response => Array.isArray(response) ? response : (response?.items ?? []))
    );
  }

  revokeSession(userId: string, sessionId: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${userId}/sessions/${sessionId}`, this.accessManagementOptions);
  }

  revokeAllSessions(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${userId}/sessions`, this.accessManagementOptions);
  }

  resetMfa(userId: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${userId}/mfa/reset`, {}, this.accessManagementOptions);
  }

  private normalizeUser(user: any): UserDto {
    return {
      ...user,
      id: user.id ?? user.userId,
      roles: user.roles ?? (user.role ? [user.role] : []),
      createdAt: user.createdAt ? new Date(user.createdAt) : undefined,
      updatedAt: user.updatedAt ? new Date(user.updatedAt) : undefined,
      lastLoginAt: user.lastLoginAt ? new Date(user.lastLoginAt) : undefined,
      institutionId: user.institutionId ?? user.studentDetails?.institutionId ?? user.teacherDetails?.institutionId,
      institutionName: user.institutionName ?? user.studentDetails?.institutionName ?? user.teacherDetails?.institutionName,
      learningStyle: user.learningStyle ?? user.studentDetails?.learningStyle,
      ageGroupId: user.ageGroupId,
      dateOfBirth: user.dateOfBirth ?? user.studentDetails?.birthDate
    };
  }
}
