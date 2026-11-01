import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { parentPanelLinks } from '../../core/utils/parent-panel-links';
import { AuthService } from '../../core/services/auth.service';
import { GoogleIdentityService } from '../../core/services/google-identity.service';
import { PlatformLegalPagesService } from '../../core/services/platform-legal-pages.service';
import { SubscriptionService } from '../../core/services/subscription.service';
import { ToasterService } from '../../core/services/toaster.service';
import { LoginComponent } from './login/login.component';
import { ParentPanelEntryComponent } from './parent-panel-entry.component';
import { RegisterComponent } from './register/register.component';

/** V6b (decision 01.11.2026): a parent who lands on the speed-reading site finds the way to the parent panel. */
describe('ParentPanelEntryComponent (Veli misin?)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: jasmine.createSpyObj('AuthService', ['login', 'googleAuth', 'register', 'completeGoogleRegistration']) },
        { provide: GoogleIdentityService, useValue: {
          renderButton: jasmine.createSpy('renderButton').and.returnValue(Promise.resolve()),
          clearCallback: jasmine.createSpy('clearCallback')
        } },
        { provide: SubscriptionService, useValue: { getMyModules: () => of({ hasSpeedReading: true }) } },
        { provide: ToasterService, useValue: jasmine.createSpyObj('ToasterService', ['error', 'success']) },
        { provide: PlatformLegalPagesService, useValue: { getPage: (slug: string) => of({
          slug, title: slug, content: 'Metin', isPublished: true, version: 1, createdAt: '2026-10-01T00:00:00Z' }) } }
      ]
    });
  });

  function expectEntry(root: HTMLElement): void {
    const entry = root.querySelector<HTMLElement>('[data-testid="parent-panel-entry"]');
    expect(entry).withContext('parent entry block').toBeTruthy();
    expect(entry!.textContent).toContain('Veli misin?');
    expect(entry!.textContent).toContain('Çocuğunun hızlı okuma ilerlemesini Veli panelinden izleyebilirsin.');
    const login = entry!.querySelector<HTMLAnchorElement>('[data-testid="parent-panel-login"]')!;
    const register = entry!.querySelector<HTMLAnchorElement>('[data-testid="parent-panel-register"]')!;
    expect(login.textContent?.trim()).toBe('Veli paneline giriş');
    expect(register.textContent?.trim()).toBe('Veli olarak kaydol');
    expect(login.href).toBe(parentPanelLinks().login);
    expect(register.href).toBe(parentPanelLinks().register);
  }

  it('names the parent panel and links to the coaching-site parent login and register pages', () => {
    const fixture = TestBed.createComponent(ParentPanelEntryComponent);
    fixture.detectChanges();
    expectEntry(fixture.nativeElement);
    const region = fixture.nativeElement.querySelector('section');
    expect(region.getAttribute('aria-labelledby')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#' + region.getAttribute('aria-labelledby'))).toBeTruthy();
  });

  it('appears on the login page', () => {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    expectEntry(fixture.nativeElement);
  });

  it('appears on the student register page', () => {
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    expectEntry(fixture.nativeElement);
  });
});
