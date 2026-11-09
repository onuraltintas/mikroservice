import type { WeeklySlot } from './weekly-calendar';

/** Paket H5: a saved study time (the same shape as the availability editor's windows). */
export interface AvailabilityWindow { day: string; startMinute: number; endMinute: number }

/**
 * Paket H5 (K4: warn, allow): the weekly cards that are not fully inside a saved study time, in their order. Touching times on a
 * day count as one, so a card across 18:00 with 17:00–18:00 and 18:00–19:00 saved is inside. Shared by the weekly calendar, the
 * availability editor and (later) the plan wizard.
 */
export function cardsOutsideAvailability(slots: WeeklySlot[], windows: AvailabilityWindow[]): WeeklySlot[] {
  const ranges = new Map<string, [number, number][]>();
  for (const w of [...windows].sort((a, b) => a.startMinute - b.startMinute)) {
    const list = ranges.get(w.day) ?? []; const last = list.at(-1);
    if (last && w.startMinute <= last[1]) last[1] = Math.max(last[1], w.endMinute); else list.push([w.startMinute, w.endMinute]);
    ranges.set(w.day, list);
  }
  return slots.filter(s => !(ranges.get(s.day) ?? []).some(([from, to]) => from <= s.startMinute && s.startMinute + s.durationMinutes <= to));
}
