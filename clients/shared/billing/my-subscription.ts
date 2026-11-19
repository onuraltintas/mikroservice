// "Aboneliğim": the one subscription and payment page every product shows (billing service, stage 6).
// Framework-free on purpose: this folder is compiled by each client application, which wraps the controller, the
// template and the styles below in a thin component of its own.
import { trMoney, trNumber } from '../utils/tr-number';

export type BuyerType = 'Individual' | 'Teacher' | 'Institution';
export type OrderStatus = 'Pending' | 'Paid' | 'Rejected' | 'Expired' | 'Withdrawn' | 'Cancelled';
export type WithdrawalStatus = 'Requested' | 'Approved' | 'Rejected';

export interface BillingPlan {
  id: string; name: string; description: string; buyerType: BuyerType; price: number; currency: string; durationDays: number;
  trialDays: number; seatCount: number | null; extraSeatPrice: number | null; isContactOnly: boolean; features: string[];
  productCodes: string[];
}
export interface BillingBankAccount { bankName: string; accountHolder: string; iban: string; ibanFormatted: string; instructions: string | null }
export interface BillingCatalog { productCode: string; productName: string; requireSubscription: boolean; salesOpen: boolean; plans: BillingPlan[]; bankAccounts: BillingBankAccount[] }
export interface BillingSubscription {
  id: string; planId: string; planName: string; productCodes: string[]; status: 'Active' | 'Cancelled'; startsAt: string; endsAt: string;
  isCurrent: boolean; seatCount: number | null;
}
export interface BillingOrder {
  id: string; planId: string; planName: string; productCodes: string[]; buyerType: BuyerType; amount: number; currency: string;
  durationDays: number; paymentCode: string; status: OrderStatus; createdAt: string; expiresAt: string; paidDeclaredAt: string | null;
  payerNote: string | null; hasReceipt: boolean; reviewedAt: string | null; reviewNote: string | null; documentsVersion: string;
  kind: 'Subscription' | 'ExtraSeats'; extraSeats: number | null; listPrice: number; discount: number; couponCode: string | null;
  withdrawal: WithdrawalStatus | null; withdrawUntil: string | null; hasInvoice: boolean;
}
export interface MyBilling {
  productCode: string; productName: string; requireSubscription: boolean; salesOpen: boolean;
  entitlements: { key: string; limitValue: number | null; endsAt: string }[]; subscriptions: BillingSubscription[]; pendingOrders: BillingOrder[];
}
export interface BillingOrganization { id: string; name: string; subscriptions: BillingSubscription[]; pendingOrders: BillingOrder[] }
export interface SalesDocuments { version: string; preInformation: string; distanceSales: string }
export interface CouponQuote { code: string; listPrice: number; discount: number; amount: number; currency: string }
export interface SeatPool {
  subscriptionId: string; planName: string; endsAt: string; capacity: number; used: number; extraSeats: number; extraSeatsUntil: string | null;
  extraSeatPrice: number | null; seats: { userId: string; isSuspended: boolean; assignedAt: string; isCovered: boolean }[];
}
export interface SeatQuote { count: number; amount: number; currency: string; daysLeft: number; documents: SalesDocuments }

/** What the page needs from the application: its own HTTP client (with the session) and a way to hand a file over. */
export interface BillingHttp {
  get<T>(url: string): Promise<T>;
  post<T>(url: string, body: unknown): Promise<T>;
  delete<T>(url: string): Promise<T>;
  blob(url: string): Promise<Blob>;
}

export interface MySubscriptionOptions {
  /** Product code in the billing catalog: coaching, speed-reading, course-management. */
  product: string;
  /** The application's API root as its environment gives it (for example /api). */
  apiUrl: string;
  /** The signed-in user's roles; a teacher plan is shown to teachers only. */
  roles: readonly string[];
  /** The user's name for the contract; the session token carries none. */
  buyerName: string;
  /** Address the reader with "sen" (default) or "siz". */
  voice?: BillingVoice;
  /** Offer individual (student) plans; staff sites set false. Default true. */
  individualPlans?: boolean;
  /** Called after every change of state, so the host can refresh its view. */
  changed: () => void;
  /** Hands a downloaded file to the browser. */
  save: (blob: Blob, fileName: string) => void;
}

export const BILLING_MAX_RECEIPT_BYTES = 5 * 1024 * 1024;
export const BILLING_RECEIPT_TYPES = ['application/pdf', 'image/jpeg', 'image/png'];

/** How the page addresses the reader: "sen" on student and parent sites, "siz" on staff and management sites. */
export type BillingVoice = 'sen' | 'siz';

export interface BillingTexts {
  loading: string; loadFailed: string; open: string; noActive: string; nameLabel: string; writeCode: string;
  paidPrefix: string; paidSuffix: string; docsHint: string; contactPlan: string; pendingPlan: string; activePlan: string;
  withdrawPrefix: string; withdrawSuffix: string; tryFree: string; trialName: string; contractName: string;
  paidNotice: string; cancelledNotice: string; withdrawalNotice: string; retry: string;
  orderNotice: (code: string) => string; seatOrderNotice: (code: string) => string; trialNotice: (days: number) => string;
  withdrawal: Record<WithdrawalStatus, string>;
}

