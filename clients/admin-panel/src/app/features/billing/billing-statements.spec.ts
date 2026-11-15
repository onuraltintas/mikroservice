import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { BankTransaction, BillingAdminService, StatementPreview } from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { BillingStatementsComponent } from './billing-statements';

const account = { id: 'a1', bankName: 'Test Bankası', accountHolder: 'TEST', iban: 'TR000000000000000000000001', ibanFormatted: 'TR00 0000 0000 0000 0000 0000 01',
  instructions: null, statementTemplate: 'Halkbank', isActive: true, sortOrder: 1 };
const line = (id: string, status: BankTransaction['status'], extra: Partial<BankTransaction> = {}): BankTransaction => ({
  id, bankAccountId: 'a1', bankName: 'Test Bankası', bookedAt: '2026-11-14T21:00:00Z', amount: 200, description: 'HAVALE - TEST KİŞİ', sender: null,
  paymentCode: null, status, orderId: null, orderSummary: null, note: 'Açıklamada ödeme kodu yok.', ...extra });
const pending = (id: string, amount: number) => ({ order: { id, planId: 'p', planName: 'TEST Aylık', productCodes: ['coaching'], buyerType: 'Individual' as const, amount,
  currency: 'TRY', durationDays: 30, paymentCode: `EDU-${id}`, status: 'Pending' as const, createdAt: '', expiresAt: '', paidDeclaredAt: null, payerNote: null,
  hasReceipt: false, reviewedAt: null, reviewNote: null, documentsVersion: 'v' }, accountName: 'Deneme Öğrenci', accountEmail: null, orderedByUserId: 'u', beneficiaryUserId: 'u', reviewedBy: null });
const halkbank: StatementPreview = { totalRows: 12, suggested: { headerRow: 2, dateColumn: 0, descriptionColumn: 5, amountColumn: 2, senderColumn: null, referenceColumn: 1,
  directionColumn: 3, incomingValue: 'A' }, rows: [['IBAN No:', 'TR00'], [''], ['İŞLEM TARİHİ', 'BİM REF', 'TUTAR', 'B/A', 'BAKİYE', 'AÇIKLAMA'],
  ['15.11.2026', 'ref-1', '200,00', 'A', '1.000,00', 'HAVALE EDU-AAA111'], ['15.11.2026', 'ref-2', '16,76', 'B', '983,24', 'MASRAF']] };
const imported = { import: { id: 'i1', bankAccountId: 'a1', bankName: 'Test Bankası', fileName: 'ekstre.csv', uploadedAt: '2026-11-15T07:00:00Z', rowCount: 3, addedCount: 3,
  duplicateCount: 0, matchedCount: 1, autoApprovedCount: 1 }, transactions: [line('l1', 'Approved'), line('l2', 'Matched'), line('l3', 'Unmatched')] };

function setup(overrides: Record<string, unknown> = {}) {
  const billing = {
    getBankAccounts: vi.fn(() => of([account])),
    getBankTransactions: vi.fn(() => of([line('l3', 'Unmatched'), line('l2', 'Matched', { orderId: 'o9', orderSummary: 'Deneme Öğrenci · TEST Aylık · EDU-AAA111', note: null }),
      line('l4', 'Ignored', { amount: 9000 })])),
    getStatementImports: vi.fn(() => of([imported.import])),
    getOrders: vi.fn(() => of([pending('AAA111', 200), pending('BBB222', 500)])),
    previewStatement: vi.fn(() => of(halkbank)),
    importStatement: vi.fn(() => of(imported)),
    approveBankTransaction: vi.fn(() => of(line('l3', 'Approved'))),
    ignoreBankTransaction: vi.fn(() => of(line('l3', 'Ignored'))),
    restoreBankTransaction: vi.fn(() => of(line('l4', 'Unmatched'))),
    ...overrides
  };
  const toaster = { success: vi.fn() };
  TestBed.configureTestingModule({ imports: [BillingStatementsComponent], providers: [
    { provide: BillingAdminService, useValue: billing }, { provide: ToasterService, useValue: toaster }] });
  const fixture = TestBed.createComponent(BillingStatementsComponent);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, billing, toaster, element: fixture.nativeElement as HTMLElement };
}
const chooseFile = (component: BillingStatementsComponent, file = new File(['x'], 'ekstre.csv')) =>
  component.choose({ target: { files: [file] } } as unknown as Event);

