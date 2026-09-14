export type AuthDestination = 'admin' | 'institution' | 'teacher' | 'coach' | 'student';

const destinationByPriority: ReadonlyArray<[AuthDestination, readonly string[]]> = [
  ['admin', ['admin', 'systemadmin', 'editor']],
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
