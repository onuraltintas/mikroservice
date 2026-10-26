import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { LocationService } from '../../../../core/services/location.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { InstitutionRegisterComponent } from './institution-register.component';

describe('InstitutionRegisterComponent', () => {
  it('explains an existing-account conflict to the registrant', () => {
    const toaster = { success: vi.fn(), error: vi.fn(), warning: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: LocationService, useValue: { getProvinces: () => of([]), getDistricts: () => of([]) } },
        { provide: ConfigurationService, useValue: { getPublicConfigurationValue: () => of('true') } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ToasterService, useValue: toaster }
      ]
    });
    const component = TestBed.runInInjectionContext(() => new InstitutionRegisterComponent());
    component.form.patchValue({
      institutionName: 'Örnek Okul', institutionType: 1, provinceId: '34', districtId: '3401',
      firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.com',
      password: 'StrongPass1!', confirmPassword: 'StrongPass1!'
    });

    void component.onSubmit();

    const request = TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/auth/coaching/register/institution'));
    request.flush({ code: 'Identity.UserExists', message: 'Bu e-posta adresiyle zaten kayıtlı bir hesap var. Lütfen giriş yapın veya şifrenizi sıfırlayın.' }, { status: 409, statusText: 'Conflict' });
    expect(component.errorMessage()).toContain('Bu e-posta adresiyle zaten kayıtlı');
    expect(toaster.error).toHaveBeenCalledWith(expect.stringContaining('Bu e-posta adresiyle zaten kayıtlı'));
    TestBed.inject(HttpTestingController).verify();
  });

  it('does not expose server internals in a registration failure toaster', () => {
    const toaster = { success: vi.fn(), error: vi.fn(), warning: vi.fn() };
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(), provideHttpClientTesting(),
      { provide: LocationService, useValue: { getProvinces: () => of([]), getDistricts: () => of([]) } },
      { provide: ConfigurationService, useValue: { getPublicConfigurationValue: () => of('true') } },
      { provide: Router, useValue: { navigate: vi.fn() } },
      { provide: ToasterService, useValue: toaster }
    ] });
    const component = TestBed.runInInjectionContext(() => new InstitutionRegisterComponent());
    component.form.patchValue({
      institutionName: 'Örnek Okul', institutionType: 1, provinceId: '34', districtId: '3401',
      firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.com',
      password: 'StrongPass1!', confirmPassword: 'StrongPass1!'
    });
    void component.onSubmit();
    TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/auth/coaching/register/institution'))
      .flush({ message: 'SQL connection failed' }, { status: 500, statusText: 'Server Error' });
    expect(toaster.error).toHaveBeenCalledWith('Kayıt şu anda tamamlanamıyor. Lütfen daha sonra yeniden deneyin.');
    TestBed.inject(HttpTestingController).verify();
  });

  it('submits the institution type and province/district required by the API', () => {
    const locations = {
      getProvinces: vi.fn().mockReturnValue(of([{ id: '34', name: 'İstanbul' }])),
      getDistricts: vi.fn().mockReturnValue(of([{ id: '3401', provinceId: '34', name: 'Kadıköy' }]))
    };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: LocationService, useValue: locations },
        { provide: ConfigurationService, useValue: { getPublicConfigurationValue: () => of('true') } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }
      ]
    });
    const component = TestBed.runInInjectionContext(() => new InstitutionRegisterComponent());
    component.ngOnInit();
    component.onProvinceChange('34');
    component.form.patchValue({
      institutionName: 'Örnek Okul', institutionType: 1, provinceId: '34', districtId: '3401',
      firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.com',
      password: 'StrongPass1!', confirmPassword: 'StrongPass1!'
    });

    void component.onSubmit();

    const request = TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/auth/coaching/register/institution'));
    expect(request.request.body).toEqual({
      institutionName: 'Örnek Okul', institutionType: 1, provinceId: '34', districtId: '3401',
      firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.com',
      password: 'StrongPass1!', phone: null
    });
    request.flush({});
    TestBed.inject(HttpTestingController).verify();
  });
});
