import {
  coachingStatusLine, firstName, hiddenProductsText, parentNotificationLabel, parentNotificationTarget, possessive, productFromSlug,
  productSlug, speedReadingStatusLine, visibleProducts, ParentPlanWeek, parentPlanDays, parentTaskState, planClock, shiftWeek
} from './parent-model';

// V4 (01.11.2026): the parent panel shows only the products the child uses and left visible, in the parent's voice.
describe('parent model', () => {
  const link = (products?: { product: 'Coaching' | 'SpeedReading'; studentHasAccess: boolean; visibleToParent: boolean }[]) =>
    ({ studentName: 'Elif Yılmaz', products }) as never;

  it('keeps only products the child uses and shows to the parent, coaching first', () => {
    expect(visibleProducts(link([
      { product: 'Coaching', studentHasAccess: true, visibleToParent: true },
      { product: 'SpeedReading', studentHasAccess: true, visibleToParent: true }]))).toEqual(['Coaching', 'SpeedReading']);
    expect(visibleProducts(link([
      { product: 'SpeedReading', studentHasAccess: true, visibleToParent: true },
      { product: 'Coaching', studentHasAccess: false, visibleToParent: true }]))).toEqual(['SpeedReading']);
    expect(visibleProducts(link([
      { product: 'Coaching', studentHasAccess: true, visibleToParent: false },
      { product: 'SpeedReading', studentHasAccess: false, visibleToParent: true }]))).toEqual([]);
  });

  it('falls back to Koçluk while the link has no product list yet', () => {
    expect(visibleProducts(link(undefined))).toEqual(['Coaching']);
  });

  it('explains why nothing is shown', () => {
    expect(hiddenProductsText(link([
      { product: 'Coaching', studentHasAccess: true, visibleToParent: false },
      { product: 'SpeedReading', studentHasAccess: false, visibleToParent: true }]))).toContain('Elif ilerlemesini seninle paylaşmayı kapattı');
    expect(hiddenProductsText(link([
      { product: 'Coaching', studentHasAccess: false, visibleToParent: true },
      { product: 'SpeedReading', studentHasAccess: false, visibleToParent: true }]))).toContain('Elif şu an Koçluk ya da Hızlı Okuma kullanmıyor');
  });

  it('builds Turkish possessives for names', () => {
    expect(possessive('Elif')).toBe("Elif'in");
    expect(possessive('Can')).toBe("Can'ın");
    expect(possessive('Ayşe')).toBe("Ayşe'nin");
    expect(possessive('Umut')).toBe("Umut'un");
    expect(possessive('Gül')).toBe("Gül'ün");
    expect(possessive('Arda')).toBe("Arda'nın");
    expect(firstName('Elif Nur Yılmaz')).toBe('Elif');
    expect(firstName('')).toBe('Çocuğun');
  });

  it('maps the tab address to products', () => {
    expect(productSlug('Coaching')).toBe('kocluk');
    expect(productSlug('SpeedReading')).toBe('hizli-okuma');
    expect(productFromSlug('hizli-okuma')).toBe('SpeedReading');
    expect(productFromSlug('kocluk')).toBe('Coaching');
    expect(productFromSlug('başka')).toBeNull();
  });

  it('writes one short status line per product', () => {
    expect(coachingStatusLine({ planned: 14, completed: 9 } as never)).toBe('Bu hafta 9/14 görev');
    expect(coachingStatusLine({ planned: 0, completed: 0 } as never)).toBe('Bu hafta planlı görev yok');
    expect(speedReadingStatusLine({ program: { currentDay: 12, totalDays: 28 }, thisWeek: { minutes: 40 } } as never)).toBe('Program 12/28. gün');
    expect(speedReadingStatusLine({ program: { currentDay: 28, totalDays: 28, status: 'completed' }, thisWeek: { minutes: 0 } } as never)).toBe('Program tamamlandı');
    expect(speedReadingStatusLine({ program: null, thisWeek: { minutes: 35 } } as never)).toBe('Bu hafta 35 dk okuma');
    expect(speedReadingStatusLine({ program: null, thisWeek: { minutes: 0 } } as never)).toBe('Bu hafta henüz çalışmadı');
  });

  it('opens the right page from a parent notification and never guesses', () => {
    const id = '3f2a1d63-0b7e-4c49-9a59-6b4a7c1f2e10';
    expect(parentNotificationTarget({ type: 'ParentWeeklySummary', relatedEntityId: id })).toEqual({ path: `/coaching-portal/parent/children/${id}`, label: 'Özeti aç' });
    expect(parentNotificationTarget({ type: 'CoachingMessage', relatedEntityId: id })).toEqual({ path: `/coaching-portal/messages/${id}`, label: 'Mesajı aç' });
    expect(parentNotificationTarget({ type: 'ParentLinkApproved', relatedEntityId: null })).toEqual({ path: '/coaching-portal/parent', label: 'Çocuklarım' });
    expect(parentNotificationTarget({ type: 'ParentWeeklySummary', relatedEntityId: 'nope' })).toBeNull();
    expect(parentNotificationLabel('ParentWeeklySummary')).toBe('Haftalık özet');
    expect(parentNotificationLabel('SomethingNew')).toBe('Bildirim');
  });

  it('labels and opens every Identity V1 parent-link notice on Çocuklarım', () => {
    for (const type of ['ParentLinkRequested', 'ParentLinkApproved', 'ParentLinkRevoked']) {
      expect(parentNotificationTarget({ type, relatedEntityId: 'a4cea155-5485-4db6-9f52-1ec4df8080c3' })).toEqual({ path: '/coaching-portal/parent', label: 'Çocuklarım' });
      expect(parentNotificationLabel(type)).toBe('Veli bağlantısı');
    }
  });
});