describe('Abonelik ve Ödeme: bank statements', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('opens on the lines to look at and picks the only bank account', () => {
    const { component, billing } = setup();
    expect(billing.getBankTransactions).toHaveBeenCalledWith('Unmatched');
    expect(billing.getOrders).toHaveBeenCalledWith('Pending', '');
    expect(component.bankAccountId()).toBe('a1');
  });

  it('shows the recognised Halkbank columns before anything is stored, then uploads with them', () => {
    const { component, billing, element, fixture } = setup();
    const file = new File(['x'], 'ekstre.csv');
    chooseFile(component, file);
    fixture.detectChanges();
    expect(billing.previewStatement).toHaveBeenCalledWith(file);
    expect(billing.importStatement).not.toHaveBeenCalled();
    expect(element.querySelector('[data-testid="mapping-state"]')?.textContent).toContain('Sütunlar tanındı');
    expect(component.headers()).toEqual(['İŞLEM TARİHİ', 'BİM REF', 'TUTAR', 'B/A', 'BAKİYE', 'AÇIKLAMA']);
    expect(component.sampleRows().length).toBe(2);
    expect([0, 2, 3, 5].every(column => component.isPicked(column))).toBe(true);
    expect(component.isPicked(4)).toBe(false);

    component.upload();
    fixture.detectChanges();
    expect(billing.importStatement).toHaveBeenCalledWith(file, 'a1', expect.objectContaining({ headerRow: 2, amountColumn: 2, directionColumn: 3, incomingValue: 'A' }));
    const summary = element.querySelector('[data-testid="import-result"]')!.textContent!;
    expect(summary).toContain('3 yeni');
    expect(summary).toMatch(/1\s*kendiliğinden onaylandı/);
    expect(summary).toMatch(/1\s*onayınızı bekliyor/);
    expect(summary).toMatch(/1\s*eşleşmedi/);
    expect(component.preview()).toBeNull();
  });

  it('asks for the columns when they are not recognised and drops the direction value when there is no direction column', () => {
    const { component, billing, element, fixture } = setup({ previewStatement: vi.fn(() => of({ rows: [['a', 'b', 'c'], ['15.11.2026', 'x', '200']], totalRows: 2, suggested: null })) });
    chooseFile(component);
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="mapping-state"]')?.textContent).toContain('Sütunlar tanınamadı');
    component.mapping()!.incomingValue = 'A';
    component.upload();
    expect(billing.importStatement).toHaveBeenCalledWith(expect.anything(), 'a1', expect.objectContaining({ directionColumn: null, incomingValue: null }));
  });

  it('a refused file shows the server explanation', () => {
    const { component, element, fixture } = setup({ previewStatement: vi.fn(() => throwError(() =>
      new HttpErrorResponse({ status: 400, error: { detail: 'Eski Excel (.xls) dosyası okunamıyor; ekstreyi CSV ya da .xlsx olarak indirin.' } }))) });
    chooseFile(component);
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Eski Excel');
    expect(component.preview()).toBeNull();
  });

  it('offers only waiting orders of the same amount for a line without a code', () => {
    const { component, billing } = setup();
    const loose = component.lines()[0];
    expect(component.candidates(loose).map(order => order.order.id)).toEqual(['AAA111']);
    expect(component.candidates(line('x', 'Unmatched', { amount: 75 }))).toEqual([]);
    component.approve(loose, undefined);
    expect(billing.approveBankTransaction).not.toHaveBeenCalled();
    component.pick(loose.id, 'AAA111');
    component.approve(loose, component.picked()[loose.id]);
    expect(billing.approveBankTransaction).toHaveBeenCalledWith('l3', 'AAA111');
  });

  it('a matched line is approved with its own order, a line can be set aside and taken back', () => {
    const { component, billing, element } = setup();
    const matched = element.querySelector('[data-testid="line-l2"]')!;
    expect(matched.textContent).toContain('Deneme Öğrenci · TEST Aylık · EDU-AAA111');
    (matched.querySelector('.approve') as HTMLButtonElement).click();
    expect(billing.approveBankTransaction).toHaveBeenCalledWith('l2', 'o9');
    component.ignore(component.lines()[0]);
    expect(billing.ignoreBankTransaction).toHaveBeenCalledWith('l3');
    component.restore(component.lines()[2]);
    expect(billing.restoreBankTransaction).toHaveBeenCalledWith('l4');
  });
});
