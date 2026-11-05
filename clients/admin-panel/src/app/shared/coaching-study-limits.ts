/**
 * Coaching study time limits in minutes; mirror Coaching.Domain StudyLimits (user decision 01.11.2026, "sınırı yükselt").
 * MAX_TOPIC_MINUTES bounds one topic or one outcome duration version (it may span several days);
 * MAX_DAY_MINUTES bounds what fits in one day: a study task, a day's total, remaining scope.
 */
export const MAX_TOPIC_MINUTES = 4320;
export const MAX_DAY_MINUTES = 1440;
