import { olderZoneNotice, turkeyDateTimeLocal, turkeyDefaults, turkeyInstant } from './course-management-time';

// E3 #34 (06.11.2026) and review L6: course management reads and fills times in Türkiye time, whatever the device's zone.
describe('course management Türkiye time', () => {
  it('reads a datetime-local value as Türkiye time and shows an instant in Türkiye time', () => {
    expect(turkeyInstant('2026-11-10T18:30')).toBe('2026-11-10T18:30:00+03:00');
    expect(turkeyDateTimeLocal(new Date('2026-11-10T15:30:00Z'))).toBe('2026-11-10T18:30');
    expect(turkeyDateTimeLocal(new Date('2026-11-06T22:10:00Z'))).toBe('2026-11-07T01:10');
  });

  it('fills the form defaults from the Türkiye clock', () => {
    // 21:30 UTC on 6 November is 00:30 on Saturday 7 November in Türkiye.
    const d = turkeyDefaults(new Date('2026-11-06T21:30:00Z'));
    expect([d.lessonStartsAt, d.lessonEndsAt]).toEqual(['2026-11-07T01:00', '2026-11-07T02:00']);
    expect([d.seriesStartsOn, d.seriesEndsOn, d.seriesStartTime, d.weekdayIndex]).toEqual(['2026-11-07', '2026-12-07', '01:00', 5]);
    expect([d.timeOffStartsAt, d.timeOffEndsAt]).toEqual(['2026-11-08T09:00', '2026-11-08T10:00']);
  });

  it('tells staff that a row stored in another zone is read as Türkiye time', () => {
    expect(olderZoneNotice('Europe/London')).toBe('Eskiden Europe/London saatiyle kaydedildi; artık Türkiye saatine göre okunur.');
    expect(olderZoneNotice('Europe/Istanbul')).toBeNull();
    expect(olderZoneNotice(null)).toBeNull();
  });
});
