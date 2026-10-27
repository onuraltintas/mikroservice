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

/** Roadmap O3: the messages of one conversation, oldest first. Used by participants and by the institution review. */
@Component({
  selector: 'edu-coaching-message-list', standalone: true,
  styles: [`.msg-list{list-style:none;margin:0;display:grid;gap:.6rem;max-height:60vh;overflow-y:auto;padding:.25rem}.msg{justify-self:start;max-width:85%;border-radius:.75rem;background:#f1f5f9;padding:.5rem .75rem}.msg.mine{justify-self:end;background:#e0e7ff}.msg p{margin:.2rem 0;white-space:pre-wrap;overflow-wrap:anywhere}.who,.seen{color:#64748b;font-size:.75rem}.seen{display:block;text-align:right}.file{display:block;border:0;background:none;color:#3730a3;padding:.15rem 0;text-align:left;overflow-wrap:anywhere;font:inherit;cursor:pointer}.empty{color:#64748b;font-size:.9rem;padding:.5rem}@media (max-width:720px){.msg-list{max-height:none}}:host-context(.dark) .msg{background:#1e293b}:host-context(.dark) .msg.mine{background:#312e81}`],
  template: `
    <ol class="msg-list" aria-label="Mesajlar">
      @for (message of messages(); track message.id) {
        <li class="msg" [class.mine]="message.mine">
          <span class="who">{{ message.mine ? 'Sen' : message.senderName }} · <time [attr.datetime]="message.sentAt">{{ time(message.sentAt) }}</time></span>
          @if (message.body) { <p>{{ message.body }}</p> }
          @for (file of message.attachments; track file.id) {
            <button type="button" class="file" (click)="download.emit(file)">📎 {{ file.fileName }} <small>({{ size(file.sizeBytes) }})</small></button>
          }
          @if (message.mine) { <small class="seen">{{ message.readByOther ? 'Okundu' : 'Gönderildi' }}</small> }
        </li>
      } @empty { <li class="empty">Henüz mesaj yok.</li> }
    </ol>`
})
export class CoachingMessageListComponent {
  readonly messages = input.required<CoachingMessage[]>();
  readonly download = output<CoachingMessageFile>();
  readonly time = coachingMessageTime;
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
            } @else if (!loading()) { <p>Yazışabileceğin kimse yok. Mesajlaşma, öğretmen–öğrenci ya da veli bağlantısı olanlar arasındadır.</p> }
          </form>
        }
        @if (loading() && !conversations().length) { <p role="status">Yazışmalar yükleniyor…</p> }
        <ul class="conversations" aria-label="Yazışmalar">
          @for (item of conversations(); track item.id) {
            <li>
              <button type="button" [class.active]="item.id === thread()?.conversation?.id" (click)="open(item.id)" [attr.aria-current]="item.id === thread()?.conversation?.id ? 'true' : null">
                <strong>{{ item.otherName }}</strong>
                <small>{{ roleLine(item) }} · {{ time(item.lastMessageAt) }}</small>
                @if (item.lastMessagePreview) { <span class="preview">{{ item.lastMessagePreview }}</span> }
                @if (item.unreadCount) { <span class="badge" [attr.aria-label]="item.unreadCount + ' okunmamış mesaj'">{{ item.unreadCount }}</span> }
              </button>
            </li>
          } @empty { @if (!loading()) { <li class="empty">Henüz yazışma yok.</li> } }
        </ul>
      </div>

      @if (thread(); as current) {
        <div class="thread">
          <header>
            <button type="button" class="back" (click)="close()">← Yazışmalar</button>
            <h3>{{ current.conversation.otherName }} <small>{{ roleLine(current.conversation) }}</small></h3>
          </header>
          @if (current.lastInstitutionReviewAt) {
            <p class="note">Kurum yönetimi bu yazışmayı {{ time(current.lastInstitutionReviewAt) }} tarihinde inceledi.</p>
          }
          @if (current.hasOlder) { <button type="button" class="link" (click)="older()" [disabled]="working()">Daha eski mesajlar</button> }
          <edu-coaching-message-list [messages]="current.messages" (download)="download($event)" />
          @if (current.conversation.canSend) {
            <form class="composer" (ngSubmit)="send()">
              <label for="coaching-message-text">Mesajın</label>
              <textarea id="coaching-message-text" name="body" rows="3" [(ngModel)]="draft" [maxlength]="maxLength" [disabled]="working()"></textarea>
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
                    <li>{{ file.name }} <button type="button" (click)="removeFile($index)" [attr.aria-label]="file.name + ' dosyasını çıkar'">×</button></li>
                  }
                </ul>
              }
            </form>
          } @else {
            <p class="note">Bağlantı sona erdiği için bu yazışma kapandı. Eski mesajları okuyabilirsin.</p>
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

  key(contact: CoachingMessageContact) { return `${contact.userId}|${contact.studentId}`; }
  contactLabel(contact: CoachingMessageContact) {
    return contact.role === 'Parent' ? `${contact.name} (${contact.studentName} velisi)`
      : contact.role === 'Student' ? `${contact.name} (öğrenci)`
      : this.viewerRole === 'Parent' ? `${contact.name} (${contact.studentName} öğretmeni)` : `${contact.name} (öğretmen)`;
  }
  roleLine(item: CoachingConversation) {
    return item.otherRole === 'Parent' ? `${item.studentName} velisi` : item.otherRole === 'Teacher' && item.kind === 'TeacherParent' ? `Öğretmen · ${item.studentName}` : coachingMessageRoleLabel(item.otherRole);
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
    if (bad) { this.error.set(`${bad.name}: yalnız 10 MB'a kadar fotoğraf, PDF, Word ya da Markdown dosyası eklenebilir.`); return; }
    if (this.files().length + chosen.length > this.maxFiles) { this.error.set(`Bir mesaja en fazla ${this.maxFiles} dosya eklenebilir.`); return; }
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
