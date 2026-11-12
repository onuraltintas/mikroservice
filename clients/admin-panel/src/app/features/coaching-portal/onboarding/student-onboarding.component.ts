import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { Observable, catchError, firstValueFrom, forkJoin, map, of } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CoachingOnboardingService, OnboardingView } from '../../../core/services/coaching-onboarding.service';
import { LocationService } from '../../../core/services/location.service';
import { OABT_FIELDS } from '../../../core/models/practice-exam';
import { coachingDate } from '../coaching-labels';
import { CoachingParentService, ParentLinkCode, ParentLinkCodeStatus } from '../parent/coaching-parent.service';
import { NotificationPreference, NotificationPreferencesApi } from '../../../../../shared/notification-preferences.component';
import { ComboOption, ComboPage, OnboardingComboboxComponent, fold } from './onboarding-combobox.component';
import { ScoreTypeFound, ScoreTypeFinderComponent } from './score-type-finder.component';
import {
  Answer, Answers, Choice, FlowContext, Question, QuestionId, STEP_COUNT, TARGET_QUESTIONS, STEP_TITLES, StepNumber, autoAnswer, change, currentStep, examRequest,
  emailAnswer, formatScore, inviteAnswer, nextQuestion, parentAnswer, parentCodeNote, staleParentCode, personalRequest, prefill, questions, signature, stepComplete, stepQuestions,
  purposeOf, sessionAnswerLabel, summary, targetRequest, withoutUnfitPurpose, TYPED_SCHOOL_MAX, typedSchoolAnswer, typedSchoolError, fieldFoundNote
} from './onboarding-model';
import { CopyResult, copyMessage, copyText } from '../copy-text';
import { birthDateError, shiftYears } from '../birth-date';
import { coachingToaster } from '../coaching-feedback';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';

