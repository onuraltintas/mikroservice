import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CoachingOutcomesComponent } from './coaching-outcomes.component';

describe('Coaching outcome review', () => {
  it('requires a preview fingerprint and reason before importing a draft package', () => {
    TestBed.configureTestingModule({ imports: [CoachingOutcomesComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingOutcomesComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController); const c = fixture.componentInstance;
    http.expectOne('/api/coaching-admin/outcomes').flush({ data: [] });
    c.packageText = '{"code":"pilot"}'; c.importPackage(); http.expectNone('/api/coaching-admin/outcomes/import');
    c.previewPackage(); http.expectOne('/api/coaching-admin/outcomes/import-preview').flush({ data: { fingerprint: 'reviewed', outcomeCount: 52, matchedTopics: 13, unmatchedTopics: [], warnings: ['Taslak'] } });
    c.reason = 'LGS pilot aktarımı'; c.importPackage();
    const r = http.expectOne('/api/coaching-admin/outcomes/import'); expect(r.request.body.fingerprint).toBe('reviewed');
    expect(r.request.body.package.code).toBe('pilot'); r.flush({ data: {} });
    http.expectOne('/api/coaching-admin/outcomes').flush({ data: [] });
    expect(c.review()).toBeNull(); http.verify();
  });
  it('invalidates preview on package change and retains errors without writing', () => {
    TestBed.configureTestingModule({ imports: [CoachingOutcomesComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const f = TestBed.createComponent(CoachingOutcomesComponent); f.detectChanges(); const http = TestBed.inject(HttpTestingController); const c = f.componentInstance;
    http.expectOne('/api/coaching-admin/outcomes').flush({ data: [] });
    c.packageText = 'invalid'; c.previewPackage(); expect(c.error()).toContain('JSON');
    c.packageText = '{}'; c.previewPackage(); http.expectOne('/api/coaching-admin/outcomes/import-preview').flush({ data: { fingerprint: 'old', outcomeCount: 52, matchedTopics: 13, unmatchedTopics: [], warnings: [] } });
    c.packageText = '{"changed":true}'; c.reason = 'Yeni paket'; c.importPackage();
    http.expectNone('/api/coaching-admin/outcomes/import'); expect(c.error()).toContain('yeniden'); http.verify();
  });
});
