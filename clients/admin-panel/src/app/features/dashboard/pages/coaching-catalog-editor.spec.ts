import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingCatalogService } from '../../../core/services/coaching-catalog.service';
import { LocationService } from '../../../core/services/location.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingCatalogEditorComponent } from './coaching-catalog-editor';

describe('CoachingCatalogEditorComponent', () => {
  it('clears old parent options while a changed unit query is pending and after clearing the unit', () => {
    const { component, service } = create('topics');
    const pending = new Subject<{ items: never[]; totalCount: number; pageNumber: number; pageSize: number }>();
    service.list.mockReturnValue(pending);
    component.form.lessonId = 'lesson';
    component.options.set({ lessons: [], units: [], topics: [{ id: 'old', name: 'Old', source: 'fixture', sourceId: '1', isActive: true }] });
    component.lookupTotals.topics = 100;
    component.relationChanged('units', 'new-unit');
    expect(component.options().topics).toEqual([]);
    expect(component.lookupTotals.topics).toBe(0);
    component.relationChanged('units', null);
    expect(pending.observed).toBe(false);
    expect(component.options().topics).toEqual([]);
  });

  it('announces the dynamically opened editor', () => {
    const { fixture } = create();
    expect(fixture.nativeElement.querySelector('[role="status"]').textContent).toContain('Katalog');
  });
  function create(kind = 'lessons', id: string | null = null, permitted = true) {
    const document = { fingerprint: 'hash', data: { id: 'record', name: 'Math', source: 'fixture', sourceId: '1', isActive: false, gradeNumber: 8, examCode: 'LGS' } };
    const service = { get: vi.fn(() => of(document)), create: vi.fn((_kind: string, _request: unknown) => of(document)), update: vi.fn(() => of(document)), setActive: vi.fn(() => of(document)), list: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 })) };
    const locations = { getProvinces: vi.fn(() => of([{ id: '66', name: 'Yozgat' }])), getDistricts: vi.fn(() => of([{ id: '1', provinceId: '66', name: 'Merkez' }])) };
    const toaster = { success: vi.fn(), error: vi.fn(), confirm: vi.fn(async () => true) };
    TestBed.configureTestingModule({ imports: [CoachingCatalogEditorComponent], providers: [
      { provide: AuthService, useValue: { userProfile: () => ({ roles: ['SystemAdmin'] }), hasPermission: () => permitted } },
      { provide: CoachingCatalogService, useValue: service }, { provide: LocationService, useValue: locations }, { provide: ToasterService, useValue: toaster }
    ] });
    const fixture = TestBed.createComponent(CoachingCatalogEditorComponent);
    fixture.componentRef.setInput('kind', kind);
    fixture.componentRef.setInput('recordId', id);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, service, locations, toaster };
  }

  it('creates inactive records with a reason, without client-supplied source or state', () => {
    const { component, service } = create();
    component.form.name = 'Math';
    component.save();
    expect(service.create).not.toHaveBeenCalled();
    component.form.reason = 'Yeni kayıt';
    component.save();
    expect(service.create).toHaveBeenCalledWith('lessons', expect.objectContaining({ name: 'Math', reason: 'Yeni kayıt' }));
    expect(service.create.mock.calls[0][1]).not.toHaveProperty('source');
    expect(service.create.mock.calls[0][1]).not.toHaveProperty('isActive');
  });

  it('loads fresh detail and sends its fingerprint on update', () => {
    const { component, service } = create('lessons', 'record');
    expect(component.form.name).toBe('Math');
    component.form.name = 'Updated';
    component.form.reason = 'Ad düzeltmesi';
    component.save();
    expect(service.update).toHaveBeenCalledWith('lessons', 'record', expect.objectContaining({ name: 'Updated', fingerprint: 'hash' }));
  });

  it('does not load or mutate when content management permission is absent', () => {
    const { component, service } = create('lessons', 'record', false);
    component.form = { name: 'Math', reason: 'Yeni kayıt' };
    component.save();
    expect(service.get).not.toHaveBeenCalled();
    expect(service.create).not.toHaveBeenCalled();
    expect(service.update).not.toHaveBeenCalled();
  });

  it('shows a retryable error and keeps entered data after failed save', () => {
    const { component, service } = create();
    service.create.mockReturnValue(throwError(() => ({ status: 409, error: { message: 'Kayıt değişmiş.' } })));
    component.form = { name: 'Math', reason: 'Yeni kayıt' };
    component.save();
    expect(component.error()).toContain('Kayıt değişmiş');
    expect(component.form.name).toBe('Math');
    expect(component.saving()).toBe(false);
  });

  it('prevents duplicate submissions and cancels on destruction', () => {
    const { fixture, component, service } = create();
    const pending = new Subject<any>();
    service.create.mockReturnValue(pending);
    component.form = { name: 'Math', reason: 'Yeni kayıt' };
    component.save();
    component.save();
    expect(service.create).toHaveBeenCalledTimes(1);
    fixture.destroy();
    expect(pending.observed).toBe(false);
  });

  it('changes status explicitly with confirmation and its own reason', async () => {
    const { component, service, toaster } = create('lessons', 'record');
    component.statusReason = 'Yayın onayı';
    await component.changeStatus();
    expect(toaster.confirm).toHaveBeenCalled();
    expect(service.setActive).toHaveBeenCalledWith('lessons', 'record', { fingerprint: 'hash', reason: 'Yayın onayı', isActive: true });
  });

  it('clears district and dependent hierarchy choices on parent change', () => {
    const { component, locations } = create('schools');
    component.form.provinceId = '66';
    component.form.districtId = 'old';
    component.provinceChanged();
    expect(component.form.districtId).toBeNull();
    expect(locations.getDistricts).toHaveBeenCalledWith('66');
    component.form.unitId = 'unit';
    component.form.parentId = 'parent';
    component.lessonChanged();
    expect(component.form.unitId).toBeNull();
    expect(component.form.parentId).toBeNull();
  });
  it('requests parentless topics on the server before pagination', () => {
    const { component, service } = create('topics');
    component.form.lessonId = 'lesson';
    component.form.unitId = 'unit';
    component.searchRelation('topics', 2);
    expect(service.list).toHaveBeenCalledWith('topics', expect.objectContaining({ pageNumber: 2, lessonId: 'lesson', unitId: 'unit', hasParent: false }));
  });
});