export const BILLING_TEXTS: Record<BillingVoice, BillingTexts> = {
  sen: {
    loading: '{{ c.t.loading }}', loadFailed: 'Abonelik bilgilerin yüklenemedi.',
    open: 'Bu ürün şu anda abonelik gerektirmiyor; hesabınla kullanabilirsin.', noActive: 'Etkin bir aboneliğin yok.',
    nameLabel: 'Adın ve soyadın (sözleşmede yer alır)', writeCode: 'yaz',
    paidPrefix: 'Ödemeni', paidSuffix: 'tarihinde bildirdin.', docsHint: 'Sözleşme metinleri için adını ve soyadını yaz.',
    contactPlan: 'Bu plan için bizimle iletişime geç.', pendingPlan: 'Bu plan için bekleyen bir siparişin var.', activePlan: 'Bu planda aboneliğin etkin.',
    withdrawPrefix: 'Cayma hakkını', withdrawSuffix: 'tarihine kadar kullanabilirsin. Talebin kabul edilirse aboneliğin sona erer ve ödediğin tutar iade edilir.',
    tryFree: 'gün ücretsiz dene', trialName: 'Ücretsiz deneme için adını ve soyadını yaz.', contractName: 'Sözleşme için adını ve soyadını yaz.',
    paidNotice: 'Ödeme bildirimin alındı. Onaylandığında aboneliğin başlar ve sana haber veririz.', cancelledNotice: 'Siparişten vazgeçtin.',
    withdrawalNotice: 'Cayma talebin alındı. Sonucu e-postayla bildireceğiz.', retry: 'İşlem tamamlanamadı. Lütfen tekrar dene.',
    orderNotice: code => `Siparişin oluşturuldu. Havale açıklamasına ${code} kodunu yaz.`,
    seatOrderNotice: code => `Ek koltuk siparişin oluşturuldu. Havale açıklamasına ${code} kodunu yaz.`,
    trialNotice: days => `${days} günlük ücretsiz denemen başladı.`,
    withdrawal: { Requested: 'Cayma talebin inceleniyor', Approved: 'Cayma talebin kabul edildi', Rejected: 'Cayma talebin reddedildi' }
  },
  siz: {
    loading: 'Abonelik bilgileriniz yükleniyor…', loadFailed: 'Abonelik bilgileriniz yüklenemedi.',
    open: 'Bu ürün şu anda abonelik gerektirmiyor; hesabınızla kullanabilirsiniz.', noActive: 'Etkin bir aboneliğiniz yok.',
    nameLabel: 'Adınız ve soyadınız (sözleşmede yer alır)', writeCode: 'yazın',
    paidPrefix: 'Ödemenizi', paidSuffix: 'tarihinde bildirdiniz.', docsHint: 'Sözleşme metinleri için adınızı ve soyadınızı yazın.',
    contactPlan: 'Bu plan için bizimle iletişime geçin.', pendingPlan: 'Bu plan için bekleyen bir siparişiniz var.', activePlan: 'Bu planda aboneliğiniz etkin.',
    withdrawPrefix: 'Cayma hakkınızı', withdrawSuffix: 'tarihine kadar kullanabilirsiniz. Talebiniz kabul edilirse aboneliğiniz sona erer ve ödediğiniz tutar iade edilir.',
    tryFree: 'gün ücretsiz deneyin', trialName: 'Ücretsiz deneme için adınızı ve soyadınızı yazın.', contractName: 'Sözleşme için adınızı ve soyadınızı yazın.',
    paidNotice: 'Ödeme bildiriminiz alındı. Onaylandığında aboneliğiniz başlar ve size haber veririz.', cancelledNotice: 'Siparişten vazgeçtiniz.',
    withdrawalNotice: 'Cayma talebiniz alındı. Sonucu e-postayla bildireceğiz.', retry: 'İşlem tamamlanamadı. Lütfen tekrar deneyin.',
    orderNotice: code => `Siparişiniz oluşturuldu. Havale açıklamasına ${code} kodunu yazın.`,
    seatOrderNotice: code => `Ek koltuk siparişiniz oluşturuldu. Havale açıklamasına ${code} kodunu yazın.`,
    trialNotice: days => `${days} günlük ücretsiz denemeniz başladı.`,
    withdrawal: { Requested: 'Cayma talebiniz inceleniyor', Approved: 'Cayma talebiniz kabul edildi', Rejected: 'Cayma talebiniz reddedildi' }
  }
};

const ORDER_STATUS: Record<OrderStatus, string> = {
  Pending: 'Ödeme bekleniyor', Paid: 'Ödendi', Rejected: 'Onaylanmadı', Expired: 'Süresi doldu', Withdrawn: 'Vazgeçildi', Cancelled: 'İptal edildi'
};

