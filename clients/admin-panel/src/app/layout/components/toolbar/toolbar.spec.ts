import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { vi } from 'vitest';

import { ToolbarComponent } from './toolbar';

describe('ToolbarComponent', () => {
  let component: ToolbarComponent;
  let fixture: ComponentFixture<ToolbarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ToolbarComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            userProfile: signal(null),
            logout: vi.fn()
          }
        },
        {
          provide: NotificationService,
          useValue: {
            notifications: signal([]),
            unreadCount: signal(0),
            fetchNotifications: vi.fn(),
            markAsRead: vi.fn(),
            markAllAsRead: vi.fn()
          }
        }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ToolbarComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