// Live test 16.11.2026 (#49): the parent's read-only plan of a week.
describe('the child plan of a week', () => {
  const week: ParentPlanWeek = { weekStart: '2026-11-16', today: '2026-11-17', hasActivePlan: true, tasks: [
    { date: '2026-11-16', startMinute: 1020, minutes: 9, title: 'Konu A', lesson: 'Tarih', questions: null, completed: true, continuation: true },
    { date: '2026-11-17T00:00:00', startMinute: null, minutes: 30, title: 'Konu B', lesson: null, questions: 12, completed: false, continuation: false }] };
  it('lays the work out over the seven days and marks today', () => {
    const days = parentPlanDays(week);
    expect(days.map(day => day.date)).toEqual(['2026-11-16', '2026-11-17', '2026-11-18', '2026-11-19', '2026-11-20', '2026-11-21', '2026-11-22']);
    expect(days[0].tasks.map(task => task.title)).toEqual(['Konu A']);
    expect(days[1].isToday).toBe(true);
    expect(days[1].tasks.length).toBe(1);
    expect(days[2].tasks).toEqual([]);
    expect(days[0].label).toContain('16 Kasım');
  });
  it('writes the hour of a work and moves by whole weeks', () => {
    expect(planClock(1020, 9)).toBe('17:00–17:09, 9 dk');
    expect(planClock(null, 30)).toBe('30 dk');
    expect(shiftWeek('2026-11-16', 1)).toBe('2026-11-23');
    expect(shiftWeek('2026-11-16', -1)).toBe('2026-11-09');
  });

  it('shows a past unfinished study as late (Gecikti)', () => {
    expect(parentTaskState({ completed: false, date: '2026-11-16T00:00:00' }, '2026-11-18')).toBe('Gecikti');
    expect(parentTaskState({ completed: false, date: '2026-11-18' }, '2026-11-18')).toBe('Bekliyor');
    expect(parentTaskState({ completed: true, date: '2026-11-16' }, '2026-11-18')).toBe('Tamamlandı');
  });
});
