import { Component, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CourseManagementService, CourseWorkspaceSubscription } from '../../course-management.service';
import { CoursePanelApi, PanelEmailPreview, PanelEmailSettings, PanelSentEmail, PanelSupportView } from '../panel-api.service';
import { CoursePanelState, formatDate, formatDateTime, problemText, readHashQuery, writeHashQuery } from '../panel-state';

type Tab = 'emails' | 'branches' | 'support' | 'plan' | 'profile';
const KINDS: Record<string, { title: string; when: string }> = {
  Absence: { title: 'Devamsızlık', when: 'Öğrenci "gelmedi" işaretlenince, günde bir e-posta' },
  HomeworkNotDone: { title: 'Yapılmayan ödev', when: 'Öğretmen "yapmadı" ya da "eksik" işaretleyince' },
  InstallmentReminder: { title: 'Taksit hatırlatma', when: 'Vadeden önce ve isterseniz vade günü' },
  InstallmentOverdue: { title: 'Geciken taksit', when: 'Vadeden sonra, en çok iki kez' },
  LessonChanged: { title: 'Ders iptali ya da saat değişikliği', when: 'Değişiklikten 10 dakika sonra' },
  WeeklySchedule: { title: 'Haftalık program', when: 'Pazar 18.00, gelecek haftanın dersleri' },
  PaymentReceived: { title: 'Ödeme alındı', when: 'Ödeme kaydedilince (makbuz yerine geçmez)' }
};

