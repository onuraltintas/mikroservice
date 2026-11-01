import { StudentAssignment } from '../../../core/services/coaching-portal.service';
import {
  STUDENT_ASSIGNMENT_FILTERS, assignmentAttachmentError, assignmentCardState, assignmentScore
} from './student-assignment-view';

// T5 (31.10.2026): Ödevlerim shows three filters and one short label per card.
const now = new Date('2026-10-31T10:00:00+03:00');
const item = (overrides: Partial<StudentAssignment> = {}): StudentAssignment => ({
  id: 'a1', title: 'Üslü sayılar testi', subject: 'Matematik', dueDate: '2026-11-02T20:00:00+03:00', status: 'Assigned', isOverdue: false, ...overrides
});

describe('Ödevlerim list mapping', () => {
  it('offers Yapılacak, Teslim edilen and Tümü, mapped to the server filters', () => {
    expect(STUDENT_ASSIGNMENT_FILTERS.map(filter => [filter.label, filter.value]))
      .toEqual([['Yapılacak', 'open'], ['Teslim edilen', 'submitted'], ['Tümü', 'all']]);
  });

  it('counts the days left in the Türkiye day', () => {
    expect(assignmentCardState(item(), now)).toEqual({ label: '2 gün kaldı', tone: 'neutral' });
    expect(assignmentCardState(item({ dueDate: '2026-11-01T00:30:00+03:00' }), now)).toEqual({ label: 'Yarın', tone: 'warning' });
    // 23:30 in Türkiye is still today even though it is the next day in UTC+0 terms only after midnight.
    expect(assignmentCardState(item({ dueDate: '2026-10-31T23:30:00+03:00' }), now)).toEqual({ label: 'Bugün', tone: 'warning' });
  });

  it('says Gecikti for late open work and the status for submitted, graded or cancelled work', () => {
    expect(assignmentCardState(item({ dueDate: '2026-10-30T10:00:00+03:00', isOverdue: true }), now)).toEqual({ label: 'Gecikti', tone: 'danger' });
    expect(assignmentCardState(item({ status: 'Submitted', submittedAt: '2026-10-30T10:00:00Z', isOverdue: true }), now))
      .toEqual({ label: 'Teslim edildi', tone: 'success' });
    expect(assignmentCardState(item({ status: 'Graded', score: 85, maxScore: 100 }), now)).toEqual({ label: 'Değerlendirildi', tone: 'brand' });
    expect(assignmentCardState(item({ assignmentStatus: 'Cancelled', isOverdue: true }), now)).toEqual({ label: 'İptal edildi', tone: 'neutral' });
  });

  it('shows the score of graded work as 85/100', () => {
    expect(assignmentScore(item({ status: 'Graded', score: 85, maxScore: 100 }))).toBe('85/100');
    expect(assignmentScore(item({ status: 'Graded', score: 7 }))).toBe('7');
    expect(assignmentScore(item({ status: 'Submitted', score: undefined, maxScore: 100 }))).toBeNull();
  });
});

describe('K4 attachment messages', () => {
  it('names a file that failed the scan and a graded assignment that is locked', () => {
    expect(assignmentAttachmentError({ status: 400, error: { code: 'Attachment.NotClean' } }))
      .toBe('Bir dosya güvenlik taramasından geçmedi ya da hâlâ taranıyor. O dosyayı kaldırıp yeniden yükle.');
    expect(assignmentAttachmentError({ status: 400, error: { code: 'Attachment.Locked' } }))
      .toBe('Ödevin değerlendirildi; artık dosya ekleyip kaldıramazsın.');
    expect(assignmentAttachmentError({ status: 400, error: { code: 'Attachment.LimitReached' } }))
      .toBe('Bir ödeve en çok 10 dosya, toplam 50 MB ekleyebilirsin. Gerekirse önce bir dosyayı kaldır.');
    expect(assignmentAttachmentError({ status: 503 })).toBe('Dosya taraması şu an yapılamıyor. Birkaç dakika sonra yeniden dene.');
    expect(assignmentAttachmentError({ status: 500 })).toBeNull();
  });
});
