/**
 * E3 #34 (user decision 06.11.2026): no time zone choice; every lesson, series, time off and teacher hour is Türkiye time,
 * whatever the device's zone. Türkiye has been on UTC+3 all year since 2016 (no daylight saving).
 */
export const TURKEY_ZONE = 'Europe/Istanbul';
const OFFSET_MS = 3 * 60 * 60 * 1000;
const pad = (part: number) => String(part).padStart(2, '0');
/** The wall clock in Türkiye, read through the UTC getters. */
const turkeyClock = (value: Date) => new Date(value.getTime() + OFFSET_MS);

/** A datetime-local value ("2026-11-10T18:30") read as Türkiye time. */
export function turkeyInstant(local: string): string {
  return /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(local) ? `${local}:00+03:00` : new Date(local).toISOString();
}

/** The datetime-local value of an instant in Türkiye time. */
export function turkeyDateTimeLocal(value: Date): string {
  const t = turkeyClock(value);
  return `${t.getUTCFullYear()}-${pad(t.getUTCMonth() + 1)}-${pad(t.getUTCDate())}T${pad(t.getUTCHours())}:${pad(t.getUTCMinutes())}`;
}

/** The form defaults (review L6): the next full hour for a lesson, a 30-day series from that day, tomorrow 09:00 for time off. */
export function turkeyDefaults(now: Date) {
  const t = turkeyClock(now);
  const day = (offsetDays: number) => {
    const d = new Date(Date.UTC(t.getUTCFullYear(), t.getUTCMonth(), t.getUTCDate() + offsetDays));
    return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`;
  };
  const start = new Date(Date.UTC(t.getUTCFullYear(), t.getUTCMonth(), t.getUTCDate(), t.getUTCHours() + 1));
  const end = new Date(start.getTime() + 60 * 60 * 1000);
  const local = (d: Date) => `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}T${pad(d.getUTCHours())}:00`;
  const startDay = `${start.getUTCFullYear()}-${pad(start.getUTCMonth() + 1)}-${pad(start.getUTCDate())}`;
  const seriesEnd = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), start.getUTCDate() + 30));
  return {
    lessonStartsAt: local(start), lessonEndsAt: local(end),
    seriesStartsOn: startDay, seriesEndsOn: `${seriesEnd.getUTCFullYear()}-${pad(seriesEnd.getUTCMonth() + 1)}-${pad(seriesEnd.getUTCDate())}`,
    seriesStartTime: `${pad(start.getUTCHours())}:00`, weekdayIndex: (start.getUTCDay() + 6) % 7,
    timeOffStartsAt: `${day(1)}T09:00`, timeOffEndsAt: `${day(1)}T10:00`,
    /** Today in Türkiye, for date-only fields. */
    today: day(0)
  };
}

/** A row saved before E3 in another zone (re-check 4): it is now read as Türkiye time, and the panel says so. */
export function olderZoneNotice(zone: string | null | undefined): string | null {
  return zone && zone !== TURKEY_ZONE ? `Eskiden ${zone} saatiyle kaydedildi; artık Türkiye saatine göre okunur.` : null;
}
