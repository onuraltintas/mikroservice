import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { Observable, firstValueFrom, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CoachingOnboardingService, OnboardingView } from '../../../core/services/coaching-onboarding.service';
import { LocationService } from '../../../core/services/location.service';
import { OABT_FIELDS } from '../../../core/models/practice-exam';
import { CoachingParentService, ParentLinkCode } from '../parent/coaching-parent.service';
import { NotificationPreference, NotificationPreferencesApi } from '../../../../../shared/notification-preferences.component';
import { ComboOption, ComboPage, OnboardingComboboxComponent } from './onboarding-combobox.component';
import {
  Answer, Answers, Choice, FlowContext, Question, QuestionId, STEP_COUNT, STEP_TITLES, StepNumber, autoAnswer, change, currentStep, examRequest,
  formatScore, nextQuestion, personalRequest, prefill, questions, signature, stepComplete, summary, targetRequest
} from './onboarding-model';

/** The reminder e-mails the wizard offers (roadmap O8 categories); the defaults are the student's current settings. */
export const ONBOARDING_EMAIL_CATEGORIES = ['StudyPlan', 'AssignmentReminders', 'Sessions'];
interface Invitation { id: string; inviterEmail: string | null; message: string | null }
const whole = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 0 });

/**
 * Paket C (#3, user decisions 05.11.2026): the mandatory first-login wizard, a full-screen chat. Questions come one by one;
 * every answer comes from a component and stays as a bubble with "Değiştir". Each step is saved on its own as soon as its
 * questions are answered, so the student resumes where they left off. The rest of the panel stays closed until the summary
 * is confirmed (see onboarding.guard.ts).
 */
