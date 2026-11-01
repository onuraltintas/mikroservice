import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingCatalogPage, CoachingCatalogService } from '../../../core/services/coaching-catalog.service';
import { CoachingCatalogComponent } from './coaching-catalog';
import { ToasterService } from '../../../core/services/toaster.service';
import { LocationService } from '../../../core/services/location.service';

// KM7 (01.11.2026): the Katalog page keeps only the target catalog (LGS schools, university programs).
describe('CoachingCatalogComponent (target catalog)', () => {
  function create(roles = ['SystemAdmin']) {
    const service = { exams: vi.fn(() => of([])),
      list: vi.fn(() => of<CoachingCatalogPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 })),
      usage: vi.fn(() => of({ id: 'record', name: 'Ankara Fen Lisesi', fingerprint: 'fingerprint', canDelete: true, catalogReferences: 0, planReferences: 0, goalReferences: 0, examReferences: 0 })),
      delete: vi.fn(() => of({ success: true }))
    };
    const toaster = { confirm: vi.fn(async (_message: string, _options?: unknown) => true), success: vi.fn(), error: vi.fn() };
    TestBed.configureTestingModule({
      imports: [CoachingCatalogComponent],
      providers: [
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: AuthService, useValue: { userProfile: () => ({ roles }), hasPermission: () => true } },
        { provide: ToasterService, useValue: toaster },
        { provide: LocationService, useValue: { getProvinces: () => of([{ id: '66', name: 'Yozgat' }]), getDistricts: () => of([{ id: '1', provinceId: '66', name: 'Merkez' }]) } },
        { provide: CoachingCatalogService, useValue: service }
      ]
    });
    const fixture = TestBed.createComponent(CoachingCatalogComponent);
    return { fixture, component: fixture.componentInstance, service, toaster };
  }

  it('shows the LGS schools and university programs tabs and no lesson, unit or topic editing', () => {
    const { fixture } = create(); fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const tabs = Array.from(el.querySelectorAll('[role="tab"]')).map(x => x.textContent!.trim());
    expect(tabs).toEqual(['LGS okulları', 'Üniversite programları']);
    expect(el.querySelector('#catalog-kind')).toBeNull();
    expect(el.querySelector('app-coaching-catalog-import')).toBeNull();
    expect(el.querySelector('app-coaching-outcomes')).toBeNull();
    expect(el.querySelector('main')).toBeNull();
    expect(el.textContent).toContain('Müfredat Yönetimi');
  });
  it('renders the university programs component in its tab', () => {
    const { fixture, component, service } = create(); fixture.detectChanges(); service.list.mockClear();
    component.chooseTab('programs'); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-coaching-target-programs')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#catalog-search')).toBeNull();
    expect(service.list).not.toHaveBeenCalled();
  });
  it('lists schools by verified location and clears the district when the province changes', () => {
    const { component, service } = create(); component.ngOnInit();
    component.provinceId = '66'; component.districtId = 'old'; component.provinceChanged();
    expect(component.districtId).toBe('');
    component.districtId = '1'; component.load();
    expect(service.list).toHaveBeenLastCalledWith('schools', expect.objectContaining({ provinceId: '66', districtId: '1' }));
  });
  it('never opens shared catalog editing for an institution administrator', () => {
    const { component, service } = create(['InstitutionAdmin']);
    component.openEditor('record'); expect(component.editorOpen()).toBe(false);
    component.ngOnInit(); expect(service.list).not.toHaveBeenCalled(); expect(component.error()).toContain('global');
  });
  it('searches and pages on the server', () => {
    const { component, service } = create();
    component.search = 'Fen Lisesi'; component.load(2);
    expect(service.list).toHaveBeenLastCalledWith('schools', expect.objectContaining({ search: 'Fen Lisesi', pageNumber: 2, pageSize: 25 }));
  });
  it('clears old results and shows a recoverable failure', () => {
    const { component, service } = create();
    service.list.mockReturnValue(throwError(() => new Error('Unavailable'))); component.load();
    expect(component.items()).toEqual([]); expect(component.loading()).toBe(false); expect(component.error()).toContain('yeniden');
  });
  it('cancels an old request when the page reloads', () => {
    const { component, service } = create();
    const first = new Subject<CoachingCatalogPage>(); service.list.mockReturnValueOnce(first);
    component.load(); component.load(2);
    first.next({ items: [{ id: 'old', name: 'Eski', source: 'mebi-lgs', sourceId: '1', isActive: false }], totalCount: 1, pageNumber: 1, pageSize: 25 });
    expect(component.items()).toEqual([]);
  });
  it('rejects an invalid score year before sending a request', () => {
    const { component, service } = create();
    component.scoreYear = 2025.5; component.load();
    expect(service.list).not.toHaveBeenCalled(); expect(component.error()).toContain('Puan yılı');
  });
  it('shows source codes as Turkish labels', () => {
    const { fixture, component } = create(); fixture.detectChanges();
    component.items.set([{ id: 'record', name: 'Ankara Fen Lisesi', source: 'mebi-lgs', sourceId: '06-123', isActive: true }]); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('MEBİ LGS okul verisi'); expect(fixture.nativeElement.textContent).not.toContain('mebi-lgs');
  });
  it('requires a reason and explicit confirmation before permanent deletion', async () => {
    const { component, service, toaster } = create();
    component.inspectUsage('record'); await component.deleteSelected(); expect(service.delete).not.toHaveBeenCalled();
    component.deleteReason = 'Kullanılmayan kopya'; component.deleteConfirmation = 'SİL'; await component.deleteSelected();
    expect(toaster.confirm.mock.calls[0][0]).toContain('LGS okulu');
    expect(service.delete).toHaveBeenCalledWith('schools', 'record', { fingerprint: 'fingerprint', reason: 'Kullanılmayan kopya', confirmId: 'record' });
  });
  it('never offers deletion for a used record', async () => {
    const { component, service, toaster } = create();
    service.usage.mockReturnValue(of({ id: 'record', name: 'Okul', fingerprint: 'fingerprint', canDelete: false, catalogReferences: 0, planReferences: 0, goalReferences: 1, examReferences: 0 }));
    component.inspectUsage('record'); component.deleteReason = 'Kullanılmayan kopya'; component.deleteConfirmation = 'SİL'; await component.deleteSelected();
    expect(service.delete).not.toHaveBeenCalled(); expect(toaster.confirm).not.toHaveBeenCalled();
  });
});