export function billingOrderStatus(order: Pick<BillingOrder, 'status' | 'paidDeclaredAt'>): string {
  return order.status === 'Pending' && order.paidDeclaredAt ? 'Ödeme bildirildi, onay bekleniyor' : ORDER_STATUS[order.status] ?? order.status;
}
export function billingWithdrawalStatus(status: WithdrawalStatus, voice: BillingVoice = 'sen'): string { return BILLING_TEXTS[voice].withdrawal[status] ?? status; }
export function billingMoney(value: number, currency = 'TRY'): string {
  return currency === 'TRY' ? trMoney(value) : `${trNumber(value, 2, 2)} ${currency}`;
}
export function billingDate(value: string | null | undefined): string {
  if (!value) return '';
  return new Intl.DateTimeFormat('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'Europe/Istanbul' }).format(new Date(value));
}
export function billingDuration(days: number): string {
  if (days % 365 === 0) return days === 365 ? '1 yıl' : `${days / 365} yıl`;
  if (days % 30 === 0) return days === 30 ? '1 ay' : `${days / 30} ay`;
  return `${days} gün`;
}
/** The server's own sentence when it gave one (problem details), otherwise a general one. */
export function billingError(error: unknown, fallback = 'İşlem tamamlanamadı. Lütfen tekrar dene.'): string {
  const body = (error as { error?: { detail?: unknown } } | null)?.error;
  return typeof body?.detail === 'string' && body.detail.trim() ? body.detail : fallback;
}
/** Plans this user may order here: individual for everybody, teacher plans for teachers, institution plans for whoever administers one. */
export function billingPlansFor(plans: readonly BillingPlan[], roles: readonly string[], administersInstitution: boolean, individualPlans = true): BillingPlan[] {
  const teacher = roles.includes('Teacher');
  return plans.filter(plan => (plan.buyerType === 'Individual' && individualPlans) || (plan.buyerType === 'Teacher' && teacher) || (plan.buyerType === 'Institution' && administersInstitution));
}
export function billingReceiptProblem(file: { size: number; type: string } | null): string | null {
  if (!file) return null;
  if (!BILLING_RECEIPT_TYPES.includes(file.type)) return 'Dekont PDF, JPG ya da PNG olmalı.';
  return file.size > BILLING_MAX_RECEIPT_BYTES ? 'Dekont en fazla 5 MB olabilir.' : null;
}

/** How long the buyer's name must stay unchanged before the sales documents are rebuilt with it. */
export const BILLING_NAME_SETTLE_MS = 600;

export class MySubscriptionController {
  loading = true;
  failed = false;
  busy = false;
  error: string | null = null;
  notice: string | null = null;
  me: MyBilling | null = null;
  catalog: BillingCatalog | null = null;
  orders: BillingOrder[] = [];
  organizations: BillingOrganization[] = [];

  // Buying a plan.
  plan: BillingPlan | null = null;
  organizationId: string | null = null;
  buyerName: string;
  couponInput = '';
  quote: CouponQuote | null = null;
  documents: SalesDocuments | null = null;
  shownDocument: 'pre' | 'sales' | null = null;
  accepted = false;

  // "I have paid".
  payingOrderId: string | null = null;
  payerNote = '';
  receipt: File | null = null;

  // Withdrawal (cayma).
  withdrawingOrderId: string | null = null;
  withdrawalReason = '';

  // Seats of a teacher or institution subscription.
  pool: SeatPool | null = null;
  seatCount = 1;
  seatQuote: SeatQuote | null = null;
  seatAccepted = false;

  copied: string | null = null;

  constructor(private readonly http: BillingHttp, private readonly options: MySubscriptionOptions) {
    this.buyerName = options.buyerName;
    this.t = BILLING_TEXTS[options.voice ?? 'sen'];
  }

  /** The page's wording in the host's voice. */
  readonly t: BillingTexts;

  private get base(): string { return `${this.options.apiUrl}/billing/v1`; }

  async load(): Promise<void> {
    this.loading = true;
    this.failed = false;
    this.options.changed();
    try {
      const product = encodeURIComponent(this.options.product);
      const [me, catalog, orders, organizations] = await Promise.all([
        this.http.get<MyBilling>(`${this.base}/me?product=${product}`),
        this.http.get<BillingCatalog>(`${this.base}/catalog/${product}`),
        this.http.get<BillingOrder[]>(`${this.base}/orders`),
        this.http.get<BillingOrganization[]>(`${this.base}/organizations`).catch(() => [] as BillingOrganization[])
      ]);
      this.me = me;
      this.catalog = catalog;
      this.orders = orders.filter(order => order.productCodes.includes(this.options.product));
      this.organizations = organizations;
    } catch {
      this.failed = true;
    } finally {
      this.loading = false;
      this.options.changed();
    }
  }

  /** The product asks for no subscription: nothing to buy, nothing to show but that. */
  get open(): boolean { return this.me !== null && !this.me.requireSubscription; }
  get current(): BillingSubscription[] { return this.subscriptions.filter(item => item.isCurrent); }
  get subscriptions(): BillingSubscription[] {
    return [...(this.me?.subscriptions ?? []), ...this.organizations.flatMap(item => item.subscriptions.filter(sub => sub.productCodes.includes(this.options.product)))];
  }
  get pending(): BillingOrder[] {
    const own = this.me?.pendingOrders ?? [];
    const institution = this.organizations.flatMap(item => item.pendingOrders.filter(order => order.productCodes.includes(this.options.product)));
    return [...own, ...institution.filter(order => !own.some(item => item.id === order.id))];
  }
  get history(): BillingOrder[] { return this.orders.filter(order => order.status !== 'Pending'); }
  get plans(): BillingPlan[] { return billingPlansFor(this.catalog?.plans ?? [], this.options.roles, this.organizations.length > 0, this.options.individualPlans ?? true); }
  get canBuy(): boolean { return !!this.me?.salesOpen && !!this.catalog?.salesOpen; }
  get bankAccounts(): BillingBankAccount[] { return this.catalog?.bankAccounts ?? []; }
  get price(): number { return this.quote?.amount ?? this.plan?.price ?? 0; }
  organizationName(subscription: BillingSubscription): string | null {
    return this.organizations.find(item => item.subscriptions.some(sub => sub.id === subscription.id))?.name ?? null;
  }
  /** Live test 19.11.2026 (A14): an active subscription for this plan offers no second purchase. */
  hasActiveFor(plan: BillingPlan, now = Date.now()): boolean {
    return this.subscriptions.some(item => item.planId === plan.id && item.status === 'Active' && new Date(item.endsAt).getTime() > now);
  }
  hasPendingFor(plan: BillingPlan): boolean { return this.pending.some(order => order.planId === plan.id && order.kind === 'Subscription'); }
  canWithdraw(order: BillingOrder): boolean {
    return order.status === 'Paid' && order.withdrawal === null && !!order.withdrawUntil && new Date(order.withdrawUntil).getTime() > Date.now();
  }

  choose(plan: BillingPlan): void {
    this.plan = plan;
    this.organizationId = plan.buyerType === 'Institution' ? this.organizations[0]?.id ?? null : null;
    this.couponInput = '';
    this.quote = null;
    this.documents = null;
    this.shownDocument = null;
    this.accepted = false;
    this.error = null;
    this.notice = null;
    this.options.changed();
    void this.loadDocuments();
  }

  cancelPurchase(): void {
    this.plan = null;
    this.documents = null;
    this.quote = null;
    this.error = null;
    this.options.changed();
  }

  setBuyerName(value: string): void {
    this.buyerName = value;
    // The contract carries the name: what was read is no longer what will be accepted.
    this.documents = null;
    this.accepted = false;
    this.seatQuote = null;
    this.seatAccepted = false;
    this.options.changed();
    // Reload the documents shortly after typing stops, so the acceptance box comes back without leaving the field.
    if (this.documentsTimer) clearTimeout(this.documentsTimer);
    this.documentsTimer = setTimeout(() => { this.documentsTimer = null; void this.loadDocuments(); }, BILLING_NAME_SETTLE_MS);
  }

  private documentsTimer: ReturnType<typeof setTimeout> | null = null;

  async loadDocuments(): Promise<void> {
    if (this.documentsTimer) { clearTimeout(this.documentsTimer); this.documentsTimer = null; }
    if (!this.plan || this.buyerName.trim().length < 3) return;
    const plan = this.plan;
    try {
      const coupon = this.quote ? `&coupon=${encodeURIComponent(this.quote.code)}` : '';
      const documents = await this.http.get<SalesDocuments>(
        `${this.base}/documents?planId=${plan.id}&buyerName=${encodeURIComponent(this.buyerName.trim())}${coupon}`);
      if (this.plan !== plan) return;
      this.documents = documents;
      this.accepted = false;
    } catch (error) {
      this.error = billingError(error, 'Sözleşme metinleri yüklenemedi.');
    } finally {
      this.options.changed();
    }
  }

  async applyCoupon(): Promise<void> {
    if (!this.plan || !this.couponInput.trim()) return;
    await this.run(async () => {
      this.quote = await this.http.post<CouponQuote>(`${this.base}/coupons/quote`, { planId: this.plan!.id, code: this.couponInput.trim() });
      await this.loadDocuments();
    });
  }

  async removeCoupon(): Promise<void> {
    this.quote = null;
    this.couponInput = '';
    await this.loadDocuments();
  }

  get canOrder(): boolean {
    return !!this.plan && !!this.documents && this.accepted && this.buyerName.trim().length >= 3 && !this.busy
      && (this.plan.buyerType !== 'Institution' || !!this.organizationId);
  }

  async order(): Promise<void> {
    if (!this.canOrder) return;
    await this.run(async () => {
      const order = await this.http.post<BillingOrder>(`${this.base}/orders`, {
        planId: this.plan!.id, documentsVersion: this.documents!.version, buyerName: this.buyerName.trim(),
        organizationId: this.organizationId, couponCode: this.quote?.code ?? null
      });
      this.plan = null;
      this.documents = null;
      this.quote = null;
      this.notice = this.t.orderNotice(order.paymentCode);
      await this.load();
    });
  }

  async startTrial(plan: BillingPlan): Promise<void> {
    if (this.buyerName.trim().length < 3) { this.error = this.t.trialName; this.options.changed(); return; }
    await this.run(async () => {
      await this.http.post(`${this.base}/trials`, { planId: plan.id, buyerName: this.buyerName.trim() });
      this.notice = this.t.trialNotice(plan.trialDays);
      await this.load();
    });
  }

  beginPaid(order: BillingOrder): void {
    this.payingOrderId = order.id;
    this.payerNote = '';
    this.receipt = null;
    this.error = null;
    this.options.changed();
  }

  setReceipt(file: File | null): void {
    const problem = billingReceiptProblem(file);
    this.error = problem;
    this.receipt = problem ? null : file;
    this.options.changed();
  }

  async declarePaid(): Promise<void> {
    const id = this.payingOrderId;
    if (!id) return;
    await this.run(async () => {
      const form = new FormData();
      form.append('note', this.payerNote.trim());
      if (this.receipt) form.append('receipt', this.receipt, this.receipt.name);
      await this.http.post(`${this.base}/orders/${id}/paid`, form);
      this.payingOrderId = null;
      this.notice = this.t.paidNotice;
      await this.load();
    });
  }

  async giveUp(order: BillingOrder): Promise<void> {
    await this.run(async () => {
      await this.http.delete(`${this.base}/orders/${order.id}`);
      this.notice = this.t.cancelledNotice;
      await this.load();
    });
  }

  beginWithdrawal(order: BillingOrder): void {
    this.withdrawingOrderId = order.id;
    this.withdrawalReason = '';
    this.error = null;
    this.options.changed();
  }

  async requestWithdrawal(): Promise<void> {
    const id = this.withdrawingOrderId;
    if (!id) return;
    await this.run(async () => {
      await this.http.post(`${this.base}/orders/${id}/withdrawal`, { reason: this.withdrawalReason.trim() || null });
      this.withdrawingOrderId = null;
      this.notice = this.t.withdrawalNotice;
      await this.load();
    });
  }

  async download(order: BillingOrder, what: 'invoice' | 'receipt'): Promise<void> {
    await this.run(async () => {
      const blob = await this.http.blob(`${this.base}/orders/${order.id}/${what}`);
      const extension = blob.type === 'application/pdf' ? 'pdf' : blob.type === 'image/png' ? 'png' : blob.type === 'image/jpeg' ? 'jpg' : 'bin';
      this.options.save(blob, `${what === 'invoice' ? 'fatura' : 'dekont'}-${order.paymentCode}.${extension}`);
    });
  }

  async showAccepted(order: BillingOrder): Promise<void> {
    await this.run(async () => {
      const documents = await this.http.get<SalesDocuments>(`${this.base}/orders/${order.id}/documents`);
      this.options.save(new Blob([`${documents.preInformation}\n\n\n${documents.distanceSales}`], { type: 'text/plain;charset=utf-8' }),
        `sozlesme-${order.paymentCode}.txt`);
    });
  }

  async openSeats(subscription: BillingSubscription): Promise<void> {
    await this.run(async () => {
      this.pool = await this.http.get<SeatPool>(`${this.base}/subscriptions/${subscription.id}/seats`);
      this.seatCount = 1;
      this.seatQuote = null;
      this.seatAccepted = false;
    });
  }

  closeSeats(): void { this.pool = null; this.seatQuote = null; this.options.changed(); }

  async quoteSeats(): Promise<void> {
    if (!this.pool) return;
    if (this.buyerName.trim().length < 3) { this.error = this.t.contractName; this.options.changed(); return; }
    if (!(this.seatCount >= 1)) { this.error = 'Ek koltuk sayısı en az 1 olmalı.'; this.options.changed(); return; }
    await this.run(async () => {
      this.seatQuote = await this.http.get<SeatQuote>(
        `${this.base}/subscriptions/${this.pool!.subscriptionId}/seat-quote?count=${Math.floor(this.seatCount)}&buyerName=${encodeURIComponent(this.buyerName.trim())}`);
      this.seatAccepted = false;
    });
  }

  async orderSeats(): Promise<void> {
    if (!this.pool || !this.seatQuote || !this.seatAccepted) return;
    await this.run(async () => {
      const order = await this.http.post<BillingOrder>(`${this.base}/subscriptions/${this.pool!.subscriptionId}/seat-orders`,
        { count: this.seatQuote!.count, documentsVersion: this.seatQuote!.documents.version, buyerName: this.buyerName.trim() });
      this.pool = null;
      this.seatQuote = null;
      this.notice = this.t.seatOrderNotice(order.paymentCode);
      await this.load();
    });
  }

  async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.copied = text;
    } catch {
      this.copied = null;
    }
    this.options.changed();
  }

  set<K extends 'couponInput' | 'payerNote' | 'withdrawalReason' | 'organizationId'>(key: K, value: string): void {
    (this as Record<string, unknown>)[key] = value;
    this.options.changed();
  }
  setSeatCount(value: string): void { this.seatCount = Number(value); this.seatQuote = null; this.options.changed(); }
  accept(value: boolean): void { this.accepted = value; this.options.changed(); }
  acceptSeats(value: boolean): void { this.seatAccepted = value; this.options.changed(); }
  show(document: 'pre' | 'sales' | null): void { this.shownDocument = this.shownDocument === document ? null : document; this.options.changed(); }

  money = billingMoney;
  date = billingDate;
  duration = billingDuration;
  orderStatus = billingOrderStatus;
  withdrawalStatus(status: WithdrawalStatus): string { return billingWithdrawalStatus(status, this.options.voice ?? 'sen'); }

  private async run(work: () => Promise<void>): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    this.error = null;
    this.notice = null;
    this.options.changed();
    try {
      await work();
    } catch (error) {
      this.error = billingError(error, this.t.retry);
    } finally {
      this.busy = false;
      this.options.changed();
    }
  }
}