@Component({
  selector: 'app-student-onboarding',
  standalone: true,
  imports: [RouterLink, OnboardingComboboxComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './student-onboarding.component.html',
  styleUrl: './student-onboarding.component.scss'
})
export class StudentOnboardingComponent implements OnInit {
  private readonly api = inject(CoachingOnboardingService);
  private readonly locations = inject(LocationService);
  private readonly parents = inject(CoachingParentService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);
  readonly notifications = new NotificationPreferencesApi(this.http, environment.apiUrl);

  readonly view = signal<OnboardingView | null>(null);
  readonly answers = signal<Answers>({});
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly done = signal<{ needsSubscription: boolean } | null>(null);
  /** 'target': after the wizard, "Hedefini belirle" asks only the target question, then goes back to Bugün. */
  readonly mode = signal<'wizard' | 'target'>('wizard');
  readonly locked = computed(() => !!this.answers().targetSaved?.data?.['locked']);
  readonly prefsError = signal(false);
  /** Step → the signature the server has (only for steps the server reports as saved). */
  private readonly saved = signal<Partial<Record<StepNumber, string>>>({});

  readonly context = computed<FlowContext>(() => ({ sessions: this.view()?.examSessions ?? [], academicYearStart: this.view()?.academicYearStart ?? new Date().getFullYear() }));
  readonly flow = computed(() => questions(this.answers(), this.context()));
  readonly current = computed(() => this.mode() === 'target'
    ? this.flow().find(x => x.step === 3 && !(x.id in this.answers())) ?? null
    : nextQuestion(this.answers(), this.context()));
  /** The conversation so far: every applicable question up to the current one (in target mode only the target question). */
  readonly asked = computed(() => {
    const list = this.mode() === 'target' ? this.flow().filter(x => x.step === 3) : this.flow(); const current = this.current();
    return current ? list.slice(0, list.findIndex(x => x.id === current.id)) : list;
  });
  readonly step = computed(() => currentStep(this.answers(), this.context()));
  readonly stepTitle = computed(() => STEP_TITLES[this.step()]);
  readonly stepCount = STEP_COUNT;
  readonly summary = computed(() => summary(this.answers(), this.context()));

  // Lists for the current question.
  readonly provinces = signal<ComboOption[]>([]);
  readonly districts = signal<ComboOption[]>([]);
  readonly cityOptions = signal<ComboOption[] | null>(null);
  readonly typeChoices = signal<Choice[]>([]);
  readonly universities = signal<ComboOption[] | null>(null);
  readonly invitations = signal<Invitation[] | null>(null);
  readonly parentCode = signal<ParentLinkCode | null>(null);
  readonly emailPrefs = signal<NotificationPreference[] | null>(null);
  readonly oabtOptions: ComboOption[] = [...Object.entries(OABT_FIELDS).map(([value, label]) => ({ value, label })),
    { value: 'none', label: 'ÖABT\'ye girmeyeceğim (yalnız AGS)' }];
  /** The server accepts a birth date between 100 and 6 years ago (Türkiye date). */
  readonly oldest = shiftYears(-100);
  readonly youngest = shiftYears(-6);
  draftNumber: number | null = null;
  draftDate = '';
  draftEstimated = false;

  async ngOnInit() {
    try {
      const view = await firstValueFrom(this.api.get());
      const targetOnly = this.route.snapshot.queryParamMap.get('step') === 'target';
      if (view.status === 'Completed' && !targetOnly) { await this.router.navigate(['/coaching-portal']); return; }
      if (view.status === 'Completed') this.mode.set('target');
      this.view.set(view);
      const answers = prefill(view);
      this.answers.set(this.mode() === 'target' && !view.answers.targetLocked ? change(answers, 'targetSaved') : answers);
      this.markSaved(view);
      if (this.mode() === 'target') { await this.settle(); return; }
      this.locations.getProvinces().subscribe({ next: list => this.provinces.set(list.map(x => ({ value: x.id, label: x.name }))),
        error: () => this.error.set('Şehir listesi yüklenemedi. Sayfayı yenile.') });
      await this.settle();
    } catch { this.error.set('Kurulum bilgilerin yüklenemedi. Sayfayı yenileyip tekrar dene.'); }
    finally { this.loading.set(false); }
  }

  // ---------- Answering ----------

  answer(question: Question, answer: Answer) {
    if (this.busy()) return;
    this.answers.update(current => ({ ...change(current, question.id), [question.id]: answer }));
    void this.settle();
  }
  choose(question: Question, choice: Choice) { this.answer(question, { value: choice.value, label: choice.label }); }
  skip(question: Question) { this.answer(question, { value: null, label: question.skipLabel ?? 'Geç' }); }
  pickCombo(question: Question, option: ComboOption) { this.answer(question, { value: option.value, label: option.label, data: option.data }); }

  submitDate(question: Question) {
    if (!this.draftDate || this.draftDate < this.oldest || this.draftDate > this.youngest) { this.error.set('Doğum tarihini kontrol et.'); return; }
    this.error.set(null);
    this.answer(question, { value: this.draftDate, label: this.draftDate.split('-').reverse().join('.') });
  }
  submitNumber(question: Question) {
    const value = this.draftNumber; const min = question.min ?? 0; const max = question.max ?? 1000;
    // A score is above 0 (the server's rule); the diploma grade is 50–100.
    if (value === null || !Number.isFinite(value) || value < min || min === 0 && value <= 0 || value > max || Math.round(value * 100) !== value * 100) {
      this.error.set(min === 0 ? `0'dan büyük, en çok ${max} olan bir sayı yaz.` : `${min} ile ${max} arasında bir sayı yaz.`); return;
    }
    this.error.set(null);
    const estimated = question.kind === 'diploma' && this.draftEstimated;
    this.answer(question, { value, label: `${formatScore(value)}${estimated ? ' (tahmini)' : ''}`, data: question.kind === 'diploma' ? { estimated } : undefined });
  }
  /** "Değiştir": the answer and only the answers that depend on it are asked again. */
  edit(id: QuestionId) {
    if (this.busy()) return;
    this.error.set(null); this.done.set(null);
    this.answers.update(current => change(current, id));
    void this.settle();
  }
  editStep(step: StepNumber) {
    const first = this.flow().find(x => x.step === step);
    if (first) this.edit(first.id);
  }

  /** Steps 4–6 are passed after their own action (accepting an invitation, a parent code, e-mail choices). */
  pass(question: Question, label: string) { this.answer(question, { value: 'passed', label }); }

  async acceptInvitation(invitation: Invitation) {
    await this.run(async () => {
      await firstValueFrom(this.http.post(`${environment.apiUrl}/coaching/invitations/${encodeURIComponent(invitation.id)}/accept`, {}));
      this.invitations.update(list => (list ?? []).filter(x => x.id !== invitation.id));
    }, 'Davet kabul edilemedi. Yeniden dene.');
  }
  async createParentCode() {
    await this.run(async () => this.parentCode.set(await firstValueFrom(this.parents.createCode())), 'Veli kodu oluşturulamadı. Yeniden dene.');
  }
  toggleEmail(category: string, event: Event) {
    const email = (event.target as HTMLInputElement).checked;
    this.emailPrefs.update(list => (list ?? []).map(x => x.category === category ? { ...x, email } : x));
  }
  loadEmails() {
    this.prefsError.set(false); this.emailPrefs.set(null);
    this.notifications.list().subscribe({ next: list => this.emailPrefs.set(list.filter(x => ONBOARDING_EMAIL_CATEGORIES.includes(x.category) && x.emailEditable !== false)),
      error: () => this.prefsError.set(true) });
  }
  async logout() { this.api.reset(); await this.auth.logout(); }
  async saveEmails(question: Question) {
    const prefs = this.emailPrefs();
    if (!prefs) return;
    await this.run(async () => {
      if (prefs?.length) await firstValueFrom(this.notifications.save(prefs.map(({ category, inApp, email, sms, whatsApp }) => ({ category, inApp, email, sms: !!sms, whatsApp: !!whatsApp }))));
    }, 'E-posta tercihlerin kaydedilemedi. Yeniden dene.');
    if (!this.error()) this.pass(question, prefs?.filter(x => x.email).map(x => x.label).join(', ') || 'E-posta istemiyorum');
  }

  async finish() {
    await this.run(async () => {
      const result = await firstValueFrom(this.api.complete());
      this.done.set({ needsSubscription: result.needsSubscription });
      if (result.needsSubscription) await this.router.navigate(['/coaching-portal/account/payments'], { queryParams: { required: 1, from: 'welcome' } });
    }, 'Kurulum tamamlanamadı. Yeniden dene.');
  }

  // ---------- Saving and the next question ----------

  /** Fills single-choice answers, saves every completed step the server does not have yet, then prepares the next question. */
  private async settle() {
    for (let guard = 0; guard < 20; guard++) {
      const next = this.current();
      const auto = next ? autoAnswer(next) : null;
      if (next && auto) { this.answers.update(current => ({ ...current, [next.id]: auto })); continue; }
      const steps = (this.mode() === 'target' ? [3] : [1, 2, 3, 4, 5, 6]) as StepNumber[];
      const pending = steps.find(step => this.saved()[step] !== signature(this.answers(), this.context(), step));
      if (!pending || !stepComplete(this.answers(), this.context(), pending)) break;
      const view = await this.saveStep(pending);
      if (!view) return;
      this.view.set(view);
      this.markSaved(view);
      if (this.mode() === 'target' && pending === 3) { await this.router.navigate(['/coaching-portal']); return; }
    }
    this.prepare();
  }

  private async saveStep(step: StepNumber): Promise<OnboardingView | null> {
    this.busy.set(true); this.error.set(null);
    try {
      const answers = this.answers();
      const request: Observable<OnboardingView> = step === 1 ? this.api.savePersonal(personalRequest(answers))
        : step === 2 ? this.api.saveExam(examRequest(answers)) : step === 3 ? this.api.saveTarget(targetRequest(answers))
        : this.api.passStep(step as 4 | 5 | 6);
      return await firstValueFrom(request);
    } catch (e) {
      this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Kaydedilemedi. Bağlantını kontrol edip yeniden dene.');
      return null;
    } finally { this.busy.set(false); }
  }

  retry() { this.error.set(null); void this.settle(); }

  /** A step counts as saved only while the server reports it; a re-opened step is sent again. */
  private markSaved(view: OnboardingView) {
    const answers = this.answers(); const context = this.context();
    const saved: Partial<Record<StepNumber, string>> = {};
    for (const step of view.savedSteps) if (step >= 1 && step <= 6) saved[step as StepNumber] = signature(answers, context, step as StepNumber);
    this.saved.set(saved);
  }

  private async run(action: () => Promise<void>, failure: string) {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(null);
    try { await action(); } catch (e) { this.error.set((e as { error?: { message?: string } })?.error?.message ?? failure); }
    finally { this.busy.set(false); }
  }

  /** Loads what the current question needs and moves the focus to it (the log also announces it). */
  private prepare() {
    const question = this.current();
    this.draftNumber = null; this.draftEstimated = false; this.draftDate = '';
    const a = this.answers();
    const val = (id: QuestionId) => String(a[id]?.value ?? '');
    switch (question?.id) {
      case 'district':
        this.districts.set([]);
        this.locations.getDistricts(val('city')).subscribe({ next: list => this.districts.set(list.map(x => ({ value: x.id, label: x.name }))),
          error: () => this.error.set('İlçe listesi yüklenemedi.') });
        break;
      case 'schoolCity': this.options(this.api.schoolCities()); break;
      case 'schoolType':
        this.typeChoices.set([]);
        this.api.schoolTypes(val('schoolCity')).subscribe({ next: list => this.typeChoices.set(list.map(x => ({ value: x.value, label: `${x.value} (${x.count})` }))),
          error: () => this.error.set('Okul türleri yüklenemedi.') });
        break;
      case 'programCity': this.options(this.api.programCities(val('level'), this.programScoreType())); break;
      case 'university':
        this.universities.set(null);
        this.api.universities(val('level'), this.programScoreType(), val('programCity')).subscribe({
          next: list => this.universities.set(list.map(x => ({ value: x.value, label: x.value, hint: `${x.count} bölüm` }))),
          error: () => this.error.set('Üniversite listesi yüklenemedi.') });
        break;
      case 'invite':
        this.http.get<Invitation[]>(`${environment.apiUrl}/coaching/invitations/my-invitations`).subscribe({
          next: list => this.invitations.set(list), error: () => this.invitations.set([]) });
        break;
      case 'notifications': this.loadEmails(); break;
      case 'targetScore': {
        const level = a.ydsLevel?.value;
        if (typeof level === 'string') this.draftNumber = Number(level);
        break;
      }
    }
    setTimeout(() => (this.host.nativeElement.querySelector('[data-current] input, [data-current] button') as HTMLElement | null)?.focus());
  }

  private options(source: Observable<{ value: string; label?: string; count: number }[]>) {
    this.cityOptions.set(null);
    source.subscribe({ next: list => this.cityOptions.set(list.map(x => ({ value: x.value, label: x.label ?? x.value }))), error: () => this.error.set('Liste yüklenemedi.') });
  }
  private programScoreType(): string | null {
    const a = this.answers();
    return a.level?.value === 'Associate' ? 'TYT' : (a.programScoreType?.value as string | null) ?? null;
  }

  readonly loadOwnSchools = (search: string, page: number): Observable<ComboPage> =>
    this.api.schools(this.answers().city?.label ?? '', null, search, page).pipe(map(r => ({ total: r.totalCount,
      items: r.items.map(x => ({ value: x.id, label: x.name, hint: x.district ?? undefined })) })));
  readonly loadSchools = (search: string, page: number): Observable<ComboPage> =>
    this.api.schools(String(this.answers().schoolCity?.value ?? ''), String(this.answers().schoolType?.value ?? ''), search, page).pipe(map(r => ({ total: r.totalCount,
      items: r.items.map(x => ({ value: x.id, label: x.name, data: { minimumScore: x.minimumScore },
        hint: [x.district, x.minimumScore !== null ? `taban yaklaşık ${formatScore(x.minimumScore)}` : 'taban puanı yok'].filter(Boolean).join(' · ') })) })));
  readonly loadPrograms = (search: string, page: number): Observable<ComboPage> => {
    const a = this.answers();
    return this.api.programs(String(a.level?.value), this.programScoreType(), String(a.programCity?.value ?? ''), a.university?.label ?? '', search, page).pipe(map(r => ({
      total: r.totalCount,
      items: r.items.map(x => ({ value: x.id, label: [x.name, x.faculty].filter(Boolean).join(' · '), data: { minimumScore: x.minimumScore },
        hint: [x.minimumScore !== null ? `taban ${formatScore(x.minimumScore)}` : 'taban puanı yok', x.rank !== null ? `başarı sırası ${whole.format(x.rank)}` : null,
          x.scholarship, x.educationType].filter(Boolean).join(' · ') })) })));
  };
  comboOptions(question: Question): ComboOption[] | null {
    switch (question.id) {
      case 'city': return this.provinces();
      case 'district': return this.districts();
      case 'oabtField': return this.oabtOptions;
      case 'schoolCity': case 'programCity': return this.cityOptions() ?? [];
      case 'university': return this.universities() ?? [];
      default: return null;
    }
  }
  comboLoader(question: Question) {
    return question.id === 'ownSchool' ? this.loadOwnSchools : question.id === 'targetSchool' ? this.loadSchools : question.id === 'program' ? this.loadPrograms : null;
  }
  choicesOf(question: Question): Choice[] { return question.id === 'schoolType' ? this.typeChoices() : question.choices ?? []; }
  numberInput(event: Event) { const raw = (event.target as HTMLInputElement).value; this.draftNumber = raw === '' ? null : Number(raw); }
  dateInput(event: Event) { this.draftDate = (event.target as HTMLInputElement).value; }
  estimatedInput(event: Event) { this.draftEstimated = (event.target as HTMLInputElement).checked; }
  stepOf(question: Question) { return question.step; }
}

/** Today ± years as YYYY-MM-DD in Türkiye (the server's birth-date window). */
export function shiftYears(years: number, now = new Date()): string {
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul' }).format(now);
  const [y, m, d] = today.split('-').map(Number);
  const shifted = new Date(Date.UTC(y + years, m - 1, d));
  if (shifted.getUTCMonth() !== m - 1) shifted.setUTCDate(0); // 29 Feb → 28 Feb
  return shifted.toISOString().slice(0, 10);
}
