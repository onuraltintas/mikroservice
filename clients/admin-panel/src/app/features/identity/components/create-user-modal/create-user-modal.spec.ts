import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ToasterService } from '../../../../core/services/toaster.service';
import { CreateUserModalComponent } from './create-user-modal';

describe('CreateUserModalComponent', () => {
  it('offers every built-in identity role that the API can provision', () => {
    TestBed.configureTestingModule({
      imports: [CreateUserModalComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } }
      ]
    });

    const fixture = TestBed.createComponent(CreateUserModalComponent);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/users/roles').flush(['Student', 'Teacher', 'InstitutionAdmin', 'Parent', 'Editor']);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('option[value="Editor"]')?.textContent).toContain('Editör');
    http.verify();
  });

  it('loads active custom roles from the identity API', () => {
    TestBed.configureTestingModule({
      imports: [CreateUserModalComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } }
      ]
    });

    const fixture = TestBed.createComponent(CreateUserModalComponent);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http.expectOne('/api/users/roles').flush(['Student', 'Learner']);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('option[value="Learner"]')).not.toBeNull();
    http.verify();
  });

  it('confirms the password setup invitation without exposing a temporary password', () => {
    const toaster = {
      success: vi.fn(),
      error: vi.fn()
    };

    TestBed.configureTestingModule({
      imports: [CreateUserModalComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToasterService, useValue: toaster }
      ]
    });

    const fixture = TestBed.createComponent(CreateUserModalComponent);
    const component = fixture.componentInstance;
    const http = TestBed.inject(HttpTestingController);

    component.form.setValue({
      firstName: 'Test',
      lastName: 'User',
      email: 'test@example.com',
      phoneNumber: '',
      role: 'Student',
      product: 1
    });
    component.save();

    const request = http.expectOne('/api/users');
    request.flush({ userId: 'user-1' });

    expect(component.createdUser()).toMatchObject({
      email: 'test@example.com',
      invitationSent: true
    });
    expect(component.createdUser()).not.toHaveProperty('temporaryPassword');
    expect(toaster.success).toHaveBeenCalledWith('Kullanıcı başarıyla oluşturuldu.');
    http.verify();
  });

  it.each(['Student', 'Teacher', 'InstitutionAdmin', 'InstitutionOwner'])('requires a service for %s', (role) => {
    TestBed.configureTestingModule({ imports: [CreateUserModalComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } }] });
    const component = TestBed.createComponent(CreateUserModalComponent).componentInstance;
    component.form.patchValue({ firstName: 'Test', lastName: 'User', email: 'user@example.test', role });
    component.save();
    TestBed.inject(HttpTestingController).expectNone('/api/users');
    expect(component.form.invalid).toBe(true);
  });

  it.each(['Student', 'Teacher', 'InstitutionAdmin', 'InstitutionOwner'].flatMap(role => [1, 2].map(product => ({ role, product }))))(
    'creates $role in service $product and preserves its selection on error', ({ role, product }) => {
      TestBed.configureTestingModule({ imports: [CreateUserModalComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } }] });
      const component = TestBed.createComponent(CreateUserModalComponent).componentInstance;
      component.form.patchValue({ firstName: 'Test', lastName: 'User', email: 'user@example.test', role, product: product as 1 | 2 });
      component.save();
      component.save();
      const http = TestBed.inject(HttpTestingController);
      const request = http.expectOne('/api/users');
      expect(request.request.body).toMatchObject({ role, product });
      request.flush({ message: 'Test error' }, { status: 400, statusText: 'Bad request' });
      expect(component.form.value.product).toBe(product);
      expect(component.createdUser()).toBeNull();
      expect(component.loading()).toBe(false);
      http.verify();
    });

  it('creates SystemAdmin globally without granting the previously selected service', () => {
    TestBed.configureTestingModule({ imports: [CreateUserModalComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } }] });
    const component = TestBed.createComponent(CreateUserModalComponent).componentInstance;
    component.form.patchValue({ firstName: 'Test', lastName: 'Admin', email: 'admin@example.test', role: 'SystemAdmin', product: 2 });
    component.save();
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne('/api/users');
    expect(request.request.body.product).toBeNull();
    request.flush({ userId: 'admin-1' });
    expect(component.createdUser()?.scope).toBe('SystemAdmin · Tüm servisler');
    http.verify();
  });
});
