import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { GamificationService } from './gamification.service';

// H19 (audit 30.10.2026): levels above 20 stay in the top tier instead of falling back to a grey "Okuyucu".
describe('GamificationService tiers', () => {
  it('keeps every level from 16 upwards in the master tier', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(GamificationService);
    for (const level of [16, 20, 21, 35, 120]) {
      expect(service.getTierName(level)).toBe('Master Okuyucu');
      expect(service.getTierIcon(level)).toBe('📕');
      expect(service.getTierColor(level)).toBe('#FFD700');
    }
    expect(service.getTierName(1)).toBe('Başlangıç Okuyucu');
    expect(service.getTierName(0)).toBe('Başlangıç Okuyucu');
  });
});