/** Ayarlar: e-mails (with consent, preview, test and the sending record), branches and rooms, support access, plan, profile. */
@Component({
  selector: 'course-settings-section',
  standalone: true,
  imports: [FormsModule],
  styleUrl: '../panel.scss',
  template: `
    <header class="page-head"><div><h1>Ayarlar</h1><p>{{ state.workspace()?.name }}</p></div></header>
    <div class="tabs" role="tablist" aria-label="Ayarlar" style="margin-bottom: 18px">
      @for (item of tabs; track item.id) {
        <button type="button" role="tab" [attr.aria-selected]="tab() === item.id" (click)="open(item.id)">{{ item.label }}</button>
      }
    </div>
    @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
    @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }

    @switch (tab()) {
      @case ('emails') {
        @if (email(); as settings) {
          <div class="stack">
            <section class="card card-pad">
              <label class="check" style="align-items: flex-start"><input type="checkbox" [(ngModel)]="consent" [disabled]="!!settings.informedConsentAt || state.readOnly()">
                <span><strong>Öğrencileri ve velileri bilgilendirdik.</strong> Kişisel verilerinin bu e-postalar için kullanılacağını onlara bildirdiğinizi onaylarsınız.
                  @if (settings.informedConsentAt) { <span class="muted">{{ date(settings.informedConsentAt) }} tarihinde onaylandı.</span> }</span></label>
            </section>
            <div class="row">
              <section class="card grow" aria-labelledby="email-kinds-title">
                <div class="card-head"><h2 id="email-kinds-title">Bildirimler</h2><span class="muted">Başta hepsi kapalı</span></div>
                @for (rule of settings.rules; track rule.kind) {
                  <div class="rule">
                    <div class="rule-text"><strong>{{ kinds[rule.kind].title }}</strong><span class="muted">{{ kinds[rule.kind].when }}</span></div>
                    <label class="field">Kime<select [(ngModel)]="rule.audience" [disabled]="state.readOnly()">
                      <option [ngValue]="1">Veliye</option><option [ngValue]="2">Öğrenciye</option><option [ngValue]="3">Öğrenciye ve veliye</option></select></label>
                    <button type="button" role="switch" class="switch" [attr.aria-checked]="rule.enabled" [attr.aria-label]="kinds[rule.kind].title"
                      (click)="rule.enabled = !rule.enabled" [disabled]="state.readOnly()"><span></span></button>
                    @if (rule.enabled) {
                      <label class="field note">Kurumun notu<input [(ngModel)]="rule.note" maxlength="300" placeholder="İsteğe bağlı, e-postanın sonuna eklenir"></label>
                      <div class="actions note">
                        <button type="button" class="btn-quiet" (click)="showPreview(rule.kind)">Önizleme</button>
                        <button type="button" class="btn-quiet" (click)="sendTest(rule.kindValue)" [disabled]="!testEmail.includes('@')">Kendime deneme gönder</button>
                      </div>
                    }
                  </div>
                }
              </section>
              <div class="side stack">
                <section class="card card-pad stack" style="gap: 14px">
                  <h2>Zamanlama ve yanıt adresi</h2>
                  <label class="field">Taksitten kaç gün önce<select [(ngModel)]="settings.installmentDaysBefore">@for (d of [1,2,3,4,5,6,7]; track d) { <option [ngValue]="d">{{ d }} gün</option> }</select></label>
                  <label class="check"><input type="checkbox" [(ngModel)]="settings.installmentOnDueDay">Vade günü de hatırlat</label>
                  <label class="field">Gecikmeden kaç gün sonra<select [(ngModel)]="settings.overdueDaysAfter">@for (d of [1,3,5,7,10,15]; track d) { <option [ngValue]="d">{{ d }} gün</option> }</select></label>
                  <label class="field">Gönderim saati<select [(ngModel)]="settings.sendHour">@for (h of hours; track h) { <option [ngValue]="h">{{ h }}.00</option> }</select></label>
                  <label class="field">Yanıtlar hangi adrese gelsin<input type="email" [(ngModel)]="settings.replyToEmail" maxlength="320" placeholder="bilgi@kurumunuz.com"></label>
                  <label class="field">Deneme e-postası adresi<input type="email" [(ngModel)]="testEmail" maxlength="320"></label>
                  @if (!state.readOnly()) { <div><button type="button" class="btn" (click)="saveEmails(settings)" [disabled]="busy()">Kaydet</button></div> }
                </section>
                @if (preview(); as mail) {
                  <section class="card card-pad stack" style="gap: 8px" aria-label="Önizleme">
                    <span class="muted">Gönderen: {{ mail.institutionName }}</span>
                    <strong>{{ mail.title }}</strong>
                    @for (line of mail.paragraphs; track $index) { <span>{{ line }}</span> }
                    @if (mail.note) { <span class="banner">{{ mail.note }}</span> }
                    <span class="muted" style="font-size: 13px">Bu e-postaları almak istemiyorsanız buradan çıkabilirsiniz.</span>
                  </section>
                }
              </div>
            </div>
            <section class="card" aria-labelledby="email-log-title">
              <div class="card-head"><h2 id="email-log-title">Gönderim kaydı</h2></div>
              @if (log().length) {
                <div class="table-wrap"><table>
                  <thead><tr><th scope="col">Zaman</th><th scope="col">Bildirim</th><th scope="col">Öğrenci</th><th scope="col">Kime</th><th scope="col">Durum</th></tr></thead>
                  <tbody>@for (row of log(); track row.id) {
                    <tr><td class="num">{{ row.processedAt ? dateTime(row.processedAt) : '' }}</td><td>{{ kinds[row.kind]?.title ?? row.kind }}</td>
                      <th scope="row">{{ row.studentName }}</th><td>{{ row.recipients ?? '' }}</td>
                      <td>@if (row.status !== 'Sent') { <span class="badge warn">Gönderilmedi: {{ row.skipReason }}</span> }
                        @else if (row.delivery === 'Failed') { <span class="badge warn">Teslim edilemedi</span> }
                        @else if (row.delivery === 'PartlyFailed') { <span class="badge warn">{{ row.failedRecipientCount }} adrese teslim edilemedi</span> }
                        @else if (row.delivery === 'Delivered') { <span class="badge ok">Teslim edildi</span> }
                        @else { <span class="badge ok">Gönderildi</span> }</td></tr>
                  }</tbody>
                </table></div>
                <nav class="pager" aria-label="Gönderim kaydı sayfaları"><span class="num">{{ logSkip + 1 }}–{{ logSkip + log().length }}</span>
                  <div class="actions"><button type="button" class="btn-secondary btn-small" (click)="loadLog(logSkip - 25)" [disabled]="logSkip === 0">Önceki</button>
                    <button type="button" class="btn-secondary btn-small" (click)="loadLog(logSkip + 25)" [disabled]="log().length < 25">Sonraki</button></div></nav>
              } @else { <p class="empty">Henüz e-posta gönderilmedi.</p> }
            </section>
          </div>
        }
      }
      @case ('branches') {
        <div class="row">
          <section class="card grow"><div class="card-head"><h2>Şubeler ve derslikler</h2></div>
            @for (branch of state.branches(); track branch.id) {
              <div class="rule"><div class="rule-text"><strong>{{ branch.name }}</strong>
                <span class="muted">{{ roomsLine(branch.id) }}</span></div></div>
            } @empty { <p class="empty">Şube yok. Tek yerde ders veriyorsanız "Merkez" adıyla bir şube açın.</p> }
          </section>
          @if (!state.readOnly()) {
            <div class="side stack">
              <form class="card card-pad stack" style="gap: 12px" (ngSubmit)="addBranch()"><h2>Şube ekle</h2>
                <label class="field">Şube adı<input name="branch" [(ngModel)]="branchName" maxlength="200"></label>
                <div><button type="submit" class="btn-secondary" [disabled]="busy() || !branchName.trim()">Ekle</button></div></form>
              <form class="card card-pad stack" style="gap: 12px" (ngSubmit)="addRoom()"><h2>Derslik ekle</h2>
                <label class="field">Şube<select name="roomBranch" [(ngModel)]="roomBranchId"><option value="">Şube seçin</option>
                  @for (branch of state.branches(); track branch.id) { <option [value]="branch.id">{{ branch.name }}</option> }</select></label>
                <label class="field">Derslik adı<input name="room" [(ngModel)]="roomName" maxlength="200"></label>
                <label class="field">Kapasite<input name="capacity" type="number" min="1" max="500" [(ngModel)]="roomCapacity"><span class="hint">Boş bırakılırsa sınır yok.</span></label>
                <div><button type="submit" class="btn-secondary" [disabled]="busy() || !roomBranchId || !roomName.trim()">Ekle</button></div></form>
            </div>
          }
        </div>
      }
      @case ('support') {
        <section class="card card-pad stack" style="gap: 14px">
          <h2>Destek kaydı</h2>
          <p class="muted" style="margin: 0">eduivme destek ekibi, bir sorunu çözmek için kayıtlarınıza bakabilir ve küçük düzeltmeler yapabilir: öğrenci ve veli iletişim bilgisi, yoklama, tek bir dersin saati, bir e-posta bildirimini kapatma. Para kayıtlarına dokunmaz; öğrenci ve velilere bildirim gitmez. Yapılan her bakış ve değişiklik burada listelenir.</p>
          @if (support()?.logs?.length) {
            <ul>@for (log of support()!.logs; track $index) {
              <li>{{ dateTime(log.occurredAt) }}: @if (log.isChange) { <strong>{{ log.what }}</strong>@if (log.target) {, {{ log.target }}}@if (log.after) {: {{ change(log.before, log.after) }}} } @else { {{ log.what }} görüntülendi }</li>
            }</ul>
          } @else { <p class="empty">Destek ekibi kayıtlarınıza bakmadı.</p> }
        </section>
      }
      @case ('plan') {
        <section class="card card-pad stack" style="gap: 10px">
          <h2>Abonelik</h2>
          @if (plan(); as current) {
            @if (current.subscription; as sub) {
              <p style="margin: 0"><strong>{{ sub.planName }}</strong>, {{ date(sub.startsAt) }} – {{ date(sub.endsAt) }}</p>
              <p style="margin: 0">Etkin öğrenci: <span class="num">{{ current.activeStudentCount }} / {{ sub.activeStudentLimit }}</span></p>
            } @else { <p style="margin: 0">Etkin öğrenci: {{ current.activeStudentCount }}. Planınız eduivme aboneliğinizden okunur.</p> }
          } @else { <p class="muted" style="margin: 0">Plan bilgisi yok.</p> }
        </section>
      }
      @case ('profile') {
        <form class="card card-pad stack" style="gap: 14px; max-width: 520px" (ngSubmit)="saveProfile()">
          <h2>Profilim</h2>
          <label class="field">Görünen adınız<input name="displayName" [(ngModel)]="displayName" maxlength="200"><span class="hint">Takvimde ve listelerde bu ad görünür.</span></label>
          <div><button type="submit" class="btn" [disabled]="busy() || !displayName.trim()">Kaydet</button></div>
        </form>
      }
    }
  `,
  styles: `
    .rule { display: grid; grid-template-columns: minmax(0, 1fr) 200px auto; gap: 12px 16px; align-items: center; padding: 14px 20px; border-top: 1px solid var(--ds-line); }
    .rule-text { display: flex; flex-direction: column; gap: 2px; } .rule-text .muted { font-size: 14px; }
    .rule .note { grid-column: 1 / -1; }
    .switch { width: 52px; height: 32px; border-radius: 999px; border: 1px solid var(--ds-field); background: var(--ds-surface); position: relative; cursor: pointer; padding: 0; }
    .switch span { position: absolute; top: 3px; left: 3px; width: 24px; height: 24px; border-radius: 999px; background: var(--ds-field); transition: left .15s; }
    .switch[aria-checked='true'] { background: var(--ds-primary); border-color: var(--ds-primary); }
    .switch[aria-checked='true'] span { left: 23px; background: var(--ds-accent); }
    .days { border: 0; padding: 0; margin: 0; display: flex; flex-wrap: wrap; gap: 4px 16px; }
    @media (max-width: 640px) { .rule { grid-template-columns: minmax(0, 1fr) auto; } .rule .field:not(.note) { grid-column: 1 / -1; } }
  `
})
export class SettingsSectionComponent {
  readonly state = inject(CoursePanelState);
  private readonly api = inject(CoursePanelApi);
  private readonly service = inject(CourseManagementService);
  readonly kinds = KINDS;
  readonly hours = [7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21];
  readonly tabs: { id: Tab; label: string; slug: string }[] = [
    { id: 'emails', label: 'E-posta bildirimleri', slug: 'eposta' }, { id: 'branches', label: 'Şube ve derslik', slug: 'subeler' },
    { id: 'support', label: 'Destek kaydı', slug: 'destek' }, { id: 'plan', label: 'Abonelik', slug: 'abonelik' }, { id: 'profile', label: 'Profilim', slug: 'profil' }];
  readonly tab = signal<Tab>('emails');
  readonly email = signal<PanelEmailSettings | null>(null);
  readonly preview = signal<PanelEmailPreview | null>(null);
  readonly log = signal<PanelSentEmail[]>([]);
  readonly support = signal<PanelSupportView | null>(null);
  readonly plan = signal<CourseWorkspaceSubscription | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly date = formatDate;
  readonly dateTime = formatDateTime;
  consent = false; testEmail = ''; logSkip = 0;
  branchName = ''; roomBranchId = ''; roomName = ''; roomCapacity: number | null = null;
  displayName = '';

