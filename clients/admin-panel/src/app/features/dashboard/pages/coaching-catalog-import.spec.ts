import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingCatalogImportReview, CoachingCatalogService } from '../../../core/services/coaching-catalog.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingCatalogImportComponent } from './coaching-catalog-import';

describe('CoachingCatalogImportComponent', () => {
  function setup(permitted = true) {
    const review: CoachingCatalogImportReview = { fingerprint: 'hash', counts: { 'lessons.json': 1 }, newRecords: 1 };
    const service = { previewImport: vi.fn(() => of(review)), approveImport: vi.fn(() => of({ changed: 1 })), publishImport: vi.fn(() => of({ changed: 1 })) };
    const toaster = { confirm: vi.fn(async () => true), success: vi.fn() };
    TestBed.configureTestingModule({ imports: [CoachingCatalogImportComponent], providers: [
      { provide: AuthService, useValue: { userProfile: () => ({ roles: ['SystemAdmin'] }), hasPermission: () => permitted } },
      { provide: CoachingCatalogService, useValue: service }, { provide: ToasterService, useValue: toaster }
    ] });
    const fixture = TestBed.createComponent(CoachingCatalogImportComponent); fixture.detectChanges();
    const component = fixture.componentInstance;
    component.source = 'fixture'; component.files = Object.fromEntries(component.fileNames.map(name => [name, '[]']));
    component.reason = 'Test onayı';
    return { fixture, component, service, toaster };
  }
  it('requires preview before approval and keeps publication separate', async () => {
    const { component, service } = setup();
    await component.approve(false); expect(service.approveImport).not.toHaveBeenCalled();
    component.preview(); component.reason = 'Test onayı'; await component.approve(false);
    expect(service.approveImport).toHaveBeenCalledWith(expect.objectContaining({ source: 'fixture', fingerprint: 'hash', reason: 'Test onayı' }));
    expect(component.review()).toBeNull();
    expect(service.publishImport).not.toHaveBeenCalled();
  });
  it('does not expose operations without management permission', () => {
    const { component, service } = setup(false); component.preview();
    expect(service.previewImport).not.toHaveBeenCalled();
  });
  it('cancels pending preview when destroyed', () => {
    const { fixture, component, service } = setup();
    const pending = new Subject<{ fingerprint: string; counts: Record<string, number>; newRecords: number }>();
    service.previewImport.mockReturnValue(pending); component.preview(); fixture.destroy();
    expect(pending.observed).toBe(false);
  });
  it('invalidates preview when source changes', () => {
    const { component } = setup(); component.preview(); component.reason = 'Önceki kaynak gerekçesi'; component.invalidate(); expect(component.review()).toBeNull();
    expect(component.reason).toBe('');
  });
  it('announces a pending operation visibly', () => {
    const { fixture, component } = setup(); component.busy.set(true); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="status"]').textContent).toContain('İşlem');
  });
  it('never publishes files that still contain new records', async () => {
    const { component, service } = setup(); component.preview(); await component.approve(true);
    expect(service.publishImport).not.toHaveBeenCalled();
  });
});
