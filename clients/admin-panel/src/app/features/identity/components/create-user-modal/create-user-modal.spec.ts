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
      role: 'Student'
    });
    component.save();

    const request = http.expectOne('/api/users');
    request.flush({ userId: 'user-1' });

    expect(component.createdUser()).toEqual({
      email: 'test@example.com',
      invitationSent: true
    });
    expect(component.createdUser()).not.toHaveProperty('temporaryPassword');
    expect(toaster.success).toHaveBeenCalledWith('Kullanıcı başarıyla oluşturuldu.');
    http.verify();
  });
});
