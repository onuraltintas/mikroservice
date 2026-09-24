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

    const request = TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/auth/register-institution'));
    expect(request.request.body).toEqual({
      institutionName: 'Örnek Okul', institutionType: 1, provinceId: '34', districtId: '3401',
      firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.com',
      password: 'StrongPass1!', phone: null
    });
    request.flush({});
    TestBed.inject(HttpTestingController).verify();
  });
});
