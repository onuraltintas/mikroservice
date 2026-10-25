import { ToasterService } from '../../../../src/app/core/services/toaster.service';
import { ChangeDetectorRef, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { DeferBlockBehavior, TestBed } from '@angular/core/testing';
import { DarkModeService } from '../../../../src/app/core/services/dark-mode.service';
import { vi } from 'vitest';
import { StaffAuthService } from './auth/staff-auth.service';
import { StaffProductAccess } from './auth/staff-auth.models';
import { AppComponent } from './app.component';

describe('Staff login rejection rendering', () => {
  it('releases the submit button and renders a CAPTCHA rejection after the async response', async () => {
    let rejectLogin!: (error: unknown) => void;
    TestBed.configureTestingModule({ imports: [AppComponent], providers: [{ provide: StaffAuthService, useValue: {
      login: vi.fn().mockImplementation(() => new Promise((_, reject) => { rejectLogin = reject; }))
    } }] });
    TestBed.overrideComponent(AppComponent, { set: { template: '<button [disabled]="isSubmitting" (click)="submitLogin()">Giriş</button><p role="alert">{{errorMessage}}</p>' } });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('button').disabled).toBe(true);
    rejectLogin(new HttpErrorResponse({ status: 403, error: {
      code: 'Auth.CaptchaFailed', message: 'Güvenlik doğrulaması tamamlanamadı. Lütfen tekrar deneyin.'
    } }));
    await new Promise(resolve => setTimeout(resolve, 0));
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('button').disabled).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Güvenlik doğrulaması tamamlanamadı');
  });
});

describe('Staff portal MFA flow', () => {
  it('starts the enrollment setup when Identity requires first-time MFA enrollment', async () => {
    const auth = {
      login: vi.fn().mockResolvedValue({
        requiresMfa: true,
        mfaEnrollmentRequired: true,
        mfaChallengeToken: 'mfa-challenge'
      }),
      startMfaSetup: vi.fn().mockResolvedValue({
        secret: 'TOTP-SECRET',
        otpAuthUri: 'otpauth://example',
        setupToken: 'setup-token'
      })
    };
    const component = createComponent(auth);
    component.email = 'manager@example.test';
    component.password = 'password';

    await component.submitLogin();

    expect(auth.startMfaSetup).toHaveBeenCalledWith('mfa-challenge', 'coaching');
    expect((component as unknown as { mfaStage: string }).mfaStage).toBe('setup');
    expect((component as unknown as { mfaSecret: string }).mfaSecret).toBe('TOTP-SECRET');
  });

  it('shows one-time recovery codes after MFA enrollment succeeds', async () => {
    const auth = {
      enableMfa: vi.fn().mockResolvedValue(['ONE-TIME-01']),
      verifyMfa: vi.fn().mockResolvedValue(undefined)
    };
    const component = createComponent(auth);
    const state = component as unknown as {
      mfaStage: string;
      mfaChallengeToken: string;
      mfaSetupToken: string;
      mfaCode: string;
      recoveryCodes: string[];
    };
    state.mfaStage = 'setup';
    state.mfaChallengeToken = 'mfa-challenge';
    state.mfaSetupToken = 'setup-token';
    state.mfaCode = '123456';

    await component.submitMfa();

    expect(auth.enableMfa).toHaveBeenCalledWith('coaching', 'mfa-challenge', 'setup-token', '123456');
    expect(state.mfaStage).toBe('recovery');
    expect(state.recoveryCodes).toEqual(['ONE-TIME-01']);
  });

  it('allows an existing MFA user to authenticate with a recovery code', async () => {
    const auth = {
      verifyMfa: vi.fn().mockResolvedValue(undefined)
    };
    const component = createComponent(auth);
    const state = component as unknown as {
      mfaChallengeToken: string;
      mfaRecoveryCode: string;
      submitRecoveryCode: () => Promise<void>;
    };
    state.mfaChallengeToken = 'mfa-challenge';
    state.mfaRecoveryCode = 'RECOVERY-01';

    await state.submitRecoveryCode();

    expect(auth.verifyMfa).toHaveBeenCalledWith('coaching', 'mfa-challenge', null, 'RECOVERY-01');
  });
});

