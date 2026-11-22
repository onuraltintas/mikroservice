import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  BILLING_NAME_SETTLE_MS, BillingHttp, BillingOrder, BillingPlan, MyBilling, MySubscriptionController, billingBreakdown, billingDuration, billingError, billingMoney,
  billingOrderStatus, billingPlansFor, billingReceiptProblem, billingTeacherTiers, billingCommonFeatures, billingMonthly
} from '../../../../../shared/billing/my-subscription';
import { AuthService } from '../../core/auth/auth.service';
import { MySubscriptionComponent } from './my-subscription.component';

const plan = (over: Partial<BillingPlan> = {}): BillingPlan => ({
  id: 'p1', name: 'TEST Aylık', description: '', buyerType: 'Individual', price: 200, currency: 'TRY', durationDays: 30, trialDays: 0,
  seatCount: null, extraSeatPrice: null, isContactOnly: false, features: ['TEST özellik'], productCodes: ['coaching'], ...over
});
const order = (over: Partial<BillingOrder> = {}): BillingOrder => ({
  id: 'o1', planId: 'p1', planName: 'TEST Aylık', productCodes: ['coaching'], buyerType: 'Individual', amount: 200, currency: 'TRY',
  durationDays: 30, paymentCode: 'EDU-7K4Q2M', status: 'Pending', createdAt: '2026-11-15T09:00:00Z', expiresAt: '2026-11-29T09:00:00Z',
  paidDeclaredAt: null, payerNote: null, hasReceipt: false, reviewedAt: null, reviewNote: null, documentsVersion: '2026-11-15',
  kind: 'Subscription', extraSeats: null, listPrice: 200, discount: 0, couponCode: null, withdrawal: null, withdrawUntil: null, hasInvoice: false, ...over
});
const me = (over: Partial<MyBilling> = {}): MyBilling => ({
  productCode: 'coaching', productName: 'Eduİvme Koçluk', requireSubscription: true, salesOpen: true, entitlements: [], subscriptions: [],
  pendingOrders: [], ...over
});
const catalog = (plans = [plan()]) => ({
  productCode: 'coaching', productName: 'Eduİvme Koçluk', requireSubscription: true, salesOpen: true, plans,
  bankAccounts: [{ bankName: 'Test Bankası', accountHolder: 'TEST Satıcı Ltd.', iban: 'TR330006100519786457841326', ibanFormatted: 'TR33 0006 1005 1978 6457 8413 26', instructions: null }]
});
const documents = { version: '2026-11-15', preInformation: 'ÖN BİLGİLENDİRME', distanceSales: 'SÖZLEŞME' };

function fake(responses: Record<string, unknown>) {
  const calls: { method: string; url: string; body?: unknown }[] = [];
  const answer = (method: string, url: string, body?: unknown) => {
    calls.push({ method, url, body });
    const key = Object.keys(responses).filter(item => `${method} ${url}`.startsWith(item)).sort((x, y) => y.length - x.length)[0];
    if (!key) return Promise.reject({ error: { detail: `Yanıt yok: ${method} ${url}` } });
    const value = responses[key];
    return value instanceof Error ? Promise.reject({ error: { detail: value.message } }) : Promise.resolve(value);
  };
  const http: BillingHttp = {
    get: url => answer('GET', url) as never, post: (url, body) => answer('POST', url, body) as never,
    delete: url => answer('DELETE', url) as never, blob: url => answer('GET', url) as never
  };
  return { http, calls, responses };
}

function controller(responses: Record<string, unknown>, roles: string[] = ['Student']) {
  const server = fake({
    'GET /api/billing/v1/me': me(), 'GET /api/billing/v1/catalog/coaching': catalog(), 'GET /api/billing/v1/orders': [],
    'GET /api/billing/v1/organizations': [], ...responses
  });
  const saved: string[] = [];
  let changes = 0;
  const c = new MySubscriptionController(server.http, {
    product: 'coaching', apiUrl: '/api', roles, buyerName: 'Deneme Öğrenci', changed: () => { changes++; }, save: (_, name) => saved.push(name)
  });
  return { c, saved, ...server, changes: () => changes };
}

