/**
 * KM7 (01.11.2026): Turkish labels for the curriculum editor. Admins never see enum values or raw source codes.
 * Unknown codes fall back to a neutral Turkish label, never to the code itself.
 */
export const CONFIDENCE_LABELS: Record<string, string> = { Low: 'Düşük', Medium: 'Orta', High: 'Yüksek' };
export const METHOD_LABELS: Record<string, string> = { ReviewedStudy: 'İncelenmiş çalışma süresi', OfficialBudgetDraft: 'Resmî ders saatinden ilk dağılım', ImportedCatalog: 'Katalog aktarımı' };
export const MAPPING_STATUS_LABELS: Record<string, string> = { pending: 'Onay bekliyor', approved: 'Onaylı', rejected: 'Reddedildi', all: 'Tümü' };
export const QUALITY_ISSUE_LABELS: Record<string, string> = {
  noMinutes: 'Süresi yok', noOutcomes: 'Kazanımı yok', noYksScope: 'YKS kapsamı yok', inactiveParent: 'Üst kaydı pasif', pendingLinks: 'Onay bekleyen bağlantı'
};
export const SOURCE_KIND_LABELS: Record<string, string> = { Official: 'Resmî program kazanımı', ExamSkill: 'Sınav hazırlık becerisi' };
export const SOURCE_LABELS: Record<string, string> = {
  'approved-catalog': 'Onaylı ders kataloğu', 'admin-manual': 'Yönetici girişi', 'coaching-catalog-km2': 'Koçluk kataloğu · YKS/LGS taslağı',
  'coaching-catalog-km3': 'Koçluk kataloğu · AGS, ÖABT, YDS, YÖKDİL taslağı', 'mebi-yks': 'MEBİ YKS tercih verisi', 'mebi-lgs': 'MEBİ LGS okul verisi',
  'meb-ttkb': 'MEB öğretim programı', osym: 'ÖSYM kılavuzu', 'outcome-import': 'Kazanım paketi aktarımı'
};
export const SCORE_TYPE_LABELS: Record<string, string> = { TYT: 'TYT', SAY: 'Sayısal', EA: 'Eşit ağırlık', SOZ: 'Sözel', 'SÖZ': 'Sözel', DIL: 'Dil', 'DİL': 'Dil' };
export const YIELD_LABELS: Record<number, string> = { 1: 'Nadiren', 2: 'Az', 3: 'Orta', 4: 'Sık', 5: 'Çok sık' };
const EXAM_FALLBACK: Record<string, string> = { TYT: 'TYT', AYT: 'AYT', YDT: 'YDT', LGS: 'LGS', AGS: 'AGS', OABT: 'ÖABT', YDS: 'YDS', YOKDIL: 'YÖKDİL', TDP: 'TDP' };

export function label(map: Record<string, string>, value: string | null | undefined, fallback = 'Belirtilmedi'): string {
  if (value === null || value === undefined || value === '') return fallback;
  return map[value] ?? fallback;
}
/** "coaching-catalog-km3" → its Turkish name; an unknown code is shown as "Diğer kaynak", not as the code. */
export function sourceLabel(source: string | null | undefined): string {
  if (!source) return 'Kaynak belirtilmedi';
  return SOURCE_LABELS[source] ?? (source.startsWith('coaching-catalog') ? 'Koçluk kataloğu' : 'Diğer kaynak');
}
/** An exam code as admins read it: "OABT_TURKCE" → "ÖABT · Türkçe" (from the exam list when known). */
export function examLabel(code: string | null | undefined, exams: { code: string; name: string }[] = []): string {
  if (!code) return 'Sınav belirtilmedi';
  const exam = exams.find(x => x.code === code);
  if (code.startsWith('OABT_')) {
    const name = exam?.name.replace(/^ÖABT\s*[·:-]?\s*/i, '').replace(/\s*\(ÖABT\)\s*/i, '').trim();
    return name ? `ÖABT · ${name}` : 'ÖABT alanı';
  }
  return EXAM_FALLBACK[code] ?? exam?.name ?? 'Diğer sınav';
}
