import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '../core/auth/auth.service';
import { InstitutionListComponent } from './identity/pages/institution-list';
import { SupportInboxComponent } from './notifications/pages/support-inbox';

describe('large admin lists', () => {
  const auth = {
    userProfile: signal({ roles: ['SystemAdmin'] }),
    hasPermission: () => true
  };

  it('pages institutions on the server', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: auth }, { provide: PLATFORM_ID, useValue: 'browser' }]
    });
    const component = TestBed.runInInjectionContext(() => new InstitutionListComponent());
    const http = TestBed.inject(HttpTestingController);
    expectPage(http, '/institutions', '1').flush({ items: [], totalCount: 60, pageNumber: 1, pageSize: 25 });

    component.changePage(2);
    expectPage(http, '/institutions', '2').flush({ items: [], totalCount: 60, pageNumber: 2, pageSize: 25 });
    http.verify();
  });

  it('pages support requests on the server', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: auth }, { provide: PLATFORM_ID, useValue: 'browser' }]
    });
    const component = TestBed.runInInjectionContext(() => new SupportInboxComponent());
    const http = TestBed.inject(HttpTestingController);
    expectPage(http, '/support/requests', '1').flush({ items: [], totalCount: 60, pageNumber: 1, pageSize: 25 });

    component.changePage(2);
    expectPage(http, '/support/requests', '2').flush({ items: [], totalCount: 60, pageNumber: 2, pageSize: 25 });
    http.verify();
  });
});

function expectPage(http: HttpTestingController, path: string, page: string) {
  return http.expectOne(request =>
    request.url.endsWith(path)
    && request.params.get('pageNumber') === page
    && request.params.get('pageSize') === '25');
}
