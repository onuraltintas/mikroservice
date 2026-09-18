export type AuthDestination = 'exercisePreview' | 'institution' | 'teacher' | 'coach' | 'student';

const destinationByPriority: ReadonlyArray<[AuthDestination, readonly string[]]> = [
  // The central admin application remains available through eduivme.com. On
  // the Master application, these roles get a read-only exercise catalogue
  // preview so they can try every exercise without creating student data.
  ['exercisePreview', ['admin', 'systemadmin', 'editor']],
  ['institution', ['institutionadmin', 'institutionowner']],
  ['teacher', ['teacher']],
  ['coach', ['coach']],
  ['student', ['student']]
];

export function resolveAuthDestination(roles: readonly string[] | null | undefined): AuthDestination | null {
  const normalizedRoles = new Set(
    (roles ?? []).filter((role): role is string => typeof role === 'string').map(role => role.trim().toLowerCase())
  );

  return destinationByPriority.find(([, supportedRoles]) =>
    supportedRoles.some(role => normalizedRoles.has(role))
  )?.[0] ?? null;
}
