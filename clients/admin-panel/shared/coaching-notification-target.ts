export interface CoachingNotificationReference {
  type?: string | number;
  relatedEntityId?: string | null;
}

export interface WeeklyDestination { studentId: string; weekStart: string; decisionId: string | null }
export type CoachingNotificationTarget = { kind: 'plan' | 'assignment'; id: string; label: string }
  | ({ kind: 'weekly'; id: string; label: string } & WeeklyDestination);
export function validCoachingId(value: string | null | undefined): value is string {
  return !!value && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) && value !== '00000000-0000-0000-0000-000000000000';
}
export function validWeeklyDate(value: string | null | undefined): value is string {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const date = new Date(value + 'T00:00:00Z');
  return Number.isFinite(date.getTime()) && date.toISOString().slice(0,10) === value && date.getUTCDay() === 1;
}

const labels: Record<string, string> = {
  StudyPlanReminder: 'Günlük çalışma hatırlatması', StudyPlanOverdueReminder: 'Geciken çalışmalar',
  StudyPlanPublished: 'Çalışma planı yayımlandı', StudyPlanUpdated: 'Çalışma planı güncellendi',
  AssignmentCreated: 'Yeni ödev', AssignmentUpdated: 'Ödev güncellendi',
  AssignmentSubmitted: 'Ödev teslim edildi', AssignmentGraded: 'Ödev değerlendirildi',
  WeeklyReflectionSaved: 'Haftalık değerlendirme kaydedildi', WeeklyDecisionCreated: 'Yeni haftalık karar', WeeklyDecisionUpdated: 'Haftalık karar güncellendi'
};

export function coachingNotificationLabel(type: string): string {
  return labels[type] ?? type;
}

// Older notifications may have no entity reference. Never guess a destination from their text.
export function coachingNotificationTarget(notification: CoachingNotificationReference, role: 'student' | 'teacher'): CoachingNotificationTarget | null {
  const id = notification.relatedEntityId;
  if (typeof notification.type === 'string' && (role === 'teacher' && notification.type === 'WeeklyReflectionSaved'
    || role === 'student' && ['WeeklyDecisionCreated','WeeklyDecisionUpdated'].includes(notification.type))) {
    const parts = id?.split(':') ?? []; const decision = parts[3] ?? null;
    if (parts[0] !== 'weekly' || !validCoachingId(parts[1]) || !validWeeklyDate(parts[2])
      || notification.type === 'WeeklyReflectionSaved' && parts.length !== 3
      || notification.type !== 'WeeklyReflectionSaved' && (parts.length !== 4 || !validCoachingId(decision))) return null;
    return { kind: 'weekly', id: parts[1], studentId: parts[1], weekStart: parts[2], decisionId: decision,
      label: role === 'teacher' ? 'Haftalık değerlendirmeyi aç' : 'Haftalık kararı aç' };
  }
  if (!validCoachingId(id)) return null;
  if (role === 'teacher') {
    return notification.type === 'AssignmentSubmitted' ? {kind:'assignment', id, label:'Teslimleri incele'} : null;
  }
  switch (notification.type) {
    case 'StudyPlanReminder': case 'StudyPlanOverdueReminder': case 'StudyPlanPublished': case 'StudyPlanUpdated': return {kind:'plan', id, label:'Planı aç'};
    case 'AssignmentCreated': case 'AssignmentUpdated': case 'AssignmentGraded': return {kind:'assignment', id, label:'Ödevi aç'};
    default: return null;
  }
}