export const MY_SUBSCRIPTION_TEMPLATE = `
<section class="bill" aria-live="polite">
  @if (c.loading) {
    <p class="bill-muted" role="status">Abonelik bilgilerin yükleniyor…</p>
  } @else if (c.failed) {
    <div class="bill-alert bill-alert--error" role="alert">
      {{ c.t.loadFailed }}
      <button type="button" class="bill-btn" (click)="c.load()">Tekrar dene</button>
    </div>
  } @else if (c.open) {
    <div class="bill-card"><h2>{{ c.me!.productName }}</h2><p>{{ c.t.open }}</p></div>
  } @else {
    @if (c.notice) { <div class="bill-alert bill-alert--ok" role="status">{{ c.notice }}</div> }
    @if (c.error) { <div class="bill-alert bill-alert--error" role="alert">{{ c.error }}</div> }

    <div class="bill-card">
      <h2>Aboneliğim</h2>
      @if (c.current.length === 0) {
        <p class="bill-muted">{{ c.t.noActive }}</p>
      }
      @for (item of c.subscriptions; track item.id) {
        <div class="bill-row">
          <div>
            <strong>{{ item.planName }}</strong>
            @if (c.organizationName(item); as name) { <span class="bill-tag">{{ name }}</span> }
            <span class="bill-tag" [class.bill-tag--ok]="item.isCurrent">{{ item.isCurrent ? 'Etkin' : 'Sona erdi' }}</span>
            <div class="bill-muted">{{ c.date(item.startsAt) }} – {{ c.date(item.endsAt) }}
              @if (item.seatCount) { · {{ item.seatCount }} koltuk }
            </div>
          </div>
          @if (item.seatCount && item.isCurrent) {
            <button type="button" class="bill-btn" [disabled]="c.busy" (click)="c.openSeats(item)">Koltuklar</button>
          }
        </div>
      }
    </div>

    @if (c.pool; as pool) {
      <div class="bill-card">
        <h2>Koltuklar: {{ pool.planName }}</h2>
        <p>{{ pool.used }} / {{ pool.capacity }} koltuk dolu.
          @if (pool.extraSeats > 0) { Bunun {{ pool.extraSeats }} tanesi ek koltuk ({{ c.date(pool.extraSeatsUntil) }} tarihine kadar). }
        </p>
        @if (pool.extraSeatPrice !== null) {
          <div class="bill-form">
            <label>{{ c.t.nameLabel }}
              <input type="text" maxlength="200" autocomplete="name" [value]="c.buyerName" (input)="c.setBuyerName($any($event.target).value)" />
            </label>
            <label>Ek koltuk sayısı
              <input type="number" min="1" [value]="c.seatCount" (input)="c.setSeatCount($any($event.target).value)" />
            </label>
            <button type="button" class="bill-btn" [disabled]="c.busy" (click)="c.quoteSeats()">Tutarı hesapla</button>
          </div>
          @if (c.seatQuote; as quote) {
            <p><strong>{{ c.money(quote.amount, quote.currency) }}</strong> · kalan {{ quote.daysLeft }} gün için {{ quote.count }} ek koltuk.</p>
            <details class="bill-doc"><summary>Ön bilgilendirme formu</summary><pre>{{ quote.documents.preInformation }}</pre></details>
            <details class="bill-doc"><summary>Mesafeli satış sözleşmesi</summary><pre>{{ quote.documents.distanceSales }}</pre></details>
            <label class="bill-check"><input type="checkbox" [checked]="c.seatAccepted" (change)="c.acceptSeats($any($event.target).checked)" />
              Ön bilgilendirme formunu ve mesafeli satış sözleşmesini okudum, kabul ediyorum.</label>
            <button type="button" class="bill-btn bill-btn--primary" [disabled]="c.busy || !c.seatAccepted" (click)="c.orderSeats()">Havale ile öde</button>
          }
        } @else {
          <p class="bill-muted">Bu planda ek koltuk satılmıyor.</p>
        }
        <button type="button" class="bill-btn" (click)="c.closeSeats()">Kapat</button>
      </div>
    }

    @for (order of c.pending; track order.id) {
      <div class="bill-card bill-card--pending">
        <h2>{{ c.orderStatus(order) }}: {{ order.planName }}</h2>
        <dl class="bill-pay">
          <dt>Ödenecek tutar</dt>
          <dd><strong>{{ c.money(order.amount, order.currency) }}</strong>
            <button type="button" class="bill-copy" (click)="c.copy(order.amount.toFixed(2))">Kopyala</button></dd>
          <dt>Ödeme kodu</dt>
          <dd><strong class="bill-code">{{ order.paymentCode }}</strong>
            <button type="button" class="bill-copy" (click)="c.copy(order.paymentCode)">{{ c.copied === order.paymentCode ? 'Kopyalandı' : 'Kopyala' }}</button></dd>
        </dl>
        <p>Havale ya da EFT açıklamasına <strong>yalnız bu kodu</strong> {{ c.t.writeCode }}. Sipariş {{ c.date(order.expiresAt) }} tarihine kadar geçerli.</p>
        @for (account of c.bankAccounts; track account.iban) {
          <div class="bill-bank">
            <div><strong>{{ account.bankName }}</strong> · {{ account.accountHolder }}</div>
            <div><span class="bill-code">{{ account.ibanFormatted }}</span>
              <button type="button" class="bill-copy" (click)="c.copy(account.iban)">{{ c.copied === account.iban ? 'Kopyalandı' : 'Kopyala' }}</button></div>
            @if (account.instructions) { <div class="bill-muted">{{ account.instructions }}</div> }
          </div>
        }
        @if (c.payingOrderId === order.id) {
          <div class="bill-form">
            <label>Not (isteğe bağlı)
              <input type="text" maxlength="500" [value]="c.payerNote" (input)="c.set('payerNote', $any($event.target).value)" />
            </label>
            <label>Dekont (isteğe bağlı; PDF, JPG ya da PNG, en fazla 5 MB)
              <input type="file" accept="application/pdf,image/jpeg,image/png" (change)="c.setReceipt($any($event.target).files[0] || null)" />
            </label>
            <button type="button" class="bill-btn bill-btn--primary" [disabled]="c.busy" (click)="c.declarePaid()">Bildir</button>
            <button type="button" class="bill-btn" (click)="c.payingOrderId = null">İptal</button>
          </div>
        } @else {
          <div class="bill-actions">
            @if (!order.paidDeclaredAt) {
              <button type="button" class="bill-btn bill-btn--primary" [disabled]="c.busy" (click)="c.beginPaid(order)">Ödemeyi yaptım</button>
            } @else {
              <span class="bill-muted">{{ c.t.paidPrefix }} {{ c.date(order.paidDeclaredAt) }} {{ c.t.paidSuffix }}</span>
            }
            <button type="button" class="bill-btn" [disabled]="c.busy" (click)="c.giveUp(order)">Siparişten vazgeç</button>
          </div>
        }
      </div>
    }

    @if (c.plan; as plan) {
      <div class="bill-card">
        <h2>{{ plan.name }}</h2>
        <p>{{ c.duration(plan.durationDays) }} ·
          @if (c.quote; as quote) {
            <s>{{ c.money(quote.listPrice, quote.currency) }}</s> <strong>{{ c.money(quote.amount, quote.currency) }}</strong> ({{ quote.code }})
          } @else { <strong>{{ c.money(plan.price, plan.currency) }}</strong> }
        </p>
        <div class="bill-form">
          <label>{{ c.t.nameLabel }}
            <input type="text" maxlength="200" autocomplete="name" [value]="c.buyerName"
              (input)="c.setBuyerName($any($event.target).value)" (blur)="c.loadDocuments()" />
          </label>
          @if (plan.buyerType === 'Institution') {
            <label>Kurum
              <select (change)="c.set('organizationId', $any($event.target).value)">
                @for (organization of c.organizations; track organization.id) {
                  <option [value]="organization.id" [selected]="organization.id === c.organizationId">{{ organization.name }}</option>
                }
              </select>
            </label>
          }
          <label>Kupon kodu (varsa)
            <input type="text" maxlength="40" [value]="c.couponInput" (input)="c.set('couponInput', $any($event.target).value)" />
          </label>
          @if (c.quote) {
            <button type="button" class="bill-btn" [disabled]="c.busy" (click)="c.removeCoupon()">Kuponu kaldır</button>
          } @else {
            <button type="button" class="bill-btn" [disabled]="c.busy || !c.couponInput.trim()" (click)="c.applyCoupon()">Kuponu uygula</button>
          }
        </div>
        <p class="bill-muted">Havaleyi kim yaparsa yapsın, açıklamaya ödeme kodunun yazılması yeterlidir.</p>
        @if (c.documents; as documents) {
          <button type="button" class="bill-link" (click)="c.show('pre')">Ön bilgilendirme formu</button>
          <button type="button" class="bill-link" (click)="c.show('sales')">Mesafeli satış sözleşmesi</button>
          @if (c.shownDocument) {
            <pre class="bill-doc-text" tabindex="0">{{ c.shownDocument === 'pre' ? documents.preInformation : documents.distanceSales }}</pre>
          }
          <label class="bill-check"><input type="checkbox" [checked]="c.accepted" (change)="c.accept($any($event.target).checked)" />
            Ön bilgilendirme formunu ve mesafeli satış sözleşmesini okudum, kabul ediyorum.</label>
        } @else {
          <p class="bill-muted">{{ c.t.docsHint }}</p>
        }
        <div class="bill-actions">
          <button type="button" class="bill-btn bill-btn--primary" [disabled]="!c.canOrder" (click)="c.order()">Havale ile öde</button>
          <button type="button" class="bill-btn" (click)="c.cancelPurchase()">Vazgeç</button>
        </div>
      </div>
    } @else if (c.plans.length > 0) {
      <div class="bill-card">
        <h2>Planlar</h2>
        @if (!c.canBuy) { <p class="bill-muted">Satın alma şu anda kapalı.</p> }
        <div class="bill-plans">
          @for (plan of c.plans; track plan.id) {
            <article class="bill-plan">
              <h3>{{ plan.name }}</h3>
              @if (plan.productCodes.length > 1) { <span class="bill-tag bill-tag--ok">Paket</span> }
              <p class="bill-price">{{ plan.isContactOnly ? 'Teklif alın' : c.money(plan.price, plan.currency) }}
                <span class="bill-muted">/ {{ c.duration(plan.durationDays) }}</span></p>
              @if (plan.description) { <p>{{ plan.description }}</p> }
              @if (plan.seatCount) { <p class="bill-muted">{{ plan.seatCount }} koltuk</p> }
              <ul>@for (feature of plan.features; track feature) { <li>{{ feature }}</li> }</ul>
              @if (plan.isContactOnly) {
                <p class="bill-muted">{{ c.t.contactPlan }}</p>
              } @else if (c.hasActiveFor(plan)) {
                <p class="bill-muted">{{ c.t.activePlan }}</p>
              } @else if (c.hasPendingFor(plan)) {
                <p class="bill-muted">{{ c.t.pendingPlan }}</p>
              } @else {
                <button type="button" class="bill-btn bill-btn--primary" [disabled]="!c.canBuy || c.busy" (click)="c.choose(plan)">Satın al</button>
                @if (plan.trialDays > 0 && plan.buyerType !== 'Institution' && c.subscriptions.length === 0) {
                  <button type="button" class="bill-btn" [disabled]="!c.canBuy || c.busy" (click)="c.startTrial(plan)">{{ plan.trialDays }} {{ c.t.tryFree }}</button>
                }
              }
            </article>
          }
        </div>
      </div>
    }

    @if (c.history.length > 0) {
      <div class="bill-card">
        <h2>Ödeme geçmişi</h2>
        @for (order of c.history; track order.id) {
          <div class="bill-row">
            <div>
              <strong>{{ order.planName }}</strong>
              @if (order.kind === 'ExtraSeats') { <span class="bill-tag">{{ order.extraSeats }} ek koltuk</span> }
              <span class="bill-tag" [class.bill-tag--ok]="order.status === 'Paid'">{{ c.orderStatus(order) }}</span>
              <div class="bill-muted">{{ c.date(order.createdAt) }} · {{ c.money(order.amount, order.currency) }} · {{ order.paymentCode }}
                @if (order.couponCode) { · kupon {{ order.couponCode }} }
              </div>
              @if (order.reviewNote && order.status !== 'Paid') { <div class="bill-muted">Gerekçe: {{ order.reviewNote }}</div> }
              @if (order.withdrawal) { <div class="bill-muted">{{ c.withdrawalStatus(order.withdrawal) }}</div> }
            </div>
            <div class="bill-actions">
              <button type="button" class="bill-btn" [disabled]="c.busy" (click)="c.showAccepted(order)">Sözleşme</button>
              @if (order.hasInvoice) { <button type="button" class="bill-btn" [disabled]="c.busy" (click)="c.download(order, 'invoice')">Fatura</button> }
              @if (c.canWithdraw(order)) {
                <button type="button" class="bill-btn" [disabled]="c.busy" (click)="c.beginWithdrawal(order)">Cayma hakkını kullan</button>
              }
            </div>
          </div>
          @if (c.withdrawingOrderId === order.id) {
            <div class="bill-form">
              <p>{{ c.t.withdrawPrefix }} {{ c.date(order.withdrawUntil) }} {{ c.t.withdrawSuffix }}</p>
              <label>Gerekçe (isteğe bağlı)
                <input type="text" maxlength="500" [value]="c.withdrawalReason" (input)="c.set('withdrawalReason', $any($event.target).value)" />
              </label>
              <button type="button" class="bill-btn bill-btn--primary" [disabled]="c.busy" (click)="c.requestWithdrawal()">Cayma talebini gönder</button>
              <button type="button" class="bill-btn" (click)="c.withdrawingOrderId = null">İptal</button>
            </div>
          }
        }
      </div>
    }
  }
</section>
`;

