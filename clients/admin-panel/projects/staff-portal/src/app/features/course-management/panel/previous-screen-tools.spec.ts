import { AUDIT_LABELS, auditLabel } from './audit-labels';
import { legacySources } from './sections/legacy-import.component';

// The earlier single-page screen's tools, moved into the panel (21.11.2026).
describe('tools moved from the earlier screen', () => {
  it('names every change in Turkish and never shows a code', () => {
    expect(auditLabel('Group.TeacherChanged')).toBe('Grubun öğretmeni değişti');
    expect(auditLabel('CourseRoom.Deleted')).toBe('Derslik silindi');
    expect(auditLabel('Something.Unknown')).toBe('Diğer işlem');
    expect(Object.keys(AUDIT_LABELS).length).toBeGreaterThan(80);
  });

  it('moves from another product only where the owner manages an institution', () => {
    expect(legacySources([{ product: 'course-management', roles: [] }])).toEqual([]);
    expect(legacySources([{ product: 'coaching', roles: ['Teacher'] }, { product: 'speed-reading', roles: ['InstitutionAdmin'] }]))
      .toEqual(['speed-reading']);
  });
});
