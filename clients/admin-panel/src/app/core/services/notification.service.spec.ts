import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../auth/auth.service';
import {
  NotificationHubConnectionFactory,
  NotificationService
} from './notification.service';

describe('NotificationService session isolation', () => {
  const userA: UserProfile = {
    id: 'user-a', email: 'a@example.test', firstName: 'A', lastName: 'User',
    username: 'a', roles: ['SystemAdmin'], role: 'SystemAdmin', permissions: []
  };
  const userB: UserProfile = {
    id: 'user-b', email: 'b@example.test', firstName: 'B', lastName: 'User',
    username: 'b', roles: ['SystemAdmin'], role: 'SystemAdmin', permissions: []
  };

  it('stops and clears the previous session before connecting the next user', async () => {
    const profile = signal<UserProfile | null>(null);
    let token = 'token-a';
    const auth = {
      userProfile: profile.asReadonly(),
      getToken: vi.fn(() => token)
    };
    const firstConnection = createConnection();
    const secondConnection = createConnection();
    const factory = {
      create: vi.fn()
        .mockReturnValueOnce(firstConnection)
        .mockReturnValueOnce(secondConnection)
    };

    TestBed.configureTestingModule({
      providers: [
        NotificationService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: NotificationHubConnectionFactory, useValue: factory },
        { provide: PLATFORM_ID, useValue: 'browser' }
      ]
    });
    const service = TestBed.inject(NotificationService);
    const http = TestBed.inject(HttpTestingController);

    profile.set(userA);
    TestBed.tick();
    await vi.waitFor(() => expect(firstConnection.start).toHaveBeenCalledOnce());
    http.expectOne(request => request.url.includes('/notifications')).flush([], {
      headers: { 'X-Unread-Count': '0' }
    });

    expect(service.notifications()).toEqual([]);
    profile.set(null);
    TestBed.tick();
    await vi.waitFor(() => expect(firstConnection.stop).toHaveBeenCalledOnce());
    expect(service.notifications()).toEqual([]);
    expect(service.unreadCount()).toBe(0);

    token = 'token-b';
    profile.set(userB);
    TestBed.tick();
    await vi.waitFor(() => expect(secondConnection.start).toHaveBeenCalledOnce());
    http.expectOne(request => request.url.includes('/notifications')).flush([], {
      headers: { 'X-Unread-Count': '0' }
    });

    expect(factory.create).toHaveBeenCalledTimes(2);
    const accessTokenFactory = factory.create.mock.calls[1][1] as () => string;
    expect(accessTokenFactory()).toBe('token-b');
    token = 'rotated-token-b';
    expect(accessTokenFactory()).toBe('rotated-token-b');
  });
});

function createConnection() {
  return {
    start: vi.fn().mockResolvedValue(undefined),
    stop: vi.fn().mockResolvedValue(undefined),
    on: vi.fn()
  };
}
