import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { CoachingOnboardingService } from './coaching-onboarding.service';

describe('CoachingOnboardingService status cache (Paket C review L4)', () => {
  it('remembers a completed wizard per user and forgets it on sign-out or for another user', async () => {
    const user = signal<{ id: string } | null>({ id: 'student-1' });
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { userProfile: user } }] });
    const service = TestBed.inject(CoachingOnboardingService); const http = TestBed.inject(HttpTestingController);
    const status = service.currentStatus().subscribe();
    http.expectOne('/api/coaching/students/me/onboarding').flush({ data: { status: 'Completed' } }); status.unsubscribe();
    expect(await firstValueFrom(service.currentStatus())).toBe('Completed');
    http.expectNone('/api/coaching/students/me/onboarding');
    user.set({ id: 'student-2' });
    expect(service.status()).toBeNull();
    service.currentStatus().subscribe();
    http.expectOne('/api/coaching/students/me/onboarding').flush({ data: { status: 'NotStarted' } });
    expect(service.status()).toBe('NotStarted');
    user.set(null); expect(service.status()).toBeNull();
    service.reset(); user.set({ id: 'student-2' }); expect(service.status()).toBeNull();
    http.verify();
  });
});
