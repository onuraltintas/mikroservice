import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  UserDto,
  UserDetailDto,
  CreateUserRequest,
  UpdateUserRequest,
  AssignRoleRequest,
  Institution,
  PagedResult,
  RoleDto
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

  /**
   * Get all users with optional filtering
   * Backend returns: ApiResponse<PagedResult<UserDto>>
   * Service receives: PagedResult<UserDto> (auto-unwrapped)
   * Then extracts items array
   */
  getUsers(searchTerm?: string, role?: string, isActive?: boolean): Observable<UserDto[]> {
    let params = new HttpParams();
    params = params.set('page', '1');
    params = params.set('pageSize', '100');
    if (searchTerm) {
      params = params.set('search', searchTerm);
    }
    if (role) {
      params = params.set('role', role);
    }
    if (isActive !== undefined) {
      params = params.set('isActive', isActive.toString());
    }
    return this.http.get<PagedResult<UserDto>>(this.API_URL, { params }).pipe(
      map(response => response.items)
    );
  }

  /**
   * Get user by ID
   * Backend returns: ApiResponse<UserDetailDto>
   * Service receives: UserDetailDto (auto-unwrapped)
   */
  getUserById(id: string): Observable<UserDetailDto> {
    return this.http.get<UserDetailDto>(`${this.API_URL}/${id}`);
  }

  /**
   * Create new user
   * Backend returns: ApiResponse<UserDto>
   * Service receives: UserDto (auto-unwrapped)
   */
  createUser(request: CreateUserRequest): Observable<UserDto> {
    return this.http.post<UserDto>(this.API_URL, request);
  }

  /**
   * Update existing user
   * Backend returns: ApiResponse<UserDto>
   * Service receives: UserDto (auto-unwrapped)
   */
  updateUser(id: string, request: UpdateUserRequest): Observable<UserDto> {
    return this.http.put<UserDto>(`${this.API_URL}/${id}`, request);
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
    return this.http.post<void>(`${this.API_URL}/${userId}/restore`, {});
  }

  /**
   * Deactivate user
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  deactivateUser(userId: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${userId}/deactivate`, {});
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
    return this.http.get<Institution[]>(`${environment.apiUrl}/v1/institutions`);
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
  adminResetPassword(userId: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${userId}/reset-password-admin`, { userId, newPassword });
  }
}
