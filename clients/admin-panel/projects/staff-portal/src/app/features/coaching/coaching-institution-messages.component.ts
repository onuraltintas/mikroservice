import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { lastValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CoachingConversationThread, CoachingInstitutionConversation, CoachingInstitutionConversationPage, CoachingInstitutionMessagesApi,
  CoachingMessageFile, saveCoachingMessageFile
} from '../../../../../../shared/coaching-messages.api';
import { CoachingMessageListComponent, coachingMessageTime, messageOf } from '../../../../../../shared/coaching-messages.component';

/**
 * Roadmap O3: the institution sees which of its teachers' conversations exist (without their text) and can read one only
 * after recording why. Both participants see that the institution reviewed the conversation.
 */
@Component({
  selector: 'staff-coaching-institution-messages', standalone: true, imports: [FormsModule, CoachingMessageListComponent],
  template: `
    <section class="institution-messages" aria-labelledby="institution-messages-title">
      <h3 id="institution-messages-title">Yazışmalar</h3>
      <p>Öğretmenlerinizin öğrenci ve velilerle yazışmalarını burada görürsünüz. Mesajları okumak için bir gerekçe yazmanız gerekir; gerekçe kaydedilir ve iki taraf da yazışmanın incelendiğini görür.</p>
      @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
      @if (review(); as current) {
        <button type="button" class="link" (click)="review.set(null)">← Yazışma listesi</button>
        <h4>{{ selected()?.teacherName }} ile {{ selected()?.counterpartName }}</h4>
        <edu-coaching-message-list [messages]="current.messages" (download)="download($event)" />
      } @else if (selected(); as item) {
        <form class="reason" (ngSubmit)="open()">
          <p><strong>{{ item.teacherName }}</strong> ile <strong>{{ item.counterpartName }}</strong> ({{ item.kind === 'TeacherParent' ? item.studentName + ' velisi' : 'öğrenci' }})</p>
          <label for="review-reason">İnceleme gerekçesi</label>
          <textarea id="review-reason" name="reason" rows="3" [(ngModel)]="reason" minlength="10" maxlength="500" required></textarea>
          <div class="actions">
            <button type="button" (click)="selected.set(null)">Vazgeç</button>
            <button type="submit" class="primary" [disabled]="reason.trim().length < 10 || working()">Gerekçeyi kaydet ve oku</button>
          </div>
        </form>
      } @else {
        @if (loading()) { <p role="status">Yazışmalar yükleniyor…</p> }
        <table>
          <thead><tr><th scope="col">Öğretmen</th><th scope="col">Yazıştığı kişi</th><th scope="col">Öğrenci</th><th scope="col">Son mesaj</th><th scope="col">Mesaj</th><th scope="col">Son inceleme</th><th scope="col"><span class="sr-only">İşlem</span></th></tr></thead>
          <tbody>
            @for (item of page().items; track item.id) {
              <tr>
                <td>{{ item.teacherName }}</td>
                <td>{{ item.counterpartName }} <small>({{ item.kind === 'TeacherParent' ? 'veli' : 'öğrenci' }})</small></td>
                <td>{{ item.studentName }}</td>
                <td>{{ time(item.lastMessageAt) }}</td>
                <td>{{ item.messageCount }}</td>
                <td>{{ item.lastReviewedAt ? time(item.lastReviewedAt) : '—' }}</td>
                <td><button type="button" (click)="choose(item)">İncele</button></td>
              </tr>
            } @empty { @if (!loading()) { <tr><td colspan="7">Henüz yazışma yok.</td></tr> } }
          </tbody>
        </table>
        @if (page().totalCount > page().pageSize) {
          <div class="actions">
            <button type="button" [disabled]="page().pageNumber <= 1" (click)="load(page().pageNumber - 1)">Önceki</button>
            <button type="button" [disabled]="page().pageNumber * page().pageSize >= page().totalCount" (click)="load(page().pageNumber + 1)">Sonraki</button>
          </div>
        }
      }
    </section>`,
  styles: [`.institution-messages{display:grid;gap:.75rem}.error{color:#b91c1c}.reason{display:grid;gap:.5rem;max-width:40rem}textarea{font:inherit;padding:.5rem;border:1px solid #cbd5e1;border-radius:.5rem}.actions{display:flex;gap:.5rem;flex-wrap:wrap}.primary{background:#4f46e5;color:#fff;border:0;border-radius:.5rem;padding:.45rem .8rem;font-weight:600}.link{border:0;background:none;color:#4f46e5;font-weight:600;justify-self:start}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:.5rem;border-bottom:1px solid #e2e8f0}.sr-only{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0)}`]
})
export class CoachingInstitutionMessagesComponent implements OnInit {
  private readonly api = new CoachingInstitutionMessagesApi(inject(HttpClient), environment.apiUrl);
  readonly page = signal<CoachingInstitutionConversationPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
  readonly selected = signal<CoachingInstitutionConversation | null>(null);
  readonly review = signal<CoachingConversationThread | null>(null);
  readonly loading = signal(false);
  readonly working = signal(false);
  readonly error = signal<string | null>(null);
  readonly time = coachingMessageTime;
  reason = '';

  ngOnInit() { void this.load(1); }

  async load(pageNumber: number) {
    this.loading.set(true); this.error.set(null);
    try { this.page.set(await lastValueFrom(this.api.list(pageNumber))); }
    catch (error) { this.error.set(messageOf(error)); }
    finally { this.loading.set(false); }
  }

  choose(item: CoachingInstitutionConversation) { this.selected.set(item); this.review.set(null); this.reason = ''; }

  async open() {
    const item = this.selected();
    if (!item || this.reason.trim().length < 10) return;
    this.working.set(true); this.error.set(null);
    try { this.review.set(await lastValueFrom(this.api.review(item.id, this.reason.trim()))); }
    catch (error) { this.error.set(messageOf(error)); }
    finally { this.working.set(false); }
  }

  async download(file: CoachingMessageFile) {
    const item = this.selected();
    if (!item) return;
    try { saveCoachingMessageFile(await lastValueFrom(this.api.download(item.id, file.id)), file.fileName); }
    catch (error) { this.error.set(messageOf(error)); }
  }
}
