import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { InstitutionSettingsComponent } from './institution-settings.component';
import { AuthService } from '../../core/services/auth.service';
import { UsersService } from '../../core/services/users.service';
import { InstitutionsService } from '../../core/services/institutions.service';
import { SubscriptionService } from '../../core/services/subscription.service';
import { LocationsService } from '../../core/services/locations.service';
import { ToasterService } from '../../core/services/toaster.service';

describe('InstitutionSettingsComponent', () => {
  it('resolves the institution from the authenticated profile when the login token has no institution claim', () => {
    TestBed.configureTestingModule({
      imports: [InstitutionSettingsComponent],
      providers: [
        { provide: AuthService, useValue: { currentUserValue: { roles: ['InstitutionAdmin'] } } },
        { provide: UsersService, useValue: { getMyProfile: () => of({ institutionId: 'institution-1' }) } },
        { provide: InstitutionsService, useValue: { getInstitutionById: () => of({ id: 'institution-1', name: 'Örnek Kurum' }) } },
        { provide: SubscriptionService, useValue: { getMyInstitutionAccess: () => of(null) } },
        { provide: LocationsService, useValue: { getProvinces: () => of([]), getDistricts: () => of([]) } },
        { provide: ToasterService, useValue: { error: jasmine.createSpy('error') } }
      ]
    });

    const component = TestBed.createComponent(InstitutionSettingsComponent).componentInstance;
    component.ngOnInit();

    expect(component.institutionId).toBe('institution-1');
    expect(component.institution()?.name).toBe('Örnek Kurum');
  });
});
