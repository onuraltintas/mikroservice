import type { UserDto, UserProductRoleDto } from './identity.service';

export type RoleProduct = 'coaching' | 'speed-reading';

export function isRoleProduct(product: string | null): product is RoleProduct {
    return product === 'coaching' || product === 'speed-reading';
}

export function userProductRoles(user: Pick<UserDto, 'roles' | 'productRoles'> | null): UserProductRoleDto[] {
    return user?.productRoles ?? [...new Set(user?.roles ?? [])].map(role => ({ role, product: null }));
}

/** Live test 19.11.2026 (B14): role codes are shown by their Turkish names; unknown codes stay as they are. */
export const ROLE_NAMES: Readonly<Record<string, string>> = {
    Student: 'Öğrenci',
    Teacher: 'Öğretmen',
    InstitutionAdmin: 'Kurum yöneticisi',
    InstitutionOwner: 'Kurum sahibi',
    Parent: 'Veli',
    Editor: 'Editör',
    SystemAdmin: 'Sistem yöneticisi'
};

export function roleName(role: string): string {
    return ROLE_NAMES[role] ?? role;
}

export function productRoleLabel(assignment: UserProductRoleDto): string {
    const service = assignment.product === 'coaching' ? 'Koçluk'
        : assignment.product === 'speed-reading' ? 'Hızlı Okuma'
        : assignment.product ?? (assignment.role === 'SystemAdmin' ? 'tüm servisler' : 'servis belirtilmemiş');
    return `${roleName(assignment.role)}, ${service}`;
}
