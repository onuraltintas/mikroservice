import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { InstitutionListComponent } from './institution-list';
import { InstitutionService } from '../../../core/services/institution.service';
import { IdentityService } from '../../../core/services/identity.service';
import { AuthService } from '../../../core/auth/auth.service';
import { LocationService } from '../../../core/services/location.service';

describe('InstitutionListComponent coaching navigation', () => {
  it('links the selected institution to both coaching rosters', async () => {
    await TestBed.configureTestingModule({
      imports: [InstitutionListComponent],
      providers: [provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: InstitutionService, useValue: { getAll: () => of({ items: [{ id: 'school-1', name: 'School', isActive: true }], totalCount: 1 }) } },
        { provide: IdentityService, useValue: {} },
        { provide: AuthService, useValue: { hasPermission: () => true, userProfile: () => ({ roles: ['SystemAdmin'] }) } },
        { provide: LocationService, useValue: { getProvinces: () => of([]) } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(InstitutionListComponent);
    fixture.detectChanges();
    const links = Array.from(fixture.nativeElement.querySelectorAll('a')) as HTMLAnchorElement[];
    expect(links.some(link => link.getAttribute('href') === '/dashboard/coaching/students?institutionId=school-1')).toBe(true);
    expect(links.some(link => link.getAttribute('href') === '/dashboard/coaching/teachers?institutionId=school-1')).toBe(true);
  });
});
