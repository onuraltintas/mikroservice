import { Pipe, PipeTransform } from '@angular/core';

/**
 * Turkish labels for the code values the Coaching API returns (enum names such as "OneOnOne" or "Clean").
 * Shared by the student portal, the staff portal and the admin panel; an unknown value is shown as it is.
 */
const labels: Record<string, string> = {
  // Sessions and attendance
  OneOnOne: 'Birebir', Group: 'Grup', Scheduled: 'Planlandı', Completed: 'Tamamlandı', Cancelled: 'İptal edildi',
  NotRecorded: 'Girilmedi', Present: 'Katıldı', Absent: 'Katılmadı', Late: 'Geç geldi', Excused: 'Mazeretli',
  // Assignments
  Active: 'Aktif', Assigned: 'Atandı', InProgress: 'Devam ediyor', Submitted: 'Teslim edildi', Graded: 'Değerlendirildi',
  Individual: 'Bireysel', Digital: 'Dijital', Book: 'Kitap', Mixed: 'Karma',
  PendingUpload: 'Yükleniyor', PendingScan: 'Taranıyor', Clean: 'Hazır', Rejected: 'Reddedildi',
  // Exams and goals
  Mock: 'Deneme sınavı', Weekly: 'Haftalık test', MidTerm: 'Ara sınav', Final: 'Final', Quiz: 'Kısa sınav',
  StudentReported: 'Öğrencinin girdiği', TeacherRecorded: 'Öğretmenin girdiği',
  ExamPreparation: 'Sınav hazırlığı', SubjectMastery: 'Ders hakimiyeti', GradeImprovement: 'Not yükseltme',
  StudyHabits: 'Çalışma alışkanlıkları', TimeManagement: 'Zaman yönetimi', Other: 'Diğer',
  // Subscriptions and payments
  Pending: 'Bekliyor', Approved: 'Onaylandı', Withdrawn: 'Geri çekildi', Expired: 'Süresi doldu', Succeeded: 'Başarılı',
  BankTransfer: 'Havale/EFT', Manual: 'Elle', OneTime: 'Tek seferlik', Quarterly: '3 aylık', Annual: 'Yıllık',
  Teacher: 'Öğretmen', Institution: 'Kurum', Student: 'Öğrenci',
  // Newsletter sign-up places
  Footer: 'Sayfa altı', Blog: 'Blog', BlogLanding: 'Blog ana sayfası', Homepage: 'Ana sayfa'
};

const byKind: Record<string, Record<string, string>> = {
  billingPeriod: { Monthly: 'Aylık', Quarterly: '3 aylık', Annual: 'Yıllık', OneTime: 'Tek seferlik' },
  examType: { Monthly: 'Aylık değerlendirme', Weekly: 'Haftalık test', Mock: 'Deneme sınavı', YDT: 'YDT (Dil)', AGS: 'AGS / ÖABT', YOKDIL: 'YÖKDİL' }
};

export type CoachingLabelKind = keyof typeof byKind;

export function coachingLabel(value: string | number | null | undefined, kind?: CoachingLabelKind | string): string {
  if (value === null || value === undefined || value === '') return '—';
  const key = String(value);
  return (kind ? byKind[kind]?.[key] : undefined) ?? labels[key] ?? (key === 'Monthly' ? 'Aylık' : key);
}

/** A calendar date ("2026-11-03") or a timestamp, as dd.MM.yyyy in Turkey's time. */
export function coachingDate(value: string | Date | null | undefined): string {
  if (!value) return '—';
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) {
    const [year, month, day] = value.split('-');
    return `${day}.${month}.${year}`;
  }
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return new Intl.DateTimeFormat('tr-TR', { timeZone: 'Europe/Istanbul', day: '2-digit', month: '2-digit', year: 'numeric' }).format(date);
}

@Pipe({ name: 'coachingLabel', standalone: true })
export class CoachingLabelPipe implements PipeTransform {
  transform(value: string | number | null | undefined, kind?: CoachingLabelKind | string): string {
    return coachingLabel(value, kind);
  }
}

@Pipe({ name: 'coachingDate', standalone: true })
export class CoachingDatePipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    return coachingDate(value);
  }
}
