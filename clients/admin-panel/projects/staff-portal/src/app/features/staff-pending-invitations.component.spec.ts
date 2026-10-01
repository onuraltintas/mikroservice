import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StaffPendingInvitationsComponent } from './staff-pending-invitations.component';

describe('StaffPendingInvitationsComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [StaffPendingInvitationsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists only the selected product pending invitations and confirms before cancellation', () => {
    const fixture = TestBed.createComponent(StaffPendingInvitationsComponent);
    fixture.componentInstance.product = 'coaching';
    fixture.detectChanges();
    http.expectOne('/api/invitations/sent-pending').flush([{
      invitationId: 'coaching-invite-1',
      email: 'student@example.test',
      role: 'StudentToTeacher',
      createdAt: '2026-09-28T10:00:00Z',
      expiresAt: '2026-10-05T10:00:00Z',
    }]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('student@example.test');
    expect(fixture.nativeElement.textContent).toContain('Öğrenci daveti');
    const requestCancel = fixture.nativeElement.querySelector('[data-testid="request-cancel-invitation"]') as HTMLButtonElement;
    requestCancel.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Bu bekleyen davet iptal edilsin mi?');

    const confirmCancel = fixture.nativeElement.querySelector('[data-testid="confirm-cancel-invitation"]') as HTMLButtonElement;
    confirmCancel.click();
    fixture.detectChanges();
    http.expectOne('/api/invitations/coaching-invite-1/cancel').flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Davet iptal edildi');
    expect(fixture.nativeElement.textContent).toContain('Bekleyen davet yok');
  });

  it('refreshes the selected product list when the parent signals a newly sent invitation', () => {
    const fixture = TestBed.createComponent(StaffPendingInvitationsComponent);
    fixture.componentRef.setInput('product', 'speed-reading');
    fixture.componentRef.setInput('refreshKey', 0);
    fixture.detectChanges();
    http.expectOne('/api/speed-reading/invitations/sent-pending').flush([]);
    fixture.componentRef.setInput('refreshKey', 1);
    fixture.detectChanges();
    http.expectOne('/api/speed-reading/invitations/sent-pending').flush([]);

    expect(fixture.nativeElement.textContent).toContain('Hızlı Okuma');
  });

  it('reports a friendly error when listing pending invitations fails', () => {
    const fixture = TestBed.createComponent(StaffPendingInvitationsComponent);
    fixture.componentInstance.product = 'coaching';
    fixture.detectChanges();
    http.expectOne('/api/invitations/sent-pending').flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Bekleyen davetler yüklenemedi');
    expect(fixture.nativeElement.querySelector('[data-testid="refresh-pending-invitations"]')).toBeTruthy();
  });

  it('allows retrying cancellation after the API rejects it', () => {
    const fixture = TestBed.createComponent(StaffPendingInvitationsComponent);
    fixture.componentInstance.product = 'coaching';
    fixture.detectChanges();
    http.expectOne('/api/invitations/sent-pending').flush([{
      invitationId: 'coaching-invite-2',
      email: 'teacher@example.test',
      role: 'TeacherToInstitution',
      createdAt: '2026-09-28T10:00:00Z',
      expiresAt: '2026-10-05T10:00:00Z',
    }]);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="request-cancel-invitation"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="confirm-cancel-invitation"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    http.expectOne('/api/invitations/coaching-invite-2/cancel').flush({}, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Davet artık beklemede değil');
    expect(fixture.nativeElement.querySelector('[data-testid="confirm-cancel-invitation"]')).toBeTruthy();
    expect(fixture.componentInstance.isCancelling()).toBe(false);
  });
});