  constructor() {
    const wanted = this.tabs.find(item => item.slug === readHashQuery().get('bolum'));
    if (wanted) this.tab.set(wanted.id);
    effect(() => { this.state.id(); this.state.version(); untracked(() => void this.load()); });
  }

  open(tab: Tab): void { this.tab.set(tab); this.notice.set(''); this.error.set(''); void this.load(); }

  async load(): Promise<void> {
    const id = this.state.id();
    if (!id) return;
    writeHashQuery('ayarlar', { bolum: this.tabs.find(item => item.id === this.tab())?.slug });
    try {
      switch (this.tab()) {
        case 'emails': {
          const settings = await this.api.emailSettings(id);
          this.consent = !!settings.informedConsentAt;
          this.email.set(settings);
          await this.loadLog(0);
          break;
        }
        case 'support': this.support.set(await this.api.support(id)); break;
        case 'plan': this.plan.set((await this.service.getMySubscriptions()).find(item => item.workspaceId === id) ?? null); break;
        case 'profile': this.displayName = (await this.service.getProfile()).displayName; break;
      }
    } catch (error) { this.error.set(problemText(error, 'Ayarlar yüklenemedi.')); }
  }

  async loadLog(skip: number): Promise<void> {
    this.logSkip = Math.max(0, skip);
    try { this.log.set(await this.api.sentEmails(this.state.id(), this.logSkip, 25)); } catch { this.log.set([]); }
  }

