import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AcceptInvitationComponent } from './accept-invitation.component';

describe('AcceptInvitationComponent', () => {
  let http: HttpTestingController;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    TestBed.configureTestingModule({
      imports: [AcceptInvitationComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: { get: () => 'invitation-1' } } }
        }
      ]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('explains that earlier work becomes visible and accepts only after the person confirms', () => {
    // Review H3 (decision 28.10.2026, same as coaching): the new institution or teacher sees the student's earlier
    // Speed Reading history, so the person is told before accepting.
    const fixture = TestBed.createComponent(AcceptInvitationComponent);
    fixture.detectChanges();

    http.expectNone('/api/speed-reading/invitations/invitation-1/accept');
    const page = fixture.nativeElement as HTMLElement;
    expect(page.textContent).toContain('önceki kurum ve öğretmenlerindeki');
    const accept = page.querySelector<HTMLButtonElement>('[data-testid="accept-invitation"]')!;
    accept.click();

    const request = http.expectOne('/api/speed-reading/invitations/invitation-1/accept');
    expect(request.request.method).toBe('POST');
    request.flush({ code: 'Invitation.Accepted', message: 'Hızlı Okuma daveti kabul edildi.' });

    expect(fixture.componentInstance.accepted).toBeTrue();
    expect(fixture.componentInstance.error).toBe('');
  });
});
