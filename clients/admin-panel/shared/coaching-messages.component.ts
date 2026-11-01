import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, Input, OnInit, afterNextRender, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { concatMap, from, lastValueFrom, toArray } from 'rxjs';
import { coachingFeedback } from '../src/app/features/coaching-portal/coaching-feedback';
import {
  COACHING_MESSAGE_FILE_TYPES, COACHING_MESSAGE_MAX_FILE_BYTES, COACHING_MESSAGE_MAX_FILES, COACHING_MESSAGE_MAX_LENGTH,
  CoachingConversation, CoachingConversationThread, CoachingMessage, CoachingMessageContact, CoachingMessageFile, CoachingMessagesApi,
  coachingFileSize, coachingMessageRoleLabel, saveCoachingMessageFile
} from './coaching-messages.api';

const dateTime = new Intl.DateTimeFormat('tr-TR', { timeZone: 'Europe/Istanbul', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
export const coachingMessageTime = (value: string) => dateTime.format(new Date(value));

const dayFormat = new Intl.DateTimeFormat('tr-TR', { timeZone: 'Europe/Istanbul', day: 'numeric', month: 'long', weekday: 'long' });
const dayKey = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' });
const clock = new Intl.DateTimeFormat('tr-TR', { timeZone: 'Europe/Istanbul', hour: '2-digit', minute: '2-digit' });
export type CoachingMessageEntry = { key: string; day: string; message?: undefined } | { key: string; day?: undefined; message: CoachingMessage };
/** T5: messages with a separator before each new Türkiye day ("1 Kasım Pazar"). */
export function coachingMessageEntries(messages: CoachingMessage[]): CoachingMessageEntry[] {
  const entries: CoachingMessageEntry[] = [];
  let last = '';
  for (const message of messages) {
    const date = new Date(message.sentAt);
    const key = dayKey.format(date);
    if (key !== last) { entries.push({ key: `day-${key}`, day: dayFormat.format(date) }); last = key; }
    entries.push({ key: message.id, message });
  }
  return entries;
}

/** Roadmap O3: the messages of one conversation, oldest first. Used by participants and by the institution review. */
@Component({
  selector: 'edu-coaching-message-list', standalone: true,
  styles: [`:host{display:block}
    .msg-list{list-style:none;margin:0;display:grid;gap:6px;max-height:60vh;overflow-y:auto;padding:4px 2px}
    .day{justify-self:center;margin:10px 0 4px;padding:2px 12px;border-radius:999px;background:var(--ui-surface-muted);color:var(--ui-text-muted);font-size:12px;font-weight:650}
    .msg{justify-self:start;max-width:min(85%,560px);border-radius:16px 16px 16px 4px;background:var(--ui-surface-muted);color:var(--ui-text);padding:8px 12px}
    .msg.mine{justify-self:end;border-radius:16px 16px 4px 16px;background:var(--ui-brand-soft)}
    .msg p{margin:2px 0;white-space:pre-wrap;overflow-wrap:anywhere;line-height:1.5}
    .who{display:block;color:var(--ui-text-muted);font-size:12px;font-weight:650}
    .meta{display:block;color:var(--ui-text-muted);font-size:11px;text-align:right}
    .file{display:flex;align-items:center;gap:6px;min-height:36px;border:0;background:none;color:var(--ui-brand-text);padding:2px 0;text-align:left;overflow-wrap:anywhere;font:inherit;font-weight:600;cursor:pointer}
    .file small{color:var(--ui-text-muted);font-weight:400}
    .empty{color:var(--ui-text-muted);font-size:14px;padding:8px}
    @media (max-width:720px){.msg-list{max-height:none}}`],
  template: `
    <ol class="msg-list" aria-label="Mesajlar">
      @for (entry of entries(); track entry.key) {
        @if (entry.message; as message) {
          <li class="msg" [class.mine]="message.mine">
            @if (!message.mine) { <span class="who">{{ message.senderName }}</span> }
            @if (message.body) { <p>{{ message.body }}</p> }
            @for (file of message.attachments; track file.id) {
              <button type="button" class="file" (click)="download.emit(file)">{{ file.fileName }} <small>{{ size(file.sizeBytes) }}</small></button>
            }
            <small class="meta"><time [attr.datetime]="message.sentAt">{{ clock(message.sentAt) }}</time>@if (message.mine) {, {{ message.readByOther ? 'Okundu' : 'Gönderildi' }}}</small>
          </li>
        } @else { <li class="day">{{ entry.day }}</li> }
      } @empty { <li class="empty">Henüz mesaj yok. İlk mesajı sen yaz.</li> }
    </ol>`
})
export class CoachingMessageListComponent {
  readonly messages = input.required<CoachingMessage[]>();
  readonly download = output<CoachingMessageFile>();
  readonly entries = computed(() => coachingMessageEntries(this.messages()));
  readonly clock = (value: string) => clock.format(new Date(value));
  readonly size = coachingFileSize;
}

/**
 * Roadmap O3: inbox and conversation for a teacher, a student or a parent. Writing is possible while the teacher–student
 * (and parent) link is active; an ended link leaves the conversation readable. The selected conversation is an input and
 * an output so the page can keep it in the address.
 */
@Component({
  selector: 'edu-coaching-messages', standalone: true, imports: [FormsModule, CoachingMessageListComponent],
  styleUrl: './coaching-messages.component.scss',
  template: `
    <section class="messages" [class.has-thread]="!!thread()" aria-labelledby="coaching-messages-title">
      <div class="inbox">
        <header>
          <h2 id="coaching-messages-title">Mesajlar</h2>
          @if (unreadMessages()) { <span class="unread-total">{{ unreadMessages() }} okunmamış</span> }
          <button type="button" class="primary" (click)="togglePicker()" [attr.aria-expanded]="picking()">Yeni mesaj</button>
        </header>
        @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
        @if (picking()) {
          <form class="picker" (ngSubmit)="start()">
            @if (contacts().length) {
              <label>Kime yazacaksın?
                <select name="contact" [(ngModel)]="contactKey" required>
                  <option value="">Seç</option>
                  @for (contact of contacts(); track key(contact)) { <option [value]="key(contact)">{{ contactLabel(contact) }}</option> }
                </select>
              </label>
              <button type="submit" class="primary" [disabled]="!contactKey || working()">Yazışmayı aç</button>
            } @else if (!loading()) { <p>Yazabileceğin kimse yok. Mesajlaşma, öğretmen, öğrenci ve veli arasında bağlantı varken açılır.</p> }
          </form>
        }
        @if (loading() && !conversations().length) { <p role="status" class="empty">Yazışmalar yükleniyor…</p> }
        <ul class="conversations" aria-label="Yazışmalar">
          @for (item of conversations(); track item.id) {
            <li>
              <button type="button" class="conversation" [class.active]="item.id === thread()?.conversation?.id" [class.unread]="item.unreadCount > 0" (click)="open(item.id)" [attr.aria-current]="item.id === thread()?.conversation?.id ? 'true' : null">
                <span class="avatar" aria-hidden="true">{{ initial(item.otherName) }}</span>
                <span class="text">
                  <span class="line"><strong>{{ item.otherName }}</strong><time [attr.datetime]="item.lastMessageAt">{{ time(item.lastMessageAt) }}</time></span>
                  <small>{{ roleLine(item) }}</small>
                  @if (item.lastMessagePreview) { <span class="preview">{{ item.lastMessagePreview }}</span> }
                </span>
                @if (item.unreadCount) { <span class="badge" [attr.aria-label]="item.unreadCount + ' okunmamış mesaj'">{{ item.unreadCount }}</span> }
              </button>
            </li>
          } @empty { @if (!loading()) { <li class="empty">Henüz yazışman yok. Yeni mesajla başla.</li> } }
        </ul>
      </div>

      @if (thread(); as current) {
        <div class="thread">
          <header>
            <button type="button" class="back" (click)="close()" aria-label="Yazışmalara dön"></button>
            <span class="avatar" aria-hidden="true">{{ initial(current.conversation.otherName) }}</span>
            <h3>{{ current.conversation.otherName }} <small>{{ roleLine(current.conversation) }}</small></h3>
          </header>
          @if (current.lastInstitutionReviewAt) {
            <p class="note">Kurum yönetimi bu yazışmayı {{ time(current.lastInstitutionReviewAt) }} tarihinde inceledi.</p>
          }
          @if (current.hasOlder) { <button type="button" class="link" (click)="older()" [disabled]="working()">Daha eski mesajlar</button> }
          <edu-coaching-message-list [messages]="current.messages" (download)="download($event)" />
          @if (current.conversation.canSend) {
            <form class="composer" (ngSubmit)="send()">
              <label for="coaching-message-text" class="sr-only">Mesajın</label>
              <textarea id="coaching-message-text" name="body" rows="2" placeholder="Mesajını yaz" [(ngModel)]="draft" [maxlength]="maxLength" [disabled]="working()"></textarea>
              <div class="row">
                <label class="attach">Dosya ekle
                  <input type="file" multiple [accept]="fileTypes" (change)="addFiles($event)" [disabled]="working() || files().length >= maxFiles">
                </label>
                <small>{{ draft.length }}/{{ maxLength }}</small>
                <button type="submit" class="primary" [disabled]="!canSend()">{{ working() ? 'Gönderiliyor…' : 'Gönder' }}</button>
              </div>
              @if (files().length) {
                <ul class="chosen" aria-label="Eklenecek dosyalar">
                  @for (file of files(); track $index) {
                    <li>{{ file.name }} <button type="button" class="remove" (click)="removeFile($index)" [attr.aria-label]="file.name + ' dosyasını çıkar'">×</button></li>
                  }
                </ul>
              }
            </form>
          } @else {
            <p class="note">Bu yazışma kapandı, çünkü aranızdaki bağlantı sona erdi. Eski mesajları okuyabilirsin.</p>
          }
        </div>
      }
    </section>`
})
export class CoachingMessagesComponent implements OnInit {
  @Input({ required: true }) apiUrl = '';
  /** Who is reading: shapes how contacts are named. */
  @Input() viewerRole: 'Teacher' | 'Student' | 'Parent' = 'Student';
  /** Milliseconds between refreshes while the page is visible; 0 turns refreshing off. */
  @Input() pollMs = 30_000;
  @Input() set selectedId(value: string | null | undefined) {
    this.requested = value ?? null;
    if (this.ready && this.requested !== (this.thread()?.conversation.id ?? null)) this.requested ? void this.open(this.requested, false) : this.thread.set(null);
  }
  readonly selectedIdChange = output<string | null>();
  readonly unreadChange = output<number>();

  readonly maxLength = COACHING_MESSAGE_MAX_LENGTH;
  readonly maxFiles = COACHING_MESSAGE_MAX_FILES;
  readonly fileTypes = COACHING_MESSAGE_FILE_TYPES;
  readonly time = coachingMessageTime;
  readonly contacts = signal<CoachingMessageContact[]>([]);
  readonly conversations = signal<CoachingConversation[]>([]);
  readonly thread = signal<CoachingConversationThread | null>(null);
  readonly files = signal<File[]>([]);
  readonly loading = signal(false);
  readonly working = signal(false);
  readonly picking = signal(false);
  readonly error = signal<string | null>(null);
  readonly unread = computed(() => this.conversations().filter(x => x.unreadCount > 0).length);
  /** T5: the number of unread messages across all conversations, shown next to the inbox title. */
  readonly unreadMessages = computed(() => this.conversations().reduce((sum, x) => sum + (x.unreadCount || 0), 0));
  draft = '';
  contactKey = '';

  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private api!: CoachingMessagesApi;
  private requested: string | null = null;
  private ready = false;

  constructor() {
    coachingFeedback(undefined, this.error);
    afterNextRender(() => {
      if (this.pollMs <= 0) return;
      const timer = setInterval(() => { if (document.visibilityState === 'visible' && !this.working()) void this.refresh(); }, this.pollMs);
      this.destroy.onDestroy(() => clearInterval(timer));
    });
  }

  async ngOnInit(): Promise<void> {
    this.api = new CoachingMessagesApi(this.http, this.apiUrl);
    this.loading.set(true);
    try {
      await this.loadConversations();
      if (this.requested) await this.open(this.requested, false);
    } finally { this.loading.set(false); this.ready = true; }
  }

  initial(name: string) { return (name.trim()[0] ?? '?').toLocaleUpperCase('tr-TR'); }
  key(contact: CoachingMessageContact) { return `${contact.userId}|${contact.studentId}`; }
  contactLabel(contact: CoachingMessageContact) {
    return contact.role === 'Parent' ? `${contact.name} (${contact.studentName} velisi)`
      : contact.role === 'Student' ? `${contact.name} (öğrenci)`
      : this.viewerRole === 'Parent' ? `${contact.name} (${contact.studentName} öğretmeni)` : `${contact.name} (öğretmen)`;
  }
  roleLine(item: CoachingConversation) {
    return item.otherRole === 'Parent' ? `${item.studentName} velisi` : item.otherRole === 'Teacher' && item.kind === 'TeacherParent' ? `${item.studentName} öğretmeni` : coachingMessageRoleLabel(item.otherRole);
  }
  canSend() { return !this.working() && (this.draft.trim().length > 0 || this.files().length > 0) && this.draft.length <= this.maxLength; }

  async togglePicker() {
    this.picking.update(value => !value);
    if (this.picking() && !this.contacts().length) await this.run(async () => this.contacts.set(await lastValueFrom(this.api.contacts())));
  }

  async start() {
    const contact = this.contacts().find(x => this.key(x) === this.contactKey);
    if (!contact) return;
    await this.run(async () => {
      const conversation = await lastValueFrom(this.api.start(contact.userId, contact.studentId));
      this.picking.set(false); this.contactKey = '';
      if (!this.conversations().some(x => x.id === conversation.id)) this.conversations.update(list => [conversation, ...list]);
      await this.open(conversation.id);
    });
  }

  async open(id: string, announce = true) {
    await this.run(async () => {
      const thread = await lastValueFrom(this.api.thread(id));
      this.thread.set(thread); this.files.set([]); this.draft = '';
      this.conversations.update(list => list.map(x => x.id === id ? { ...thread.conversation, unreadCount: 0 } : x));
      this.unreadChange.emit(this.unread());
      if (announce) this.selectedIdChange.emit(id);
    });
  }

  close() { this.thread.set(null); this.selectedIdChange.emit(null); }

  async older() {
    const current = this.thread(); const first = current?.messages[0];
    if (!current || !first) return;
    await this.run(async () => {
      const page = await lastValueFrom(this.api.thread(current.conversation.id, first.sentAt));
      this.thread.set({ ...current, messages: [...page.messages, ...current.messages], hasOlder: page.hasOlder });
    });
  }

  addFiles(event: Event) {
    const input = event.target as HTMLInputElement;
    const chosen = [...(input.files ?? [])]; input.value = '';
    const allowed = this.fileTypes.split(',');
    const bad = chosen.find(file => file.size > COACHING_MESSAGE_MAX_FILE_BYTES || !allowed.includes('.' + (file.name.split('.').pop() ?? '').toLowerCase()));
    if (bad) { this.error.set(`${bad.name} eklenemedi. En fazla 10 MB fotoğraf, PDF, Word ya da Markdown dosyası ekleyebilirsin.`); return; }
    if (this.files().length + chosen.length > this.maxFiles) { this.error.set(`Bir mesaja en fazla ${this.maxFiles} dosya ekleyebilirsin.`); return; }
    this.error.set(null); this.files.update(list => [...list, ...chosen]);
  }

  removeFile(index: number) { this.files.update(list => list.filter((_, i) => i !== index)); }

  async send() {
    const current = this.thread();
    if (!current || !this.canSend()) return;
    await this.run(async () => {
      const id = current.conversation.id;
      const uploaded = this.files().length ? await lastValueFrom(from(this.files()).pipe(concatMap(file => this.api.upload(id, file)), toArray())) : [];
      const message = await lastValueFrom(this.api.send(id, this.draft.trim() || null, uploaded.map(x => x.id)));
      this.thread.set({ ...current, messages: [...current.messages, message] });
      this.conversations.update(list => [{ ...current.conversation, lastMessageAt: message.sentAt, unreadCount: 0,
        lastMessagePreview: message.body || 'Dosya gönderildi' }, ...list.filter(x => x.id !== id)]);
      this.draft = ''; this.files.set([]);
    });
  }

  async download(file: CoachingMessageFile) {
    const id = this.thread()?.conversation.id;
    if (id) await this.run(async () => saveCoachingMessageFile(await lastValueFrom(this.api.download(id, file.id)), file.fileName));
  }

  /** New messages arrive by refreshing the list and the open conversation. */
  async refresh() {
    try {
      await this.loadConversations();
      const current = this.thread();
      if (!current) return;
      const latest = await lastValueFrom(this.api.thread(current.conversation.id));
      const known = new Map(current.messages.map(x => [x.id, x]));
      const merged = [...current.messages.map(x => latest.messages.find(y => y.id === x.id) ?? x), ...latest.messages.filter(x => !known.has(x.id))];
      this.thread.set({ ...latest, messages: merged, hasOlder: current.hasOlder || latest.hasOlder && merged.length === latest.messages.length });
      this.conversations.update(list => list.map(x => x.id === latest.conversation.id ? { ...latest.conversation, unreadCount: 0 } : x));
    } catch { /* the next refresh tries again */ }
  }

  private async loadConversations() {
    const page = await lastValueFrom(this.api.conversations());
    this.conversations.set(page.items);
    this.unreadChange.emit(this.unread());
  }

  private async run(action: () => Promise<void>) {
    this.working.set(true); this.error.set(null);
    try { await action(); }
    catch (error) { this.error.set(messageOf(error)); }
    finally { this.working.set(false); }
  }
}

export function messageOf(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 404) return 'Yazışma bulunamadı.';
    if (error.status === 403) return 'Bu yazışma için yetkin yok.';
    if (error.status === 413) return 'Dosya çok büyük (en fazla 10 MB).';
    if (error.status === 429) return 'Çok hızlı gönderiyorsun; biraz bekleyip yeniden dene.';
    const body = error.error as { message?: string; detail?: string; title?: string } | null;
    if (body && typeof body === 'object' && (body.message || body.detail)) return (body.message || body.detail)!;
  }
  return 'İşlem tamamlanamadı. Bağlantını kontrol edip yeniden dene.';
}
