/**
 * Alan adı planı (user decision 18.11.2026): every group signs in on its own address, so one browser can keep a
 * student, a parent, a teacher, an institution manager and an administrator signed in at the same time (the session
 * cookie belongs to one host). onuraltintas.net is closed; eduivme.com itself only forwards to admin.eduivme.com.
 */
export type EduivmeSite = 'student' | 'parent' | 'teacher' | 'institution' | 'admin';

export const EDUIVME_SITE_URLS: Readonly<Record<EduivmeSite, string>> = {
  student: 'https://maxkoc.com',
  parent: 'https://veli.maxkoc.com',
  teacher: 'https://ogretmen.eduivme.com',
  institution: 'https://kurum.eduivme.com',
  admin: 'https://admin.eduivme.com'
};

/** The staff portal sits under /staff/ on the teacher and institution addresses. */
export const STAFF_PORTAL_PATH = '/staff/';

export const EDUIVME_SITE_LABELS: Readonly<Record<EduivmeSite, string>> = {
  student: 'öğrenci',
  parent: 'veli',
  teacher: 'öğretmen',
  institution: 'kurum',
  admin: 'yönetim'
};

const SITE_BY_HOST: Readonly<Record<string, EduivmeSite>> = {
  'maxkoc.com': 'student',
  'www.maxkoc.com': 'student',
  'veli.maxkoc.com': 'parent',
  'ogretmen.eduivme.com': 'teacher',
  'kurum.eduivme.com': 'institution',
  'admin.eduivme.com': 'admin',
  'eduivme.com': 'admin',
  'www.eduivme.com': 'admin',
};

/** The site a host belongs to; null for any other host (localhost, a test host), where no site rule applies. */
export function siteForHost(hostname: string | null | undefined): EduivmeSite | null {
  return SITE_BY_HOST[(hostname ?? '').trim().toLowerCase().replace(/\.$/, '')] ?? null;
}

/** Start address of a site: the staff portal for teachers and institutions, the root elsewhere. */
export function siteStartUrl(site: EduivmeSite): string {
  return EDUIVME_SITE_URLS[site] + (site === 'teacher' || site === 'institution' ? STAFF_PORTAL_PATH : '/');
}

/** A link to another group's page: absolute on the public addresses, the bare path on any other host (localhost). */
export function siteHref(target: EduivmeSite, path: string, hostname: string | null | undefined): string {
  return siteForHost(hostname) === null ? path : EDUIVME_SITE_URLS[target] + path;
}

/** Brand decision 18.11.2026: maxkoç on the student and parent addresses, eduivme everywhere else (and on localhost). */
export type SiteBrandName = 'maxkoç' | 'eduivme';
export function brandNameForHost(hostname: string | null | undefined): SiteBrandName {
  const site = siteForHost(hostname);
  return site === 'student' || site === 'parent' ? 'maxkoç' : 'eduivme';
}
/** The brand with the Turkish dative ending: "maxkoç'a", "eduivme'ye". */
export function brandDative(name: SiteBrandName): string {
  return name === 'maxkoç' ? "maxkoç'a" : "eduivme'ye";
}
