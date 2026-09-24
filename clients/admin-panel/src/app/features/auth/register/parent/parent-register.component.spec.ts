import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ParentRegisterComponent } from './parent-register.component';

it('shows the duplicate-account message in a toaster on parent registration', () => {
  const toaster = { success: vi.fn(), error: vi.fn(), warning: vi.fn() };
  TestBed.configureTestingModule({ providers: [
    provideHttpClient(), provideHttpClientTesting(),
    { provide: Router, useValue: { navigate: vi.fn() } },
    { provide: ConfigurationService, useValue: {} },
    { provide: ToasterService, useValue: toaster }
  ] });
  const component = TestBed.runInInjectionContext(() => new ParentRegisterComponent());
  component.form.patchValue({ firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.com', password: 'StrongPass1!', confirmPassword: 'StrongPass1!', phoneNumber: '5551234567' });
  void component.onSubmit();
  TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/auth/register-parent'))
    .flush({ code: 'Identity.UserExists', message: 'Bu e-posta adresiyle zaten kayıtlı bir hesap var.' }, { status: 409, statusText: 'Conflict' });
  expect(toaster.error).toHaveBeenCalledWith('Bu e-posta adresiyle zaten kayıtlı bir hesap var.');
  TestBed.inject(HttpTestingController).verify();
});