describe('Staff portal workspace role selection', () => {
  it.each(['role', 'product', 'logout'])('preserves the teacher plan when %s navigation is declined', async transition => {
    const auth = { products: signal<StaffProductAccess[]>([{ product: 'coaching', roles: ['Teacher', 'InstitutionAdmin'] }]),
      activeProduct: signal<'coaching'>('coaching'), switchProduct: vi.fn().mockResolvedValue(undefined), logout: vi.fn().mockResolvedValue(undefined) };
    const component = createComponent(auth);
    (await component.selectWorkspaceRole('teacher'));
    component.coachingTeacherWorkspace = { canLeavePage: () => false } as any;
    if (transition === 'role') (await component.selectWorkspaceRole('institution'));
    else if (transition === 'product') await component.switchProduct('speed-reading');
    else await component.logout();
    expect(component.activeWorkspaceRole).toBe('teacher');
    expect(auth.switchProduct).not.toHaveBeenCalled();
    expect(auth.logout).not.toHaveBeenCalled();
  });
  it('defaults dual-role coaching accounts to the institution workspace without rendering both panels', () => {
    const component = createWorkspaceComponent();

    expect(component.activeWorkspaceRole).toBe('institution');
    expect(component.canSwitchWorkspaceRole).toBe(true);
    expect(component.isInstitutionWorkspaceSelected).toBe(true);
    expect(component.isTeacherWorkspaceSelected).toBe(false);
  });

  it('switches to the teacher workspace when the user selects it', async () => {
    const component = createWorkspaceComponent();

    (await component.selectWorkspaceRole('teacher'));

    expect(component.activeWorkspaceRole).toBe('teacher');
    expect(component.isTeacherWorkspaceSelected).toBe(true);
    expect(component.isInstitutionWorkspaceSelected).toBe(false);
  });

  it('uses only roles granted for the currently active product', async () => {
    const activeProduct = signal<'coaching' | 'speed-reading'>('coaching');
    const component = createComponent({
      products: signal<StaffProductAccess[]>([
        { product: 'coaching', roles: ['Teacher', 'InstitutionAdmin'] },
        { product: 'speed-reading', roles: ['Teacher'] }
      ]),
      activeProduct
    });

    (await component.selectWorkspaceRole('teacher'));
    activeProduct.set('speed-reading');
    expect(component.activeWorkspaceRole).toBe('teacher');
    expect(component.isSpeedReadingTeacherWorkspaceSelected).toBe(true);
    expect(component.canSwitchWorkspaceRole).toBe(false);
    (await component.selectWorkspaceRole('institution'));
    expect(component.activeWorkspaceRole).toBe('teacher');

    activeProduct.set('coaching');
    expect(component.activeWorkspaceRole).toBe('teacher');
  });

  it('selects the Speed Reading institution workspace for that product role only', async () => {
    const component = createComponent({
      products: signal<StaffProductAccess[]>([
        { product: 'coaching', roles: ['Teacher'] },
        { product: 'speed-reading', roles: ['Teacher', 'InstitutionAdmin'] }
      ]),
      activeProduct: signal('speed-reading')
    });

    expect(component.activeWorkspaceRole).toBe('institution');
    expect(component.isSpeedReadingInstitutionWorkspaceSelected).toBe(true);
    (await component.selectWorkspaceRole('teacher'));
    expect(component.isSpeedReadingTeacherWorkspaceSelected).toBe(true);
    expect(component.isSpeedReadingInstitutionWorkspaceSelected).toBe(false);
  });
});

describe('Staff portal product deep links', () => {
  it('opens a weekly notification only after the teacher workspace accepts navigation',async ()=>{
    const component=createComponent({products:signal<StaffProductAccess[]>([{product:'coaching',roles:['Teacher']}]),activeProduct:signal('coaching')});
    const open=vi.fn(()=>false);component.coachingTeacherWorkspace={openWeeklyFromNotification:open}as unknown as NonNullable<AppComponent['coachingTeacherWorkspace']>;
    const target={studentId:'student',weekStart:'2026-10-12',decisionId:null};component.notificationsVisible=true;(await component.openCoachingWeekly(target));
    expect(open).toHaveBeenCalledWith(target);expect(component.notificationsVisible).toBe(true);open.mockReturnValue(true);(await component.openCoachingWeekly(target));expect(component.notificationsVisible).toBe(false);
  });
  it('opens a notification through the teacher workspace and keeps the inbox open if navigation is blocked',async ()=>{
    const component=createComponent({products:signal<StaffProductAccess[]>([{product:'coaching',roles:['Teacher']}]),activeProduct:signal('coaching')});
    const open=vi.fn(()=>false);
    component.coachingTeacherWorkspace={openAssignmentFromNotification:open} as unknown as NonNullable<AppComponent['coachingTeacherWorkspace']>;
    component.notificationsVisible=true;(await component.openCoachingAssignment('assignment-1'));
    expect(open).toHaveBeenCalledWith('assignment-1');expect(component.notificationsVisible).toBe(true);
    open.mockReturnValue(true);(await component.openCoachingAssignment('assignment-2'));expect(component.notificationsVisible).toBe(false);
  });
  it('blocks role, product and logout transitions while an institution membership save is pending', async () => {
    const auth = { products: signal<StaffProductAccess[]>([{ product: 'coaching', roles: ['Teacher', 'InstitutionAdmin'] }]),
      activeProduct: signal<'coaching'>('coaching'), switchProduct: vi.fn(), logout: vi.fn() };
    const component = createComponent(auth);
    component.coachingInstitutionWorkspace = { canLeavePage: async () => false };
    (await component.selectWorkspaceRole('teacher')); await component.switchProduct('speed-reading'); await component.logout();
    expect(component.activeWorkspaceRole).toBe('institution');
    expect(auth.switchProduct).not.toHaveBeenCalled(); expect(auth.logout).not.toHaveBeenCalled();
  });
  const initialUrl = window.location.href;

  afterEach(() => window.history.replaceState({}, '', initialUrl));

  it('preselects the Speed Reading product for legacy handoff links', () => {
    window.history.replaceState({}, '', '/staff/?product=speed-reading');

    expect(createComponent({}).selectedProduct).toBe('speed-reading');
  });

  it('uses Coaching for an explicit Coaching handoff', () => {
    window.history.replaceState({}, '', '/staff/?product=coaching');

    expect(createComponent({}).selectedProduct).toBe('coaching');
  });
});

