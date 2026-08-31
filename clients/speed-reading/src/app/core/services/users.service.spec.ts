import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { UsersService } from './users.service';

describe('UsersService', () => {
  let service: UsersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [UsersService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(UsersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses the identity users contract for filtered admin lists', () => {
    service.getUsers('admin', 'SystemAdmin', true).subscribe();

    const request = http.expectOne(candidate => candidate.url === '/api/v1/users');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('100');
    expect(request.request.params.get('search')).toBe('admin');
    expect(request.request.params.get('role')).toBe('SystemAdmin');
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 100 });
  });

  it('sends role assignments using the identity command shape', () => {
    service.assignRole('user-1', { roleName: 'Teacher' }).subscribe();

    const request = http.expectOne('/api/v1/users/user-1/roles');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ roleName: 'Teacher' });
    request.flush(null);
  });
});
