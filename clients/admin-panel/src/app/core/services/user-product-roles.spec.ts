import { productRoleLabel, userProductRoles } from './user-product-roles';

describe('User role service labels', () => {
    it('keeps the same role assigned to both products distinct', () => {
        const roles = [{ role: 'Student', product: 'coaching' }, { role: 'Student', product: 'speed-reading' }];
        expect(userProductRoles({ roles: ['Student', 'Student'], productRoles: roles }).map(productRoleLabel))
            .toEqual(['Öğrenci, Koçluk', 'Öğrenci, Hızlı Okuma']);
    });

    it('labels global system administrators without guessing a service', () => {
        expect(productRoleLabel({ role: 'SystemAdmin', product: null })).toBe('Sistem yöneticisi, tüm servisler');
        expect(productRoleLabel({ role: 'Teacher', product: null })).toBe('Öğretmen, servis belirtilmemiş');
    });

    it('uses legacy role names only when scoped data is absent', () => {
        expect(userProductRoles({ roles: ['Teacher', 'Teacher'] })).toEqual([{ role: 'Teacher', product: null }]);
        expect(userProductRoles({ roles: ['Teacher'], productRoles: [] })).toEqual([]);
    });
});
