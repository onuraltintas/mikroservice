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

  it('accepts invitations through the Speed Reading service API', () => {
    const fixture = TestBed.createComponent(AcceptInvitationComponent);
    fixture.detectChanges();

    const request = http.expectOne('/api/speed-reading/invitations/invitation-1/accept');
    expect(request.request.method).toBe('POST');
    request.flush({ code: 'Invitation.Accepted', message: 'Hızlı Okuma daveti kabul edildi.' });

    expect(fixture.componentInstance.loading).toBeFalse();
    expect(fixture.componentInstance.error).toBe('');
  });
});