describe('Aboneliğim: VAT and business documents (maxkampüs, 20.11.2026)', () => {
  const net: BillingPlan = { id: 'v', name: '10 öğrenci', description: '', buyerType: 'Individual', price: 500, currency: 'TRY', durationDays: 30,
    trialDays: 0, seatCount: null, extraSeatPrice: null, isContactOnly: false, features: [], productCodes: ['course-management'], vatRate: 20, priceWithVat: 600 };

  it('a net price shows net, VAT and total; a coupon quote is taken as billing computed it', () => {
    expect(billingBreakdown(net, null)).toEqual({ net: 500, vat: 100, total: 600, rate: 20 });
    expect(billingBreakdown(net, { code: 'X', listPrice: 500, discount: 100, amount: 480, currency: 'TRY', vatRate: 20, vatAmount: 80 }))
      .toEqual({ net: 400, vat: 80, total: 480, rate: 20 });
    expect(billingBreakdown({ ...net, vatRate: null, priceWithVat: 749, price: 749 }, null)).toEqual({ net: 749, vat: 0, total: 749, rate: null });
  });
});

describe('Aboneliğim (shared billing page)', () => {
  it('formats money, durations, states and server sentences the Turkish way', () => {
    expect(billingMoney(1234.5)).toMatch(/1\.234,50 TL/);
    expect([30, 90, 365, 7].map(billingDuration)).toEqual(['1 ay', '3 ay', '1 yıl', '7 gün']);
    expect(billingOrderStatus({ status: 'Pending', paidDeclaredAt: null })).toBe('Ödeme bekleniyor');
    expect(billingOrderStatus({ status: 'Pending', paidDeclaredAt: '2026-11-15T09:00:00Z' })).toContain('onay bekleniyor');
    expect(billingError({ error: { detail: 'Kupon geçersiz.' } })).toBe('Kupon geçersiz.');
    expect(billingError(new Error('x'))).toContain('tamamlanamadı');
    expect(billingReceiptProblem({ size: 10, type: 'text/plain' })).toContain('PDF');
    expect(billingReceiptProblem({ size: 6 * 1024 * 1024, type: 'application/pdf' })).toContain('5 MB');
    expect(billingReceiptProblem({ size: 10, type: 'image/png' })).toBeNull();
  });

  it('offers teacher plans to teachers and institution plans to whoever administers one', () => {
    const plans = [plan(), plan({ id: 'p2', buyerType: 'Teacher' }), plan({ id: 'p3', buyerType: 'Institution' })];
    expect(billingPlansFor(plans, ['Student'], false).map(item => item.id)).toEqual(['p1']);
    expect(billingPlansFor(plans, ['Teacher'], false).map(item => item.id)).toEqual(['p1', 'p2']);
    expect(billingPlansFor(plans, ['InstitutionAdmin'], true).map(item => item.id)).toEqual(['p1', 'p3']);
  });

  it('offers no student plan where individual plans are turned off (staff sites)', () => {
    const plans = [plan(), plan({ id: 'p2', buyerType: 'Teacher' }), plan({ id: 'p3', buyerType: 'Institution' })];
    expect(billingPlansFor(plans, ['InstitutionAdmin'], true, false).map(item => item.id)).toEqual(['p3']);
    expect(billingPlansFor(plans, ['Teacher'], false, false).map(item => item.id)).toEqual(['p2']);
  });

  it('speaks "siz" when the host asks for it and "sen" otherwise', async () => {
    const server = fake({
      'GET /api/billing/v1/me': me(), 'GET /api/billing/v1/catalog/coaching': catalog(), 'GET /api/billing/v1/orders': [],
      'GET /api/billing/v1/organizations': []
    });
    const options = { product: 'coaching', apiUrl: '/api', roles: ['InstitutionAdmin'], buyerName: '', changed: () => undefined, save: () => undefined };
    const formal = new MySubscriptionController(server.http, { ...options, voice: 'siz' });
    const plain = new MySubscriptionController(server.http, options);
    expect(formal.t.noActive).toBe('Etkin bir aboneliğiniz yok.');
    expect(plain.t.noActive).toBe('Etkin bir aboneliğin yok.');
    expect(formal.withdrawalStatus('Approved')).toBe('Cayma talebiniz kabul edildi');
    await formal.startTrial(plan({ trialDays: 7 }));
    expect(formal.error).toContain('adınızı ve soyadınızı yazın');
  });

  it('brings the sales documents back shortly after the buyer stops typing a name', async () => {
    vi.useFakeTimers();
    try {
      const { c, calls } = controller({ 'GET /api/billing/v1/documents': documents });
      await c.load();
      c.choose(c.plans[0]);
      await vi.advanceTimersByTimeAsync(0);
      expect(c.documents).toEqual(documents);
      c.setBuyerName('Yeni Ad');
      c.setBuyerName('Yeni Ad Soyad');
      expect(c.documents).toBeNull();
      await vi.advanceTimersByTimeAsync(BILLING_NAME_SETTLE_MS + 10);
      expect(c.documents).toEqual(documents);
      expect(calls.filter(call => call.url.startsWith('/api/billing/v1/documents')).at(-1)!.url).toContain(encodeURIComponent('Yeni Ad Soyad'));
      expect(calls.filter(call => call.url.startsWith('/api/billing/v1/documents'))).toHaveLength(2);
    } finally {
      vi.useRealTimers();
    }
  });

  it('orders a plan only after the documents written for this buyer and this price are accepted', async () => {
    const { c, calls } = controller({
      'GET /api/billing/v1/documents': documents,
      'POST /api/billing/v1/coupons/quote': { code: 'TEST10', listPrice: 200, discount: 20, amount: 180, currency: 'TRY' },
      'POST /api/billing/v1/orders': order({ amount: 180 })
    });
    await c.load();
    c.choose(c.plans[0]);
    await Promise.resolve(); await Promise.resolve();
    expect(c.documents).toEqual(documents);
    expect(c.canOrder).toBe(false);

    c.accept(true);
    c.set('couponInput', ' test10 ');
    await c.applyCoupon();
    // A new price is a new contract: the earlier tick does not carry over.
    expect(c.price).toBe(180);
    expect(c.accepted).toBe(false);
    expect(calls.at(-1)!.url).toContain('coupon=TEST10');

    c.accept(true);
    c.setBuyerName('Başka Ad');
    expect(c.canOrder).toBe(false);
    await c.loadDocuments();
    c.accept(true);
    await c.order();
    const sent = calls.find(call => call.method === 'POST' && call.url === '/api/billing/v1/orders')!;
    expect(sent.body).toEqual({ planId: 'p1', documentsVersion: '2026-11-15', buyerName: 'Başka Ad', organizationId: null, couponCode: 'TEST10' });
    expect(c.notice).toContain('EDU-7K4Q2M');
    expect(c.plan).toBeNull();
  });

  it('shows the server sentence when something is refused and stays usable', async () => {
    const { c } = controller({ 'GET /api/billing/v1/documents': documents, 'POST /api/billing/v1/coupons/quote': new Error('Bu kupon bu planda geçerli değil.') });
    await c.load();
    c.choose(c.plans[0]);
    c.set('couponInput', 'YOK');
    await c.applyCoupon();
    expect(c.error).toBe('Bu kupon bu planda geçerli değil.');
    expect(c.busy).toBe(false);
    expect(c.quote).toBeNull();
  });

  it('declares a payment with an optional receipt, and refuses a receipt of the wrong kind before sending', async () => {
    const pending = order();
    const { c, calls } = controller({ 'GET /api/billing/v1/me': me({ pendingOrders: [pending] }), 'POST /api/billing/v1/orders/o1/paid': pending });
    await c.load();
    c.beginPaid(pending);
    c.setReceipt(new File(['x'], 'not.txt', { type: 'text/plain' }));
    expect(c.error).toContain('PDF');
    expect(c.receipt).toBeNull();
    c.setReceipt(new File(['%PDF'], 'dekont.pdf', { type: 'application/pdf' }));
    c.set('payerNote', 'Gönderdim');
    await c.declarePaid();
    const form = calls.find(call => call.url.endsWith('/paid'))!.body as FormData;
    expect(form.get('note')).toBe('Gönderdim');
    expect((form.get('receipt') as File).name).toBe('dekont.pdf');
    expect(c.payingOrderId).toBeNull();
    expect(c.notice).toContain('bildirimin alındı');
  });

  it('lets a paid order be withdrawn from only while the right lasts, and shows institution subscriptions too', async () => {
    const soon = new Date(Date.now() + 86_400_000).toISOString();
    const past = new Date(Date.now() - 86_400_000).toISOString();
    const sub = { id: 's9', planId: 'p3', planName: 'TEST Kurum', productCodes: ['coaching'], status: 'Active' as const,
      startsAt: '2026-11-01T00:00:00Z', endsAt: '2026-12-01T00:00:00Z', isCurrent: true, seatCount: 20 };
    const { c, calls, saved } = controller({
      'GET /api/billing/v1/orders': [order({ id: 'a', status: 'Paid', withdrawUntil: soon, hasInvoice: true }), order({ id: 'b', status: 'Paid', withdrawUntil: past }),
        order({ id: 'c', status: 'Paid', withdrawUntil: soon, withdrawal: 'Requested' }), order({ id: 'd', productCodes: ['speed-reading'], status: 'Paid' })],
      'GET /api/billing/v1/organizations': [{ id: 'k1', name: 'TEST Kurum A.Ş.', subscriptions: [sub], pendingOrders: [] }],
      'POST /api/billing/v1/orders/a/withdrawal': order(), 'GET /api/billing/v1/orders/a/invoice': new Blob(['%PDF'], { type: 'application/pdf' })
    });
    await c.load();
    expect(c.history.map(item => item.id)).toEqual(['a', 'b', 'c']);
    expect(c.history.map(item => c.canWithdraw(item))).toEqual([true, false, false]);
    expect(c.current.map(item => item.id)).toEqual(['s9']);
    expect(c.organizationName(sub)).toBe('TEST Kurum A.Ş.');
    c.beginWithdrawal(c.history[0]);
    await c.requestWithdrawal();
    expect(calls.find(call => call.url.endsWith('/withdrawal'))!.body).toEqual({ reason: null });
    await c.download(c.history[0], 'invoice');
    expect(saved).toEqual(['fatura-EDU-7K4Q2M.pdf']);
  });

  it('says so when the page cannot be loaded, and that nothing is needed when the product asks for no subscription', async () => {
    const broken = controller({ 'GET /api/billing/v1/me': new Error('x') });
    await broken.c.load();
    expect([broken.c.failed, broken.c.loading]).toEqual([true, false]);
    const open = controller({ 'GET /api/billing/v1/me': me({ requireSubscription: false }) });
    await open.c.load();
    expect(open.c.open).toBe(true);
  });

  it('renders in the application with its own session: plans, the payment code and the bank account', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: { userProfile: signal({ firstName: 'Deneme', lastName: 'Öğrenci', roles: ['Student'] }) } }]
    });
    const fixture = TestBed.createComponent(MySubscriptionComponent);
    fixture.componentRef.setInput('product', 'coaching');
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/billing/v1/me?product=coaching').flush(me({ pendingOrders: [order()] }));
    http.expectOne('/api/billing/v1/catalog/coaching').flush(catalog([plan(), plan({ id: 'p2', name: 'TEST Yıllık', durationDays: 365, price: 2000, trialDays: 7 })]));
    http.expectOne('/api/billing/v1/orders').flush([order()]);
    http.expectOne('/api/billing/v1/organizations').flush([]);
    await new Promise(resolve => setTimeout(resolve));
    await fixture.whenStable();
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Etkin bir aboneliğin yok.');
    expect(text).toContain('EDU-7K4Q2M');
    expect(text).toContain('TR33 0006 1005 1978 6457 8413 26');
    expect(text).toContain('TEST Yıllık');
    expect(text).toContain('7 gün ücretsiz dene');
    expect(text).toContain('Bu plan için bekleyen bir siparişin var.');
    expect(fixture.componentInstance.c.buyerName).toBe('Deneme Öğrenci');
    http.verify();
  });

  it('KocApp uyumu: sells teacher plans as one package chosen by students and period, everything in every package', async () => {
    const all = ['Otomatik plan', 'Puan tahmini', 'Veli paneli'];
    const tier = (id: string, seatCount: number, durationDays: number, price: number, features = all) =>
      plan({ id, name: `TEST ${seatCount} öğrenci`, buyerType: 'Teacher', seatCount, durationDays, price, extraSeatPrice: 100, features, trialDays: 14 });
    const plans = [tier('t3a', 3, 90, 897), tier('t3y', 3, 365, 2990), tier('t5a', 5, 90, 1497, [...all, 'TEST fazla']), tier('t50y', 50, 365, 24990),
      plan({ id: 'kurum', buyerType: 'Teacher', seatCount: 100, isContactOnly: true }), plan({ id: 'bireysel' })];
    expect(billingTeacherTiers(plans).map(x => x.id)).toEqual(['t3a', 't3y', 't5a', 't50y']);
    expect(billingTeacherTiers([plans[0]])).toEqual([]);
    expect(billingCommonFeatures(billingTeacherTiers(plans))).toEqual(all);
    expect(billingMonthly(plans[0])).toBe(299);

    const { c } = controller({ 'GET /api/billing/v1/catalog/coaching': catalog(plans) }, ['Teacher']);
    await c.load();
    expect(c.otherPlans.map(x => x.id)).toEqual(['kurum', 'bireysel']);
    expect(c.tierSeatOptions).toEqual([3, 5, 50]);
    expect(c.tierDayOptions).toEqual([90, 365]);
    expect(c.tierPlan!.id).toBe('t3a');
    expect(c.monthly(c.tierPlan!)).toMatch(/^299/);
    c.chooseTierDays(365);
    expect(c.tierPlan!.id).toBe('t3y');
    // 5 students are sold for 3 months only: the year is off and the package falls back to 3 months.
    c.chooseTierSeats(5);
    expect([c.tierSold(365), c.tierSold(90), c.tierPlan!.id]).toEqual([false, true, 't5a']);
    c.chooseTierSeats(50);
    expect(c.tierPlan!.id).toBe('t50y');
    expect(c.planUnit(c.tierPlan!)).toBe('öğrenci');
    expect(c.t.tierExtra('100,00 TL')).toContain('ek öğrenci 100,00 TL');
  });

  it('KocApp uyumu: a teacher counts the own pool in students and buys extra students', async () => {
    const sub = { id: 's1', planId: 't3a', planName: 'TEST 3 öğrenci', productCodes: ['coaching'], status: 'Active' as const,
      startsAt: '2026-11-01T00:00:00Z', endsAt: '2027-02-01T00:00:00Z', isCurrent: true, seatCount: 3 };
    const { c, calls } = controller({
      'GET /api/billing/v1/me': me({ subscriptions: [sub] }),
      'GET /api/billing/v1/subscriptions/s1/seats': { subscriptionId: 's1', planName: 'TEST 3 öğrenci', endsAt: sub.endsAt, capacity: 3, used: 3, extraSeats: 0,
        extraSeatsUntil: null, extraSeatPrice: 100, seats: [] },
      'GET /api/billing/v1/subscriptions/s1/seat-quote': { count: 1, amount: 80, currency: 'TRY', daysLeft: 72, documents },
      'POST /api/billing/v1/subscriptions/s1/seat-orders': order({ kind: 'ExtraSeats', extraSeats: 1, buyerType: 'Teacher', paymentCode: 'EDU-AAAAAA' })
    }, ['Teacher']);
    await c.load();
    expect(c.seatUnit(sub)).toBe('öğrenci');
    await c.openSeats(sub);
    expect(c.poolUnit).toBe('öğrenci');
    c.setSeatCount('0');
    await c.quoteSeats();
    expect(c.error).toBe('Ek öğrenci sayısı en az 1 olmalı.');
    c.setSeatCount('1');
    await c.quoteSeats();
    c.acceptSeats(true);
    await c.orderSeats();
    expect(calls.find(call => call.url.endsWith('/seat-orders'))!.body).toEqual({ count: 1, documentsVersion: '2026-11-15', buyerName: 'Deneme Öğrenci' });
    expect(c.notice).toBe('Ek öğrenci siparişin oluşturuldu. Havale açıklamasına EDU-AAAAAA kodunu yaz.');
  });
});
