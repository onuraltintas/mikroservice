import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingCatalogPage, CoachingCatalogService } from '../../../core/services/coaching-catalog.service';
import { CoachingCatalogComponent } from './coaching-catalog';
import { ToasterService } from '../../../core/services/toaster.service';

describe('CoachingCatalogComponent', () => {
  function create(roles = ['SystemAdmin']) {
    const service = {
      list: vi.fn(() => of<CoachingCatalogPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 })),
      usage: vi.fn(() => of({ id: 'record', name: 'Math', fingerprint: 'fingerprint', canDelete: true, catalogReferences: 0, planReferences: 0, goalReferences: 0, examReferences: 0 })),
      delete: vi.fn(() => of({ success: true }))
    };
    const toaster = { confirm: vi.fn(async () => true), success: vi.fn(), error: vi.fn() };
    TestBed.configureTestingModule({
      imports: [CoachingCatalogComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: AuthService, useValue: { userProfile: () => ({ roles }), hasPermission: () => true } },
        { provide: ToasterService, useValue: toaster },
        { provide: CoachingCatalogService, useValue: service }
      ]
    });
    const fixture = TestBed.createComponent(CoachingCatalogComponent);
    return { fixture, component: fixture.componentInstance, service, toaster };
  }

  it('does not fetch shared catalogs for an institution administrator', () => {
    const { component, service } = create(['InstitutionAdmin']);
    component.ngOnInit();
    expect(service.list).not.toHaveBeenCalled();
    expect(component.error()).toContain('global');
  });

  it('searches and pages on the server', () => {
    const { component, service } = create();
    component.search = 'Matematik';
    component.load(2);
    expect(service.list).toHaveBeenLastCalledWith('lessons', expect.objectContaining({ search: 'Matematik', pageNumber: 2, pageSize: 25 }));
  });

  it('clears old results and shows a recoverable failure', () => {
    const { component, service } = create();
    service.list.mockReturnValue(throwError(() => new Error('Unavailable')));
    component.load();
    expect(component.items()).toEqual([]);
    expect(component.loading()).toBe(false);
    expect(component.error()).toContain('yeniden');
  });

  it('cancels an old request when changing catalog type', () => {
    const { component, service } = create();
    const first = new Subject<CoachingCatalogPage>();
    service.list.mockReturnValueOnce(first);
    component.load();
    component.changeKind('schools');
    first.next({ items: [{ id: 'old', name: 'Old lesson', source: 'test', sourceId: '1', isActive: false }], totalCount: 1, pageNumber: 1, pageSize: 25 });
    expect(component.items()).toEqual([]);
    expect(service.list).toHaveBeenLastCalledWith('schools', expect.objectContaining({ pageNumber: 1 }));
  });

  it('renders labels for empty results and accessible filters', () => {
    const { fixture } = create();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Kayıt bulunamadı');
    expect(fixture.nativeElement.querySelector('label[for="catalog-search"]')).not.toBeNull();
  });

  it('rejects invalid numeric filters before sending an API request', () => {
    const { component, service } = create();
    component.gradeNumber = 13;
    component.load();
    expect(service.list).not.toHaveBeenCalled();
    expect(component.error()).toContain('Sınıf');
    component.kind = 'schools';
    component.scoreYear = 2025.5;
    component.load();
    expect(service.list).not.toHaveBeenCalled();
    expect(component.error()).toContain('Puan yılı');
  });

  it('requires a reason and explicit confirmation before permanent deletion', async () => {
    const { component, service, toaster } = create();
    component.inspectUsage('record');
    await component.deleteSelected();
    expect(service.delete).not.toHaveBeenCalled();
    component.deleteReason = 'Unused duplicate';
    component.deleteConfirmation = 'SİL';
    await component.deleteSelected();
    expect(toaster.confirm).toHaveBeenCalled();
    expect(service.delete).toHaveBeenCalledWith('lessons', 'record', { fingerprint: 'fingerprint', reason: 'Unused duplicate', confirmId: 'record' });
    expect(component.selectedUsage()).toBeNull();
  });

  it('never offers deletion for a used record', async () => {
    const { component, service, toaster } = create();
    service.usage.mockReturnValue(of({ id: 'record', name: 'Math', fingerprint: 'fingerprint', canDelete: false, catalogReferences: 0, planReferences: 1, goalReferences: 0, examReferences: 0 }));
    component.inspectUsage('record');
    component.deleteReason = 'Unused duplicate';
    component.deleteConfirmation = 'SİL';
    await component.deleteSelected();
    expect(service.delete).not.toHaveBeenCalled();
    expect(toaster.confirm).not.toHaveBeenCalled();
  });
});
