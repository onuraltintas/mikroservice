import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingCatalogService } from '../../../core/services/coaching-catalog.service';
import { LocationService } from '../../../core/services/location.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingCatalogEditorComponent } from './coaching-catalog-editor';

// KM7: the Katalog page edits only LGS schools; lessons, units and topics are edited in Müfredat Yönetimi.
describe('CoachingCatalogEditorComponent (LGS schools)', () => {
  function create(id: string | null = null, permitted = true) {
    const document = { fingerprint: 'hash', data: { id: 'record', name: 'Ankara Fen Lisesi', source: 'mebi-lgs', sourceId: '1', isActive: false, city: 'Ankara', district: 'Çankaya', provinceId: '06', districtId: '1', minimumScore: 490.5, scoreYear: 2025 } };
    const service = { get: vi.fn(() => of(document)), create: vi.fn((_kind: string, _request: unknown) => of(document)), update: vi.fn(() => of(document)), setActive: vi.fn(() => of(document)), list: vi.fn() };
    const locations = { getProvinces: vi.fn(() => of([{ id: '06', name: 'Ankara' }])), getDistricts: vi.fn(() => of([{ id: '1', provinceId: '06', name: 'Çankaya' }])) };
    const toaster = { success: vi.fn(), error: vi.fn(), confirm: vi.fn(async () => true) };
    TestBed.configureTestingModule({ imports: [CoachingCatalogEditorComponent], providers: [
      { provide: AuthService, useValue: { userProfile: () => ({ roles: ['SystemAdmin'] }), hasPermission: () => permitted } },
      { provide: CoachingCatalogService, useValue: service }, { provide: LocationService, useValue: locations }, { provide: ToasterService, useValue: toaster }
    ] });
    const fixture = TestBed.createComponent(CoachingCatalogEditorComponent);
    fixture.componentRef.setInput('recordId', id);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, service, locations, toaster };
  }

  it('announces the opened editor and offers no lesson, unit or topic fields', () => {
    const { fixture } = create();
    expect(fixture.nativeElement.querySelector('[role="status"]').textContent).toContain('okul');
    expect(fixture.nativeElement.querySelector('#catalog-edit-exam')).toBeNull();
    expect(fixture.nativeElement.querySelector('#catalog-edit-minutes')).toBeNull();
    expect(fixture.nativeElement.querySelector('#catalog-edit-province')).not.toBeNull();
  });
  it('creates a passive school with a reason and verified location only', () => {
    const { component, service } = create();
    component.form.name = 'Yeni Fen Lisesi'; component.save();
    expect(service.create).not.toHaveBeenCalled();
    component.form.reason = 'Yeni okul'; component.form.provinceId = '06'; component.form.districtId = '1'; component.save();
    expect(service.create).toHaveBeenCalledWith('schools', expect.objectContaining({ name: 'Yeni Fen Lisesi', reason: 'Yeni okul', provinceId: '06', districtId: '1' }));
    expect(service.create.mock.calls[0][1]).not.toHaveProperty('isActive');
  });
  it('loads fresh detail and sends its fingerprint and score on update', () => {
    const { component, service } = create('record');
    expect(component.form.name).toBe('Ankara Fen Lisesi');
    component.form.minimumScore = 491.2345; component.form.reason = 'LGS 2026 taban puanı'; component.save();
    expect(service.update).toHaveBeenCalledWith('schools', 'record', expect.objectContaining({ minimumScore: 491.2345, fingerprint: 'hash' }));
  });
  it('does not load or mutate without content management permission', () => {
    const { component, service } = create('record', false);
    component.form = { name: 'Okul', reason: 'Yeni kayıt' }; component.save();
    expect(service.get).not.toHaveBeenCalled(); expect(service.update).not.toHaveBeenCalled();
  });
  it('keeps entered data after a failed save', () => {
    const { component, service } = create('record');
    service.update.mockReturnValue(throwError(() => ({ status: 409, error: { message: 'Kayıt değişmiş.' } })));
    component.form.reason = 'Puan düzeltmesi'; component.save();
    expect(component.error()).toContain('Kayıt değişmiş'); expect(component.saving()).toBe(false);
  });
  it('prevents duplicate submissions and cancels on destruction', () => {
    const { fixture, component, service } = create('record');
    const pending = new Subject<never>(); service.update.mockReturnValue(pending);
    component.form.reason = 'Puan düzeltmesi'; component.save(); component.save();
    expect(service.update).toHaveBeenCalledTimes(1); fixture.destroy(); expect(pending.observed).toBe(false);
  });
  it('changes status with confirmation and its own reason', async () => {
    const { component, service, toaster } = create('record');
    component.statusReason = 'Yayın onayı'; await component.changeStatus();
    expect(toaster.confirm).toHaveBeenCalled();
    expect(service.setActive).toHaveBeenCalledWith('schools', 'record', { fingerprint: 'hash', reason: 'Yayın onayı', isActive: true });
  });
  it('clears the district when the province changes', () => {
    const { component, locations } = create();
    component.form.provinceId = '06'; component.form.districtId = 'old'; component.provinceChanged();
    expect(component.form.districtId).toBeNull(); expect(locations.getDistricts).toHaveBeenCalledWith('06');
  });
});
