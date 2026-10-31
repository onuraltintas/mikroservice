import { LOCALE_ID } from '@angular/core';
import { formatDate, formatNumber } from '@angular/common';
import { appConfig } from './app.config';

// T1 (31.10.2026): dates and numbers in the apps are Turkish ("21 Eki 2026", "12,5").
describe('application locale', () => {
  it('provides Turkish as the default locale', () => {
    expect(appConfig.providers).toContainEqual({ provide: LOCALE_ID, useValue: 'tr' });
    expect(formatDate('2026-10-21T12:00:00Z', 'd MMM y', 'tr', '+0300')).toBe('21 Eki 2026');
    expect(formatNumber(12.5, 'tr', '1.1-1')).toBe('12,5');
  });
});
