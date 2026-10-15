import type { UserDto, UserProductRoleDto } from './identity.service';

export type RoleProduct = 'coaching' | 'speed-reading';

export function isRoleProduct(product: string | null): product is RoleProduct {
    return product === 'coaching' || product === 'speed-reading';
}

export function userProductRoles(user: Pick<UserDto, 'roles' | 'productRoles'> | null): UserProductRoleDto[] {
    return user?.productRoles ?? [...new Set(user?.roles ?? [])].map(role => ({ role, product: null }));
}

export function productRoleLabel(assignment: UserProductRoleDto): string {
    const service = assignment.product === 'coaching' ? 'Koçluk'
        : assignment.product === 'speed-reading' ? 'Hızlı Okuma'
        : assignment.product ?? (assignment.role === 'SystemAdmin' ? 'Tüm servisler' : 'Servis belirtilmemiş');
    return `${assignment.role} · ${service}`;
}
