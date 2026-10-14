import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { InstitutionRegisterComponent } from './institution-register.component';
import { LocationService } from '../../../../core/services/location.service';
import { Subject } from 'rxjs';

describe('Institution registration contract', () => {
  it('ignores an old province failure and clears the district error on retry', () => {
    const old = new Subject<any[]>();
    const current = new Subject<any[]>();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: Router, useValue: {} }, { provide: ToasterService, useValue: {} },
      { provide: ConfigurationService, useValue: {} },
      { provide: LocationService, useValue: { getDistricts: (id: string) => id === 'A' ? old : current } }] });
    const c = TestBed.runInInjectionContext(() => new InstitutionRegisterComponent());
    c.form.controls.provinceId.setValue('A'); c.onProvinceChange();
    c.form.controls.provinceId.setValue('B'); c.onProvinceChange();
    current.next([{ id: 'B1', name: 'District', provinceId: 'B' }]);
    old.error(new Error('Old failure'));
    expect(c.errorMessage()).toBeNull(); expect(c.districts[0].id).toBe('B1');
    current.error(new Error('Current failure'));
    expect(c.errorMessage()).toContain('İlçe seçenekleri');
    c.form.controls.provinceId.setValue(''); c.onProvinceChange();
    expect(c.errorMessage()).toBeNull();
  });
  it('sends the actual Identity institution fields and selected location', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: Router, useValue: { navigate: vi.fn() } },
      { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
      { provide: ConfigurationService, useValue: {} }] });
    const c = TestBed.runInInjectionContext(() => new InstitutionRegisterComponent());
    c.form.setValue({ institutionName: 'Existing School', institutionType: 1, provinceId: '34', districtId: '3401',
      firstName: 'Test', lastName: 'Manager', email: 'manager@example.test', password: 'StrongPassword1!', confirmPassword: 'StrongPassword1!' });
    c.legalReady = true;
    c.legalAcceptances = ['privacy','kvkk','coaching-terms'].map(slug => ({ slug, version: 1 }));
    await c.onSubmit();
    const http = TestBed.inject(HttpTestingController);
    const req = http.expectOne('/api/auth/coaching/register/institution');
    expect(req.request.body.firstName).toBe('Test');
    expect(req.request.body.lastName).toBe('Manager');
    expect(req.request.body.institutionType).toBe(1);
    expect(req.request.body.provinceId).toBe('34');
    expect(req.request.body.districtId).toBe('3401');
    expect(req.request.body.managerFirstName).toBeUndefined();
    expect(req.request.body.taxNumber).toBeUndefined();
    req.flush({ code: 'Institution.InvalidLocation', description: 'İl ve ilçe eşleşmiyor.' }, { status: 400, statusText: 'Bad Request' });
    expect(c.errorMessage()).toBe('İl ve ilçe eşleşmiyor.');
    expect(c.isLoading()).toBe(false); http.verify();
  });
});