export const MY_SUBSCRIPTION_STYLES = `
:host { display: block; }
.bill { display: grid; gap: 1rem; max-width: 60rem; }
.bill-card { border: 1px solid var(--bill-border, #d7dce5); border-radius: .75rem; padding: 1rem 1.25rem; background: var(--bill-surface, #fff); color: var(--bill-text, #1b2433); }
.bill-card--pending { border-color: var(--bill-accent, #1d4ed8); }
.bill-card h2 { margin: 0 0 .75rem; font-size: 1.125rem; }
.bill-card h3 { margin: 0 0 .25rem; font-size: 1rem; }
.bill-muted { color: var(--bill-muted, #5b6577); font-size: .875rem; }
.bill-row { display: flex; flex-wrap: wrap; gap: .75rem; justify-content: space-between; align-items: center; padding: .625rem 0; border-top: 1px solid var(--bill-border, #d7dce5); }
.bill-row:first-of-type { border-top: 0; }
.bill-tag { display: inline-block; margin-left: .375rem; padding: .0625rem .5rem; border-radius: 999px; font-size: .75rem; background: var(--bill-soft, #eef1f6); color: var(--bill-text, #36405a); }
.bill-tag--ok { background: #dcf5e6; color: #14532d; }
.bill-plan .bill-tag { justify-self: start; margin-left: 0; }
.bill-alert { padding: .75rem 1rem; border-radius: .5rem; }
.bill-alert--ok { background: #dcf5e6; color: #14532d; }
.bill-alert--error { background: #fde8e8; color: #7f1d1d; }
.bill-btn { min-height: 2.75rem; padding: .5rem 1rem; border-radius: .5rem; border: 1px solid var(--bill-border, #b9c1cf); background: var(--bill-surface, #fff); color: inherit; font: inherit; cursor: pointer; }
.bill-btn:disabled { opacity: .55; cursor: not-allowed; }
.bill-btn--primary { background: var(--bill-accent, #1d4ed8); border-color: var(--bill-accent, #1d4ed8); color: var(--bill-on-accent, #fff); }
.bill-btn:focus-visible, .bill-copy:focus-visible, .bill-link:focus-visible { outline: 3px solid #f59e0b; outline-offset: 2px; }
.bill-copy, .bill-link { min-height: 2.75rem; margin-left: .5rem; padding: .25rem .5rem; border: 0; background: none; color: var(--bill-accent, #1d4ed8); text-decoration: underline; font: inherit; cursor: pointer; }
.bill-link { margin-left: 0; margin-right: .75rem; }
.bill-actions { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; }
.bill-form { display: grid; gap: .75rem; margin: .75rem 0; max-width: 32rem; }
.bill-form label { display: grid; gap: .25rem; font-size: .875rem; }
.bill-form input, .bill-form select { min-height: 2.75rem; padding: .375rem .625rem; border: 1px solid var(--bill-border, #b9c1cf); border-radius: .5rem; font: inherit; }
.bill-check { display: flex; gap: .5rem; align-items: flex-start; margin: .75rem 0; }
.bill-check input { width: 1.25rem; height: 1.25rem; margin-top: .125rem; }
.bill-pay { display: grid; grid-template-columns: max-content 1fr; gap: .25rem 1rem; margin: 0 0 .5rem; align-items: center; }
.bill-pay dd { margin: 0; }
.bill-code { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; letter-spacing: .04em; overflow-wrap: anywhere; }
.bill-bank { padding: .5rem 0; border-top: 1px solid var(--bill-border, #d7dce5); }
.bill-plans { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr)); }
.bill-plan { border: 1px solid var(--bill-border, #d7dce5); border-radius: .625rem; padding: 1rem; display: grid; gap: .375rem; align-content: start; }
.bill-plan ul { margin: 0; padding-left: 1.125rem; }
.bill-price { font-size: 1.25rem; font-weight: 600; margin: 0; }
.bill-doc pre, .bill-doc-text { white-space: pre-wrap; max-height: 18rem; overflow: auto; padding: .75rem; border: 1px solid var(--bill-border, #d7dce5); border-radius: .5rem; font: inherit; font-size: .875rem; background: var(--bill-soft, #f7f8fb); }
@media (max-width: 30rem) { .bill-pay { grid-template-columns: 1fr; } .bill-btn { width: 100%; } }
`;