  private async act(work: () => Promise<void>, done: string): Promise<void> {
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try { await work(); this.notice.set(done); }
    catch (error) { this.error.set(problemText(error)); }
    finally { this.busy.set(false); }
  }

  saveEmails(settings: PanelEmailSettings): Promise<void> {
    if (settings.rules.some(rule => rule.enabled) && !this.consent) {
      this.error.set('E-postaları açmadan önce bilgilendirme onayını işaretleyin.');
      return Promise.resolve();
    }
    return this.act(async () => {
      this.email.set(await this.api.saveEmailSettings(this.state.id(), {
        rules: settings.rules.map(rule => ({ kind: rule.kindValue, enabled: rule.enabled, audience: rule.audience, note: rule.note?.trim() || null })),
        installmentDaysBefore: settings.installmentDaysBefore, installmentOnDueDay: settings.installmentOnDueDay,
        overdueDaysAfter: settings.overdueDaysAfter, sendHour: settings.sendHour, replyToEmail: settings.replyToEmail?.trim() || null,
        informedConsentConfirmed: this.consent }));
    }, 'E-posta ayarları kaydedildi.');
  }

  async showPreview(kind: string): Promise<void> {
    try { this.preview.set(await this.api.emailPreview(this.state.id(), kind)); } catch (error) { this.error.set(problemText(error)); }
  }