/** The reminder e-mails the wizard offers (roadmap O8 categories); the defaults are the student's current settings. */
export const ONBOARDING_EMAIL_CATEGORIES = ['StudyPlan', 'AssignmentReminders', 'Sessions'];
interface Invitation { id: string; inviterEmail: string | null; message: string | null; type?: string }
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
  imports: [RouterLink, OnboardingComboboxComponent, ScoreTypeFinderComponent],
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
  private readonly navigation = inject(CoachingStudentNavigationService);
  readonly notifications = new NotificationPreferencesApi(this.http, environment.apiUrl);
  /** E7 (#30): Hedefim embeds the target step: no page header, and a saved target stays on the page (no Bugün). */
  readonly embedded = input(false);
  /** E7: a new target was saved (Hedefim reloads its summary and net targets). */
  readonly targetSaved = output<void>();
  /** E7: the saved target shown first in target mode; "Vazgeç" goes back to it after "Değiştir". */
  readonly original = signal<Answer | null>(null);
  /** Review of PR 120: embedded before the first-login setup is completed (Hedefim then only links to it). */
  readonly notReady = signal(false);
  private readonly toaster = coachingToaster();
  /** The student has an exam, so the target step applies (otherwise "Önce sınavını seç"). */
  readonly hasExam = computed(() => !!purposeOf(this.answers()));
  /** Review of PR 120: a target change has been started and not saved (Hedefim asks before leaving). */
  readonly dirty = computed(() => this.mode() === 'target' && !this.answers().targetSaved
    && TARGET_QUESTIONS.some(id => id !== 'targetSaved' && id in this.answers()));
  /** E7: "Hedefin kaydedildi." after a save in Hedefim. */
  readonly saved$ = signal<string | null>(null);

  readonly view = signal<OnboardingView | null>(null);
  readonly answers = signal<Answers>({});
  readonly loading = signal(true);
  readonly busy = signal(false);
  /** A server or network error: shown with "Yeniden dene". */
  readonly error = signal<string | null>(null);
  /** E4 #18: an answer the wizard does not accept (e.g. diploma grade 45): shown at the question, without "Yeniden dene". */
  readonly invalid = signal<string | null>(null);
  readonly done = signal<{ needsSubscription: boolean } | null>(null);
  /** 'target': after the wizard, "Hedefini belirle" asks only the target question, then goes back to Bugün. */
  readonly mode = signal<'wizard' | 'target'>('wizard');
  readonly locked = computed(() => !!this.answers().targetSaved?.data?.['locked']);
  readonly prefsError = signal(false);
  /** Step → the signature the server has (only for steps the server reports as saved). */
  private readonly saved = signal<Partial<Record<StepNumber, string>>>({});

  readonly context = computed<FlowContext>(() => {
    const view = this.view(); const a = view?.answers;
    return { sessions: view?.examSessions ?? [], academicYearStart: view?.academicYearStart ?? new Date().getFullYear(),
      allowedPurposes: view?.allowedPurposes, openYears: view?.examOpenYears,
      // G2 review (L6): the saved year-only choice stays selectable for its exam.
      savedYear: a?.purpose && a.targetExamYear && !a.targetExamSessionId ? { purpose: a.purpose, year: a.targetExamYear } : null };
  });
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
  /** E4 #18/#21: an unused code made earlier (undefined while it is checked). */
  readonly activeCode = signal<ParentLinkCodeStatus | null | undefined>(undefined);
  /** The earlier code when it can still be used (an expired one only gets a note). */
  readonly openCode = computed(() => { const code = this.activeCode(); return code && !code.expired ? code : null; });
  /** E4 #18: the invitations accepted in this step (its answer). */
  readonly acceptedNow = signal<Invitation[]>([]);
  readonly emailPrefs = signal<NotificationPreference[] | null>(null);
  readonly oabtOptions: ComboOption[] = [...Object.entries(OABT_FIELDS).map(([value, label]) => ({ value, label })),
    { value: 'none', label: 'ÖABT\'ye girmeyeceğim (yalnız AGS)' }];
  /** The server accepts a birth date between 100 and 6 years ago (Türkiye date). */
  readonly oldest = shiftYears(-100);
  readonly youngest = shiftYears(-6);
  /** G4 (#43): "Okulum listede yok": the student types the school's name instead of picking it. */
  readonly typingSchool = signal(false);
  readonly typedSchoolMax = TYPED_SCHOOL_MAX;
  draftSchool = '';
  /** Paket H6: "Bu bölüm Sayısal puanla öğrenci alıyor; …" after a programme picked the score type (until the next answer). */
  readonly fieldNote = signal<string | null>(null);
  /** G5 (#45): the result of "Kopyala" for the parent code. */
  readonly copyStatus = signal<string | null>(null);
  private interacted = false;
  draftNumber: number | null = null;
  draftDate = '';
  draftEstimated = false;

  async ngOnInit() { await this.load(); }

  private async load() {
    this.loading.set(true); this.error.set(null);
    try {
      const view = await firstValueFrom(this.api.get());
      // Review of PR 120: Hedefim offers the target step only after the first-login setup; before that it links to it.
      if (this.embedded() && view.status !== 'Completed') { this.notReady.set(true); return; }
      const targetOnly = this.embedded() || this.route.snapshot.queryParamMap.get('step') === 'target';
      if (view.status === 'Completed' && !targetOnly) { await this.router.navigate(['/coaching-portal']); return; }
      if (view.status === 'Completed') this.mode.set('target');
      this.view.set(view);
      // "Hedefini belirle" keeps the saved exam (the target belongs to it); the wizard asks an exam the grade no longer fits again.
      const answers = prefill(view.status === 'Completed' ? { ...view, allowedPurposes: undefined } : view);
      // E7 (#30): a saved target is shown first with "Değiştir". "Hedefini belirle" without a target asks right away; Hedefim shows
      // "Henüz karar vermedim" as it does right after saving it.
      this.answers.set(this.mode() === 'target' && !view.answers.targetLocked && !view.answers.target && !this.embedded() ? change(answers, 'targetSaved') : answers);
      this.original.set(this.mode() === 'target' && !view.answers.targetLocked ? this.answers().targetSaved ?? null : null);
      this.markSaved(view);
      if (this.mode() === 'target') { await this.settle(); return; }
      this.resumeAnswers(view);
      this.locations.getProvinces().subscribe({ next: list => this.provinces.set(list.map(x => ({ value: x.id, label: x.name }))),
        error: () => this.error.set('Şehir listesi yüklenemedi. Sayfayı yenile.') });
      await this.settle();
    } catch { this.error.set('Kurulum bilgilerin yüklenemedi.'); }
    finally { this.loading.set(false); }
  }

  // ---------- Answering ----------

  answer(question: Question, answer: Answer) {
    if (this.busy()) return;
    this.interacted = true;
    this.invalid.set(null); this.fieldNote.set(null);
    this.answers.update(current => ({ ...change(current, question.id), [question.id]: answer }));
    void this.settle();
  }
  async choose(question: Question, choice: Choice) {
    // Review of PR 120: "Henüz karar vermedim" removes a saved target, so it is confirmed first.
    const saved = this.original();
    if (question.id === 'targetMode' && choice.value === 'none' && this.mode() === 'target' && saved && saved.value !== 'none'
      && !await this.toaster.confirm(`Mevcut hedefin silinecek: ${saved.label}. Devam edilsin mi?`, { confirmText: 'Hedefimi sil', cancelText: 'Vazgeç' })) return;
    this.answer(question, { value: choice.value, label: question.id === 'session' ? sessionAnswerLabel(choice.label) : choice.label });
  }
  /**
   * Paket H6: "Bölümüne göre bulalım" answers the score type question with the programme's field, as a tap on that button would
   * (the answers that depend on it are cleared; Dil asks the language next).
   */
  chooseByProgram(question: Question, found: ScoreTypeFound) {
    if (this.busy() || question.id !== 'yksField') return;
    this.answer(question, { value: found.field.value, label: found.field.label });
    this.fieldNote.set(fieldFoundNote(found.field));
  }
  skip(question: Question) { this.answer(question, { value: null, label: question.skipLabel ?? 'Geç' }); }
  pickCombo(question: Question, option: ComboOption) { this.answer(question, { value: option.value, label: option.label, data: option.data }); }

  submitDate(question: Question) {
    // G5 (#44): the message says what is wrong (the server's window: 6 to 100 years old, Türkiye date).
    const problem = birthDateError(this.draftDate);
    if (problem) { this.invalid.set(problem); return; }
    this.answer(question, { value: this.draftDate, label: this.draftDate.split('-').reverse().join('.') });
  }
  submitNumber(question: Question) {
    const value = this.draftNumber; const min = question.min ?? 0; const max = question.max ?? 1000;
    // A score is above 0 (the server's rule); the diploma grade is 50–100.
    if (value === null || !Number.isFinite(value) || value < min || min === 0 && value <= 0 || value > max || Math.round(value * 100) !== value * 100) {
      this.invalid.set(min === 0 ? `0'dan büyük, en çok ${max} olan bir sayı yaz.` : `${min} ile ${max} arasında bir sayı yaz.`); return;
    }
    const estimated = question.kind === 'diploma' && this.draftEstimated;
    this.answer(question, { value, label: `${formatScore(value)}${estimated ? ' (tahmini)' : ''}`, data: question.kind === 'diploma' ? { estimated } : undefined });
  }
  /** G4 (#43): "Okulum listede yok" opens a text box; "Listeden seç" goes back to the list. */
  typeSchool(typing: boolean) {
    this.typingSchool.set(typing); this.invalid.set(null); this.draftSchool = '';
    setTimeout(() => (this.host.nativeElement.querySelector(typing ? '#ob-own-school' : '[data-current] input[role="combobox"]') as HTMLElement | null)?.focus());
  }
  schoolNameInput(event: Event) { this.draftSchool = (event.target as HTMLInputElement).value; }
  submitSchoolName(question: Question) {
    const problem = typedSchoolError(this.draftSchool);
    if (problem) { this.invalid.set(problem); return; }
    this.answer(question, typedSchoolAnswer(this.draftSchool));
  }

  /** "Değiştir": the answer and only the answers that depend on it are asked again. */
  edit(id: QuestionId) {
    if (this.busy()) return;
    this.error.set(null); this.invalid.set(null); this.done.set(null); this.saved$.set(null); this.fieldNote.set(null); this.interacted = true;
    this.answers.update(current => change(current, id));
    void this.settle();
  }
  editStep(step: StepNumber) {
    const first = this.flow().find(x => x.step === step);
    if (first) this.edit(first.id);
  }

  /** E7: "Vazgeç" while changing the target: the saved target stays, nothing is sent. */
  cancelChange() {
    const original = this.original();
    if (this.busy() || !original) return;
    this.error.set(null); this.invalid.set(null);
    this.answers.update(current => {
      const next = { ...current };
      for (const id of TARGET_QUESTIONS) delete next[id];
      return { ...next, targetSaved: original };
    });
    void this.settle();
  }

  /** Steps 4–6 are passed after their own action (accepting an invitation, a parent code, e-mail choices). */
  pass(question: Question, label: string) { this.answer(question, { value: 'passed', label }); }

  async acceptInvitation(invitation: Invitation) {
    await this.run(async () => {
      await firstValueFrom(this.http.post(`${environment.apiUrl}/coaching/invitations/${encodeURIComponent(invitation.id)}/accept`, {}));
      this.invitations.update(list => (list ?? []).filter(x => x.id !== invitation.id));
      this.acceptedNow.update(list => [...list, invitation]);
      // Review of PR 122: a teacher now has the student, so Ödevlerim and Seanslar may appear.
      this.navigation.refresh({ force: true });
    }, 'Davet kabul edilemedi. Yeniden dene.');
  }
  /** E4 #18: the invite step's answer names the invitations accepted here. */
  passInvite(question: Question, pending: number) {
    const accepted = this.acceptedNow();
    this.pass(question, accepted.length
      ? inviteAnswer(accepted.map(x => ({ inviterEmail: x.inviterEmail, type: x.type ?? 'StudentToTeacher', acceptedAt: '' })))
      : pending ? 'Sonra bakarım' : 'Davetim yok');
  }
  /** E4 #18: the parent step's answer names the code's expiry. */
  passParent(question: Question, code: { expiresAt: string } | null) { this.pass(question, parentAnswer(code, [])); }
  codeNote(expiresAt: string) { return parentCodeNote(expiresAt); }
  staleCode(code: { expiresAt: string }) { return staleParentCode(code); }
  expiredNote(expiresAt: string) { return `Veli kodunun süresi doldu (${coachingDate(expiresAt)}). İstersen yeni bir kod oluştur.`; }
  async createParentCode() {
    this.copyStatus.set(null);
    await this.run(async () => this.parentCode.set(await firstValueFrom(this.parents.createCode())), 'Veli kodu oluşturulamadı. Yeniden dene.');
  }
  /** G5 (#45): copies the parent code; without clipboard access the code is selected so the student can copy it. */
  async copyParentCode(code: string, element: HTMLElement) {
    const result: CopyResult = await copyText(code, element);
    // Review L10: the live region is emptied first, so a repeated click is announced again.
    this.copyStatus.set(''); setTimeout(() => this.copyStatus.set(copyMessage(result)), 60);
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
    if (!this.error()) this.pass(question, emailAnswer(prefs));
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
      if (!view) { if (this.invalid()) this.prepare(); return; }
      this.view.set(view);
      // Review M1: a saved grade the chosen exam no longer fits asks the exam again instead of resending it.
      if (this.mode() === 'wizard') this.answers.update(current => withoutUnfitPurpose(current, view.allowedPurposes));
      this.markSaved(view);
      if (this.mode() === 'target' && pending === 3) {
        if (!this.embedded()) { await this.router.navigate(['/coaching-portal']); return; }
        // E7: Hedefim shows the new target as the current one and stays on the page.
        this.answers.set(prefill({ ...view, allowedPurposes: undefined }));
        this.original.set(view.answers.targetLocked ? null : this.answers().targetSaved ?? null);
        this.markSaved(view);
        this.saved$.set('Hedefin kaydedildi.');
        this.targetSaved.emit();
      }
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
      const failure = e as { status?: number; error?: { message?: string; code?: string; field?: string } };
      if (step === 3 && failure?.error?.code === 'Onboarding.TeacherGoal') { await this.showTeacherGoal(); return null; }
      if (failure?.status === 400) {
        // E4 review: a rule only the server knows refused the step: that question is asked again with the message (no retry).
        // F2 (E4 a): the server names the refused question. Only a question of this step or an earlier one is asked again (the
        // wizard can go back); Hedefim asks only the target step. Otherwise (an older API, an unknown name) the step's last question.
        const field = failure.error?.field;
        const steps = this.mode() === 'target' ? [3] : Array.from({ length: step }, (_, i) => i + 1);
        const named = field ? questions(this.answers(), this.context()).find(x => x.id === field && steps.includes(x.step) && x.id in this.answers()) : undefined;
        const last = named ?? stepQuestions(this.answers(), this.context(), step).filter(x => x.id in this.answers()).at(-1);
        if (last) {
          this.answers.update(current => change(current, last.id));
          this.invalid.set(failure.error?.message ?? 'Bu cevap kaydedilemedi. Başka bir seçenek dene.');
          return null;
        }
      }
      this.error.set(failure?.error?.message ?? 'Kaydedilemedi. Bağlantını kontrol edip yeniden dene.');
      return null;
    } finally { this.busy.set(false); }
  }

  /** #35: when the status itself could not be loaded, "Yeniden dene" loads it again (nothing to save yet). */
  retry() { this.error.set(null); void (this.view() ? this.settle() : this.load()); }

  /** Review of PR 120: a teacher set the goal while the student was changing it: the teacher's goal is shown, locked. */
  private async showTeacherGoal() {
    this.invalid.set(null); this.error.set(null); this.original.set(null);
    try {
      const view = await firstValueFrom(this.api.get());
      this.view.set(view);
      this.answers.set(prefill({ ...view, allowedPurposes: undefined }));
      this.markSaved(view);
    } catch {
      this.answers.update(current => {
        const next = { ...current };
        for (const id of TARGET_QUESTIONS) delete next[id];
        return { ...next, targetSaved: { value: 'locked', label: 'Hedefini öğretmenin belirledi', data: { kind: 'none', locked: true } } };
      });
    }
  }

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
    this.draftNumber = null; this.draftEstimated = false; this.draftDate = ''; this.draftSchool = ''; this.typingSchool.set(false);
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
      case 'parent':
        this.activeCode.set(undefined);
        this.parents.getActiveCode().subscribe({ next: code => this.activeCode.set(code), error: () => this.activeCode.set(null) });
        break;
      case 'targetScore': {
        const level = a.ydsLevel?.value;
        if (typeof level === 'string') this.draftNumber = Number(level);
        break;
      }
    }
    // E7: embedded in Hedefim the focus moves only once the student works on the target (not when the page opens).
    if (this.embedded() && !this.interacted) return;
    setTimeout(() => (this.host.nativeElement.querySelector('[data-current] input, [data-current] button') as HTMLElement | null)?.focus());
  }

  /**
   * E4 #18: a resumed wizard shows the real answers of the saved steps 5 and 6 (a parent code and its expiry, the e-mails chosen),
   * read from Identity and Notification; step 4 comes with the view. A failed read keeps the neutral label.
   */
  private resumeAnswers(view: OnboardingView) {
    const relabel = (id: 'invite' | 'parent' | 'notifications', label: string) =>
      this.answers.update(current => current[id] ? { ...current, [id]: { ...current[id]!, label } } : current);
    // "Sonra bakarım" is not stored: invitations still waiting mean the student chose it.
    if (view.savedSteps.includes(4) && !view.acceptedInvitations?.length)
      this.http.get<Invitation[]>(`${environment.apiUrl}/coaching/invitations/my-invitations`)
        .subscribe({ next: list => relabel('invite', inviteAnswer([], list.length)), error: () => undefined });
    if (view.savedSteps.includes(5))
      forkJoin([this.parents.getActiveCode().pipe(catchError(() => of(null))), this.parents.getLinks()])
        .subscribe({ next: ([code, links]) => relabel('parent', parentAnswer(code, links)), error: () => undefined });
    if (view.savedSteps.includes(6))
      this.notifications.list().subscribe({ next: list => relabel('notifications',
        emailAnswer(list.filter(x => ONBOARDING_EMAIL_CATEGORIES.includes(x.category) && x.emailEditable !== false))), error: () => undefined });
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
    this.api.schools(this.answers().city?.label ?? '', null, search, page, this.answers().district?.label ?? null).pipe(map(r => ({ total: r.totalCount,
      items: r.items.map(x => ({ value: x.id, label: x.name, hint: [x.inHomeDistrict ? 'ilçende' : null, x.district, x.type].filter(Boolean).join(' · ') || undefined })) })));
  /** The LGS school step: when the chosen city is the student's own, the schools of their district come first. */
  readonly loadSchools = (search: string, page: number): Observable<ComboPage> => {
    const a = this.answers();
    const home = a.city && a.schoolCity && fold(a.city.label) === fold(a.schoolCity.label) ? a.district?.label ?? null : null;
    return this.api.schools(String(a.schoolCity?.value ?? ''), String(a.schoolType?.value ?? ''), search, page, home).pipe(map(r => ({ total: r.totalCount,
      items: r.items.map(x => ({ value: x.id, label: x.name, data: { minimumScore: x.minimumScore },
        hint: [x.inHomeDistrict ? 'ilçende' : null, x.district, x.minimumScore !== null ? `taban yaklaşık ${formatScore(x.minimumScore)}` : 'taban puanı yok'].filter(Boolean).join(' · ') })) })));
  };
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

/** G5 (#44): the birth-date rule, shared with Profilim (PR 128 review L1). */
export { birthDateError, shiftYears } from '../birth-date';