describe('Coaching teacher design shell', () => {
  async function shell() {
    const dark=signal(false);
    const auth={isAuthenticated:signal(true),products:signal([{product:'coaching',roles:['Teacher']}]),activeProduct:signal('coaching')};
    TestBed.configureTestingModule({imports:[AppComponent],deferBlockBehavior:DeferBlockBehavior.Manual,providers:[
      {provide:StaffAuthService,useValue:auth},
      {provide:DarkModeService,useValue:{isDarkMode:dark,toggle:()=>dark.update(v=>!v)}}
    ]});
    const fixture=TestBed.createComponent(AppComponent); fixture.detectChanges(); await fixture.whenStable();
    return {fixture,dark,auth};
  }
  it('uses the common coaching shell and exposes the selected theme',async()=>{
    const {fixture,dark}=await shell();
    expect(fixture.nativeElement.querySelector('.coaching-staff-shell.coaching-shell')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.session-panel')).toBeNull();
    fixture.nativeElement.querySelector('[aria-label="Koyu temaya geç"]').click();fixture.detectChanges();
    expect(dark()).toBe(true);expect(fixture.nativeElement.querySelector('[aria-label="Açık temaya geç"]')).toBeTruthy();
  });
  it('closes the mobile menu with Escape and restores focus to its control',async()=>{
    const {fixture}=await shell(); const toggle=fixture.nativeElement.querySelector('[aria-label="Personel menüsünü aç"]') as HTMLButtonElement;
    toggle.click();fixture.detectChanges();expect(fixture.componentInstance.menuOpen()).toBe(true);
    fixture.componentInstance.closeTeacherMenuOnEscape();fixture.detectChanges();
    expect(fixture.componentInstance.menuOpen()).toBe(false);expect(document.activeElement).toBe(toggle);
  });
  it('does not apply coaching design classes to the speed reading workspace',async()=>{
    const {fixture,auth}=await shell();auth.activeProduct.set('speed-reading');fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.coaching-staff-shell')).toBeNull();
  });
  it('closes the drawer before awaiting a dirty-form decision and preserves the current product on cancel',async()=>{
    const {fixture,auth}=await shell();
    fixture.componentInstance.coachingTeacherWorkspace={canLeavePage:async()=>false} as unknown as NonNullable<AppComponent['coachingTeacherWorkspace']>;
    fixture.componentInstance.menuOpen.set(true);
    const navigation=fixture.componentInstance.chooseProductFromMenu('speed-reading');
    expect(fixture.componentInstance.menuOpen()).toBe(false);
    await navigation;expect(auth.activeProduct()).toBe('coaching');
  });
});

function createComponent(auth: Partial<StaffAuthService>): AppComponent {
  TestBed.configureTestingModule({
    providers: [{ provide: StaffAuthService, useValue: auth }, { provide: ChangeDetectorRef, useValue: { markForCheck: vi.fn() } }]
  });
  return TestBed.runInInjectionContext(() => new AppComponent());
}

function createWorkspaceComponent(): AppComponent {
  return createComponent({
    products: signal<StaffProductAccess[]>([{ product: 'coaching', roles: ['Teacher', 'InstitutionAdmin'] }]),
    activeProduct: signal('coaching')
  });
}

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
