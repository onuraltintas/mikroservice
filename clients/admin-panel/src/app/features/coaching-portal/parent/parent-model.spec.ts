import {
  coachingStatusLine, firstName, hiddenProductsText, parentNotificationLabel, parentNotificationTarget, possessive, productFromSlug,
  productSlug, speedReadingStatusLine, visibleProducts
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
});
