import { Pipe, PipeTransform } from '@angular/core';

/** Role names as people read them (the token carries the English system name). */
export const ADMIN_ROLE_LABELS: Record<string, string> = {
  SystemAdmin: 'Sistem yöneticisi', Admin: 'Yönetici', Editor: 'Editör', Teacher: 'Öğretmen', Student: 'Öğrenci', Parent: 'Veli',
  InstitutionAdmin: 'Kurum yöneticisi', InstitutionOwner: 'Kurum sahibi'
};

@Pipe({ name: 'adminRoleLabel', standalone: true })
export class AdminRoleLabelPipe implements PipeTransform {
  transform(role: string | null | undefined, fallback = ''): string {
    return role ? ADMIN_ROLE_LABELS[role] ?? role : fallback;
  }
}
