import { StudentAssignment, StudentAssignmentFilter } from '../../../core/services/coaching-portal.service';
import type { CsTone } from '../ui';

/** T5 (31.10.2026): Ödevlerim has three filters. Open work includes late work (the server lists it first by due date). */
export const STUDENT_ASSIGNMENT_FILTERS: { label: string; value: StudentAssignmentFilter }[] = [
  { label: 'Yapılacak', value: 'open' },
  { label: 'Teslim edilen', value: 'submitted' },
  { label: 'Tümü', value: 'all' }
];

export interface AssignmentCardState { label: string; tone: CsTone }

const TURKEY = 'Europe/Istanbul';
const dayNumber = (date: Date) => {
  const [year, month, day] = new Intl.DateTimeFormat('en-CA', { timeZone: TURKEY, year: 'numeric', month: '2-digit', day: '2-digit' })
    .format(date).split('-').map(Number);
  return Date.UTC(year, month - 1, day) / 86_400_000;
};

/** One short label per card: the status once handed in, otherwise how many Türkiye days are left. */
export function assignmentCardState(assignment: StudentAssignment, now = new Date()): AssignmentCardState {
  if (assignment.assignmentStatus === 'Cancelled') return { label: 'İptal edildi', tone: 'neutral' };
  if (assignment.status === 'Graded') return { label: 'Değerlendirildi', tone: 'brand' };
  if (assignment.submittedAt || assignment.status === 'Submitted') return { label: 'Teslim edildi', tone: 'success' };
  if (assignment.assignmentStatus === 'Completed') return { label: 'Kapandı', tone: 'neutral' };
  const due = new Date(assignment.dueDate);
  if (assignment.isOverdue || Number.isNaN(due.getTime()) || due < now) return { label: 'Gecikti', tone: 'danger' };
  const days = dayNumber(due) - dayNumber(now);
  if (days <= 0) return { label: 'Bugün', tone: 'warning' };
  if (days === 1) return { label: 'Yarın', tone: 'warning' };
  return { label: `${days} gün kaldı`, tone: 'neutral' };
}

/** "85/100" for graded work; null until a score exists. */
export function assignmentScore(assignment: Pick<StudentAssignment, 'status' | 'score' | 'maxScore'>): string | null {
  if (assignment.status !== 'Graded' || assignment.score === undefined || assignment.score === null) return null;
  return assignment.maxScore ? `${assignment.score}/${assignment.maxScore}` : `${assignment.score}`;
}

/** K4 (31.10.2026): plain words for the attachment rules the server enforces; null when nothing specific applies. */
export function assignmentAttachmentError(error: unknown): string | null {
  const failure = error as { status?: number; error?: { code?: string } } | null;
  switch (failure?.error?.code) {
    case 'Attachment.NotClean': return 'Bir dosya güvenlik taramasından geçmedi ya da hâlâ taranıyor. O dosyayı kaldırıp yeniden yükle.';
    case 'Attachment.Locked': return 'Ödevin değerlendirildi; artık dosya ekleyip kaldıramazsın.';
    case 'Attachment.LimitReached': return 'Bir ödeve en çok 10 dosya, toplam 50 MB ekleyebilirsin. Gerekirse önce bir dosyayı kaldır.';
  }
  if (failure?.status === 503) return 'Dosya taraması şu an yapılamıyor. Birkaç dakika sonra yeniden dene.';
  return null;
}

const dateTime = new Intl.DateTimeFormat('tr-TR', { timeZone: TURKEY, day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' });
const dateOnly = new Intl.DateTimeFormat('tr-TR', { timeZone: TURKEY, day: '2-digit', month: '2-digit', year: 'numeric' });
/** "2 Kasım 20:00" in Türkiye time. */
export const assignmentDateTime = (value: string) => { const date = new Date(value); return Number.isNaN(date.getTime()) ? '' : dateTime.format(date); };
/** "30.12.2029" in Türkiye time. */
export const assignmentDate = (value: string) => { const date = new Date(value); return Number.isNaN(date.getTime()) ? '' : dateOnly.format(date); };
