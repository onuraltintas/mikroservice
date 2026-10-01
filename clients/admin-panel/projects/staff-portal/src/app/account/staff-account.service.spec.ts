import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { StaffAccountService } from './staff-account.service';

describe('StaffAccountService', () => {
  let http: HttpTestingController;
  let service: StaffAccountService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [StaffAccountService, provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(StaffAccountService);
  });

  afterEach(() => http.verify());

  it('loads and updates the authenticated account through Identity own-profile endpoints', async () => {
    const profile = firstValueFrom(service.getMyProfile());
    const getRequest = http.expectOne('/api/users/me');
    expect(getRequest.request.method).toBe('GET');
    getRequest.flush({ userId: 'user-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.test' });
    await expect(profile).resolves.toMatchObject({ userId: 'user-1', email: 'ayse@example.test' });

    const update = firstValueFrom(service.updateMyProfile({ firstName: 'Ayşe', lastName: 'Demir', phoneNumber: '5555555555' }));
    const putRequest = http.expectOne('/api/users/me');
    expect(putRequest.request.method).toBe('PUT');
    expect(putRequest.request.body).toEqual({ firstName: 'Ayşe', lastName: 'Demir', phoneNumber: '5555555555' });
    putRequest.flush(null);
    await update;
  });

  it('changes the signed-in user password without exposing account ids', async () => {
    const update = firstValueFrom(service.changeMyPassword('current-pass', 'NewPass123!'));
    const request = http.expectOne('/api/users/me/change-password');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ currentPassword: 'current-pass', newPassword: 'NewPass123!' });
    request.flush(null);
    await update;
  });
});
