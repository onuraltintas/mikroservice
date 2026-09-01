import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ToasterService } from '../../../../core/services/toaster.service';
import { CreateUserModalComponent } from './create-user-modal';

describe('CreateUserModalComponent', () => {
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
