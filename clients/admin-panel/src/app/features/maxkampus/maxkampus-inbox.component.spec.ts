import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { vi } from 'vitest';
import { ToasterService } from '../../core/services/toaster.service';
import { MaxkampusInboxComponent } from './maxkampus-inbox.component';

describe('MaxkampusInboxComponent', () => {
  const toaster = { success: vi.fn(), error: vi.fn() };
  const tick = () => new Promise(resolve => setTimeout(resolve));

  function setup(inbox: 'messages' | 'subscribers') {
    TestBed.configureTestingModule({
      imports: [MaxkampusInboxComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { inbox } } } },
        { provide: ToasterService, useValue: toaster }]
    });
    const fixture = TestBed.createComponent(MaxkampusInboxComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http: TestBed.inject(HttpTestingController) };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('opens on messages waiting for an answer and deletes one only after a second click', async () => {
    const { fixture, component, http } = setup('messages');
    http.expectOne('/api/site-admin/sites/maxkampus/messages?skip=0&take=50&status=open').flush({
      total: 1,
      items: [{ id: 'm1', name: 'TEST Kişi', organization: 'TEST Kurum', email: 'test@example.com', phone: null, message: 'TEST mesaj', createdAt: '2026-11-19T10:00:00Z', answeredAt: null }]
    });
    await tick();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Yanıt bekliyor');

    const item = component.messages()[0];
    await component.removeMessage(item);
    http.expectNone('/api/site-admin/sites/maxkampus/messages/m1');
    const deleting = component.removeMessage(item);
    http.expectOne({ method: 'DELETE', url: '/api/site-admin/sites/maxkampus/messages/m1' }).flush(null);
    await tick();
    http.expectOne('/api/site-admin/sites/maxkampus/messages?skip=0&take=50&status=open').flush({ total: 0, items: [] });
    await deleting;
    expect(toaster.success).toHaveBeenCalledWith('Mesaj silindi.');
  });

  it('marks a message answered', async () => {
    const { component, http } = setup('messages');
    http.expectOne('/api/site-admin/sites/maxkampus/messages?skip=0&take=50&status=open').flush({
      total: 1, items: [{ id: 'm1', name: 'TEST', organization: null, email: 'test@example.com', phone: null, message: 'TEST', createdAt: '2026-11-19T10:00:00Z', answeredAt: null }]
    });
    await tick();
    const marking = component.answered(component.messages()[0], true);
    const request = http.expectOne('/api/site-admin/sites/maxkampus/messages/m1/answered');
    expect(request.request.body).toEqual({ answered: true });
    request.flush(null);
    await tick();
    http.expectOne('/api/site-admin/sites/maxkampus/messages?skip=0&take=50&status=open').flush({ total: 0, items: [] });
    await marking;
  });

  it('opens on confirmed newsletter addresses', async () => {
    const { fixture, http } = setup('subscribers');
    http.expectOne('/api/site-admin/sites/maxkampus/subscribers?skip=0&take=100&status=Confirmed').flush({
      total: 1, items: [{ id: 's1', email: 'test@example.com', status: 'Confirmed', source: 'footer', createdAt: '2026-11-19T10:00:00Z', confirmedAt: '2026-11-19T10:05:00Z', unsubscribedAt: null }]
    });
    await tick();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Onaylı adresleri indir');
    expect(fixture.nativeElement.textContent).toContain('test@example.com');
  });
});
