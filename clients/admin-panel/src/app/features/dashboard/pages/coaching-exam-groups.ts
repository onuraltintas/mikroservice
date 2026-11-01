/**
 * KM7 (01.11.2026): Turkish labels for catalog codes, so admins never see raw codes or English values, and the exam option
 * groups (the 17 ÖABT fields under "ÖABT").
 */
export interface ExamOption { code: string; name: string; isActive?: boolean }
export interface ExamGroup { label: string; exams: ExamOption[] }

const scoreTypes: Record<string, string> = { TYT: 'TYT', SAY: 'Sayısal (SAY)', EA: 'Eşit ağırlık (EA)', SOZ: 'Sözel (SÖZ)', SÖZ: 'Sözel (SÖZ)', DIL: 'Dil (DİL)', DİL: 'Dil (DİL)', YDT: 'Dil (DİL)', LGS: 'LGS' };
const linkStatuses: Record<string, string> = { pending: 'Onay bekliyor', approved: 'Onaylı', rejected: 'Reddedildi' };
const sources: Record<string, string> = {
  'approved-catalog': 'Onaylı koçluk kataloğu', 'admin-manual': 'Elle eklenen', 'coaching-catalog-km2': 'YKS/LGS kataloğu (KM2)',
  'coaching-catalog-km3': 'AGS, ÖABT ve dil kataloğu (KM3)', 'mebi-yks': 'YÖK Atlas / MEBİ', 'mebi-lgs': 'MEBİ LGS'
};
const programTypes: Record<string, string> = { Bachelor: 'Lisans', Associate: 'Önlisans' };
const issues: Record<string, string> = {
  noMinutes: 'Süresi yok', noOutcomes: 'Kazanımı yok', noYksScope: 'YKS kapsamı yok', inactiveParent: 'Üst kaydı pasif', pendingLinks: 'Onay bekleyen bağlantı'
};
const kinds: Record<string, string> = { exams: 'Sınav', lessons: 'Ders', units: 'Ünite', topics: 'Konu', outcomes: 'Kazanım' };
const actions: Record<string, string> = { Upload: 'Yükleme', Restore: 'Geri yükleme', Delete: 'Silme' };

export const scoreTypeLabel = (code: string | null | undefined) => code ? scoreTypes[code] ?? code : '—';
export const linkStatusLabel = (status: string) => linkStatuses[status] ?? status;
/** Unknown sources get a readable label without the raw technical code. */
export const sourceLabel = (source: string | null | undefined) => !source ? '—' : sources[source] ?? source.replace(/^coaching-catalog-/, 'Katalog ').replace(/[-_]/g, ' ');
export const programTypeLabel = (type: string | null | undefined) => type ? programTypes[type] ?? type : '—';
export const issueLabel = (issue: string) => issues[issue] ?? issue;
export const kindLabel = (kind: string) => kinds[kind] ?? kind;
export const scoreModelActionLabel = (action: string) => actions[action] ?? action;

/** "ÖABT" for OABT and OABT_* codes; the codes themselves are shown with Turkish letters. */
export function examCodeLabel(code: string | null | undefined): string {
  if (!code) return '—';
  if (code === 'OABT' || code.startsWith('OABT_')) return 'ÖABT';
  if (code.startsWith('YOKDIL')) return 'YÖKDİL';
  return code;
}

/** An exam's display name without "YDS · YDS" when the name is the code. */
export function examDisplayName(exam: ExamOption): string {
  return exam.name.trim().toLocaleUpperCase('tr-TR') === exam.code ? exam.name : `${exam.name} (${examCodeLabel(exam.code)})`;
}

/** Exams for a select: the ÖABT fields are grouped under "ÖABT", the others stay in one "Sınavlar" group, in the given order. */
export function groupExamsForSelect(exams: ExamOption[]): ExamGroup[] {
  const oabt = exams.filter(x => x.code === 'OABT' || x.code.startsWith('OABT_'));
  const others = exams.filter(x => !oabt.includes(x));
  const groups: ExamGroup[] = [{ label: 'Sınavlar', exams: others }];
  if (oabt.length) groups.push({ label: 'ÖABT', exams: [...oabt].sort((a, b) => a.name.localeCompare(b.name, 'tr')) });
  return groups.filter(x => x.exams.length > 0);
}
