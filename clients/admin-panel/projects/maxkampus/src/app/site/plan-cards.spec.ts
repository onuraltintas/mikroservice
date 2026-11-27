import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { BillingCatalogPlan, MaxkampusApi } from '../core/api';
import { SiteStore } from '../core/site-store';
import { PlanCardsComponent } from './plan-cards.component';

const tick = () => new Promise(resolve => setTimeout(resolve));

describe('maxkampüs plan cards (27.11.2026)', () => {
  const plan = (students: number, days: number, price: number, sortOrder: number): BillingCatalogPlan => ({
    id: `p-${students}-${days}`, name: `${students} öğrenci (${days === 30 ? 'aylık' : 'yıllık'})`, description: '', buyerType: 'Individual',
    price, currency: 'TRY', durationDays: days, seatCount: null, isContactOnly: false, isPublic: true, isActive: true, sortOrder,
    features: [`En çok ${students} etkin öğrenci`], vatRate: 20, priceWithVat: price * 1.2 });

  async function render(plans: BillingCatalogPlan[]) {
    TestBed.configureTestingModule({ imports: [PlanCardsComponent], providers: [provideRouter([]),
      { provide: MaxkampusApi, useValue: { catalog: vi.fn(async () => ({ plans })) } },
      { provide: SiteStore, useValue: { load: vi.fn(async () => null), home: () => ({ features: [{ id: 'f1', title: 'Yoklama', icon: 'check' }] }) } }] });
    const fixture = TestBed.createComponent(PlanCardsComponent);
    fixture.detectChanges();
    await tick();
    fixture.detectChanges();
    return fixture;
  }

  it('shows one card per student count, yearly first, with the monthly price per student and what a year saves', async () => {
    const fixture = await render([plan(10, 30, 500, 1), plan(10, 365, 5000, 2), plan(25, 30, 1250, 3), plan(25, 365, 12500, 4)]);
    const page = fixture.nativeElement as HTMLElement;
    const tiers = [...page.querySelectorAll('[data-testid="plan-tier"]')];
    expect(tiers.length).toBe(2);
    expect(tiers[0].textContent).toContain('10');
    expect(tiers[0].textContent).toContain('5.000 TL');
    expect(tiers[0].textContent).toContain('41,67 TL');
    expect(tiers[0].textContent).toContain('1.000 TL');
    expect(page.textContent).toContain('2 ay ücretsiz');
    expect(page.textContent).toContain('Her planda bütün özellikler var');
    const monthly = [...page.querySelectorAll('.period button')].find(button => button.textContent!.includes('Aylık')) as HTMLButtonElement;
    monthly.click();
    fixture.detectChanges();
    expect(page.querySelector('[data-testid="plan-tier"]')!.textContent).toContain('500 TL');
    expect(page.querySelector('[data-testid="plan-tier"]')!.textContent).toContain('50 TL');
    expect(monthly.getAttribute('aria-pressed')).toBe('true');
  });

  it('keeps plans that are not student counts as plain cards', async () => {
    const fixture = await render([{ ...plan(10, 30, 0, 1), name: 'Kurumsal', features: [], isContactOnly: true }]);
    expect(fixture.nativeElement.querySelector('[data-testid="plan-tier"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Teklif alın');
  });
});
