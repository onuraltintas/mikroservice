import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StaffAccountSettingsComponent } from './staff-account-settings.component';

describe('StaffAccountSettingsComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [StaffAccountSettingsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lets staff update their own profile and password with the selected product session', () => {
    const fixture = TestBed.createComponent(StaffAccountSettingsComponent);
    fixture.detectChanges();
    http.expectOne('/api/users/me').flush({
      userId: 'user-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.test', role: 'Teacher', phoneNumber: '5555555555',
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Hesap ayarları');
    expect((fixture.nativeElement.querySelector('[name="email"]') as HTMLInputElement).value).toBe('ayse@example.test');

    fixture.componentInstance.firstName = 'Ayşe';
    fixture.componentInstance.lastName = 'Demir';
    fixture.componentInstance.phoneNumber = '5555555555';
    fixture.componentInstance.saveProfile();
    const update = http.expectOne('/api/users/me');
    expect(update.request.body).toEqual({ firstName: 'Ayşe', lastName: 'Demir', phoneNumber: '5555555555' });
    update.flush(null);

    fixture.componentInstance.currentPassword = 'Current123!';
    fixture.componentInstance.newPassword = 'NewPass123!';
    fixture.componentInstance.confirmPassword = 'NewPass123!';
    fixture.componentInstance.changePassword();
    const password = http.expectOne('/api/users/me/change-password');
    expect(password.request.body).toEqual({ currentPassword: 'Current123!', newPassword: 'NewPass123!' });
    password.flush(null);
  });

  it('rejects a weak or mismatched password before it reaches Identity', () => {
    const fixture = TestBed.createComponent(StaffAccountSettingsComponent);
    fixture.detectChanges();
    http.expectOne('/api/users/me').flush({ userId: 'user-1', firstName: '', lastName: '', email: '', role: 'Teacher' });
    fixture.componentInstance.currentPassword = 'current';
    fixture.componentInstance.newPassword = 'weak';
    fixture.componentInstance.confirmPassword = 'different';

    fixture.componentInstance.changePassword();

    http.expectNone('/api/users/me/change-password');
    expect(fixture.componentInstance.passwordErrorMessage()).toContain('en az 8 karakter');
  });
});
