import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface UserDto {
    userId: string;
    email: string;
    fullName: string;
    role: string;
    isActive: boolean;
    emailConfirmed: boolean;
    avatarUrl?: string;
    phoneNumber?: string;
    lastLoginAt?: string;
    roles: string[];
    permissions: string[];
}

export interface TeacherDetailsDto {
    title?: string;
    subjects: string[];
    institutionId?: string;
    institutionName?: string;
}

export interface StudentDetailsDto {
    gradeLevel?: number;
    studentNumber?: string;
    institutionId?: string;
    institutionName?: string;
    birthDate?: string;
    learningStyle?: string;
}

export interface UserProfileDto extends UserDto {
    firstName: string;
    lastName: string;
    teacherDetails?: TeacherDetailsDto;
    studentDetails?: StudentDetailsDto;
}

export interface CreateUserRequest {
    firstName: string;
    lastName: string;
    email: string;
    role: string;
    phoneNumber?: string;
}

export interface UpdateUserRequest {
    firstName: string;
    lastName: string;
    phoneNumber?: string;
}

export interface PagedResult<T> {
    items: T[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
}


export interface RoleDto {
    id: string;
    name: string;
    description: string;
    isSystemRole: boolean;
    isDeleted: boolean;
}

export interface CreateRoleRequest {
    name: string;
    description: string;
}

export interface UpdateRoleRequest {
    name: string;
    description: string;
}

export interface RolePermissionsDto {
    roleId: string;
    roleName: string;
    assignedPermissions: string[];
}

export interface PermissionDto {
    id: string;
    key: string;
    description: string;
    group: string;
    isSystem: boolean;
    isDeleted: boolean;
}

export interface CreatePermissionRequest {
    key: string;
    description: string;
    group: string;
}

export interface UpdatePermissionRequest {
    description: string;
    group: string;
}

@Injectable({
    providedIn: 'root'
})
export class IdentityService {
    private http = inject(HttpClient);
    // environment.apiUrl includes '/api' suffix (e.g. http://localhost:5000/api)
    private baseUrl = `${environment.apiUrl}/users`;
    private rolesUrl = `${environment.apiUrl}/roles`;

    getAllUsers(page: number, pageSize: number, search: string = '', role?: string, isActive?: boolean) {
        let params = new HttpParams()
            .set('pageNumber', page)
            .set('pageSize', pageSize);

        if (search) params = params.set('searchTerm', search);
        if (role) params = params.set('role', role);
        if (isActive !== undefined && isActive !== null) params = params.set('isActive', isActive);

        return this.http.get<PagedResult<UserDto>>(this.baseUrl, { params });
    }

    createUser(user: CreateUserRequest) {
        return this.http.post<{ userId: string, temporaryPassword: string }>(this.baseUrl, user);
    }

    deleteUser(userId: string, permanent: boolean = false) {
        return this.http.delete(`${this.baseUrl}/${userId}?permanent=${permanent}`);
    }

    activateUser(userId: string) {
        return this.http.post(`${this.baseUrl}/${userId}/activate`, {});
    }

    confirmEmail(userId: string) {
        return this.http.post(`${this.baseUrl}/${userId}/confirm-email`, {});
    }

    updateUser(userId: string, user: UpdateUserRequest) {
        return this.http.put(`${this.baseUrl}/${userId}`, user);
    }

    changePassword(userId: string, password: string) {
        return this.http.post(`${this.baseUrl}/${userId}/change-password`, { password });
    }

    getUserById(userId: string) {
        return this.http.get<UserProfileDto>(`${this.baseUrl}/${userId}`);
    }

    getMyProfile() {
        return this.http.get<UserProfileDto>(`${this.baseUrl}/me`);
    }

    updateMyProfile(data: any) {
        return this.http.put(`${this.baseUrl}/me`, data);
    }

    changeMyPassword(data: any) {
        return this.http.post(`${this.baseUrl}/me/change-password`, data);
    }

    getRoles() {
        return this.http.get<string[]>(`${this.baseUrl}/roles`);
    }

    getAllRoles() {
        return this.http.get<RoleDto[]>(this.rolesUrl);
    }

    createRole(role: CreateRoleRequest) {
        return this.http.post<string>(this.rolesUrl, role);
    }

    updateRole(roleId: string, role: UpdateRoleRequest) {
        return this.http.put(`${this.rolesUrl}/${roleId}`, role);
    }

    deleteRoleById(id: string, permanent: boolean = false) {
        return this.http.delete(`${this.rolesUrl}/${id}`, {
            params: { permanent: permanent.toString() }
        });
    }

    restoreRole(id: string) {
        return this.http.put(`${this.rolesUrl}/${id}/restore`, {});
    }

    assignRole(userId: string, roleName: string) {
        return this.http.post(`${this.baseUrl}/${userId}/roles`, { roleName });
    }

    removeRole(userId: string, roleName: string) {
        return this.http.delete(`${this.baseUrl}/${userId}/roles/${roleName}`);
    }

    // Permission Methods
    getPermissions() {
        return this.http.get<PermissionDto[]>(`${environment.apiUrl}/permissions`);
    }

    createPermission(permission: CreatePermissionRequest) {
        return this.http.post<string>(`${environment.apiUrl}/permissions`, permission);
    }

    updatePermission(id: string, permission: UpdatePermissionRequest) {
        return this.http.put(`${environment.apiUrl}/permissions/${id}`, permission);
    }

    deletePermission(id: string, permanent: boolean = false) {
        return this.http.delete(`${environment.apiUrl}/permissions/${id}`, {
            params: { permanent: permanent.toString() }
        });
    }

    restorePermission(id: string) {
        return this.http.put(`${environment.apiUrl}/permissions/${id}/restore`, {});
    }

    // Role Permission Methods
    getRolePermissions(roleId: string) {
        return this.http.get<RolePermissionsDto>(`${this.rolesUrl}/${roleId}/permissions`);
    }

    updateRolePermissions(roleId: string, permissions: string[]) {
        return this.http.put(`${this.rolesUrl}/${roleId}/permissions`, { permissions });
    }
}