  sendTest(kind: number): Promise<void> {
    return this.act(() => this.api.sendTestEmail(this.state.id(), kind, this.testEmail.trim()).then(() => undefined),
      `Deneme e-postası ${this.testEmail.trim()} adresine gönderildi. Birkaç dakika içinde gelir.`);
  }

  roomsLine(branchId: string): string {
    const rooms = this.state.rooms().filter(room => room.branchId === branchId);
    return rooms.length ? rooms.map(room => room.capacity ? `${room.name} (${room.capacity} kişi)` : room.name).join(', ') : 'Derslik yok';
  }
  addBranch(): Promise<void> {
    return this.act(async () => { await this.service.createBranch(this.state.id(), this.branchName.trim()); this.branchName = ''; await this.state.refreshLists(); }, 'Şube eklendi.');
  }
  addRoom(): Promise<void> {
    return this.act(async () => {
      await this.service.createRoom(this.state.id(), this.roomBranchId, this.roomName.trim(), this.roomCapacity ? Number(this.roomCapacity) : null);
      this.roomName = ''; this.roomCapacity = null; await this.state.refreshLists();
    }, 'Derslik eklendi.');
  }

  change(before: string | null, after: string): string {
    const labels: Record<string, string> = { Present: 'geldi', Absent: 'gelmedi', Late: 'geç geldi', Excused: 'izinli', Yok: 'yoklama yok', Kapalı: 'kapalı',
      Absence: 'devamsızlık', HomeworkNotDone: 'yapılmayan ödev', InstallmentReminder: 'taksit hatırlatma', InstallmentOverdue: 'geciken taksit',
      LessonChanged: 'ders değişikliği', WeeklySchedule: 'haftalık program', PaymentReceived: 'ödeme alındı' };
    const name = (value: string) => labels[value] ?? value;
    return before ? `${name(before)} → ${name(after)}` : name(after);
  }
  saveProfile(): Promise<void> {
    return this.act(async () => { await this.service.saveProfile(this.displayName.trim()); await this.state.refreshLists(); }, 'Adınız kaydedildi.');
  }
}
