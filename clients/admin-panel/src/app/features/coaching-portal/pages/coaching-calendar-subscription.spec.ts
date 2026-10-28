import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import {
  CoachingCalendarSubscriptionApi,
  CoachingCalendarSubscriptionComponent,
  coachingCalendarLinks
} from '../../../../../shared/coaching-calendar-subscription.component';

describe('Coaching calendar subscription (roadmap O6)', () => {
  it('builds absolute, webcal and Google Calendar addresses from the feed path', () => {
    const links = coachingCalendarLinks('/api/calendar/feed/abc_DEF-123.ics', 'https://onuraltintas.net');
    expect(links.https).toBe('https://onuraltintas.net/api/calendar/feed/abc_DEF-123.ics');
    expect(links.webcal).toBe('webcal://onuraltintas.net/api/calendar/feed/abc_DEF-123.ics');
    expect(links.google).toBe('https://calendar.google.com/calendar/render?cid=' + encodeURIComponent(links.webcal));
  });

  it('calls the subscription endpoints with the role', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = new CoachingCalendarSubscriptionApi(TestBed.inject(HttpClient), '/api');
    const http = TestBed.inject(HttpTestingController);
    api.status('teacher').subscribe(); api.create('student').subscribe(); api.revoke('teacher').subscribe();
    const get = http.expectOne(r => r.method === 'GET' && r.url === '/api/calendar/subscription');
    expect(get.request.params.get('role')).toBe('teacher');
    const post = http.expectOne(r => r.method === 'POST' && r.url === '/api/calendar/subscription');
    expect(post.request.params.get('role')).toBe('student');
    const del = http.expectOne(r => r.method === 'DELETE' && r.url === '/api/calendar/subscription');
    expect(del.request.params.get('role')).toBe('teacher');
    http.verify();
  });

  function setup(active = false) {
    const api = {
      status: vi.fn(() => of({ role: 'Student', active, createdAt: active ? '2030-01-01T10:00:00Z' : null, lastUsedAt: null })),
      create: vi.fn(() => of({ role: 'Student', path: '/api/calendar/feed/tok.ics', createdAt: '2030-01-02T10:00:00Z' })),
      revoke: vi.fn(() => of(undefined))
    };
    TestBed.configureTestingModule({ imports: [CoachingCalendarSubscriptionComponent] });
    const fixture = TestBed.createComponent(CoachingCalendarSubscriptionComponent);
    fixture.componentRef.setInput('api', api);
    fixture.componentRef.setInput('role', 'student');
    fixture.componentRef.setInput('origin', 'https://example.test');
    fixture.detectChanges();
    const click = (id: string) => { (fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement).click(); fixture.detectChanges(); };
    return { fixture, api, click };
  }

  it('creates a link once and shows it with copy, Google and webcal actions', () => {
    const { fixture, api, click } = setup();
    expect(api.status).toHaveBeenCalledWith('student');
    click('calendar-create');
    expect(api.create).toHaveBeenCalledWith('student');
    const root = fixture.nativeElement as HTMLElement;
    expect((root.querySelector('input') as HTMLInputElement).value).toBe('https://example.test/api/calendar/feed/tok.ics');
    expect(root.querySelector('[data-testid="calendar-webcal"]')!.getAttribute('href')).toBe('webcal://example.test/api/calendar/feed/tok.ics');
    expect(root.querySelector('[data-testid="calendar-google"]')!.getAttribute('rel')).toContain('noopener');
    expect(root.textContent).toContain('paylaşma');
  });

  it('asks before replacing or closing an active link', () => {
    const { fixture, api, click } = setup(true);
    expect(fixture.nativeElement.textContent).toContain('yalnız oluşturulduğu anda gösterilir');
    click('calendar-rotate');
    expect(api.create).not.toHaveBeenCalled();
    click('calendar-rotate-confirm');
    expect(api.create).toHaveBeenCalledOnce();
    click('calendar-revoke');
    expect(api.revoke).not.toHaveBeenCalled();
    click('calendar-revoke-confirm');
    expect(api.revoke).toHaveBeenCalledWith('student');
    expect(fixture.nativeElement.querySelector('[data-testid="calendar-subscription-link"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="calendar-create"]')).not.toBeNull();
  });

  it('shows an error when the link cannot be created', () => {
    const { fixture, api, click } = setup();
    api.create.mockReturnValueOnce(throwError(() => new Error('down')));
    click('calendar-create');
    expect(fixture.nativeElement.textContent).toContain('oluşturulamadı');
    expect(fixture.nativeElement.querySelector('[data-testid="calendar-subscription-link"]')).toBeNull();
  });
});
