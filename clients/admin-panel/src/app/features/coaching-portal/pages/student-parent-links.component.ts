import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingDatePipe } from '../coaching-labels';
import { CoachingParentService, ParentLink, ParentLinkCode } from '../parent/coaching-parent.service';
import { apiMessage, parentRelationshipLabel } from '../parent/parent-home.component';

/** Roadmap Y7: the student gives a parent a code, approves the parent's request and can end the link. */
@Component({
  selector: 'app-student-parent-links',
  standalone: true,
  imports: [CommonModule, CoachingDatePipe],
  template: `
    <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="parent-links-heading">
      <h2 id="parent-links-heading" class="font-semibold">Veli bağlantısı</h2>
      <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">Velin, senin onayınla çalışmanı, ödevlerini, seanslarını, denemelerini ve ücretlerini görebilir; hiçbir şeyi değiştiremez. Bağlantıyı istediğin zaman kaldırabilirsin.</p>
      @if (error()) { <p role="alert" class="mt-3 text-sm text-red-700 dark:text-red-300">{{ error() }}</p> }
      @for (link of links(); track link.id) {
        <div class="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 pt-3 text-sm dark:border-slate-800">
          <span><strong>{{ link.parentName }}</strong> · {{ relationshipLabel(link.relationship) }} · {{ link.status === 'Pending' ? 'onayını bekliyor' : (link.verifiedAt | coachingDate) + ' tarihinden beri bağlı' }}</span>
          <span class="flex gap-2">
            @if (link.status === 'Pending') { <button type="button" class="rounded-lg bg-(--ui-brand) px-3 py-1 font-medium text-white disabled:opacity-50" [disabled]="busy()" (click)="approve(link)">Onayla</button> }
            <button type="button" class="rounded-lg border border-slate-300 px-3 py-1 disabled:opacity-50 dark:border-slate-700" [disabled]="busy()" (click)="revoke(link)">{{ link.status === 'Pending' ? 'Reddet' : 'Bağlantıyı kaldır' }}</button>
          </span>
        </div>
      }
      @if (code(); as issued) {
        <p class="mt-4 rounded-xl bg-(--ui-brand-soft) p-4 text-sm ">Velinle paylaş: <strong class="font-mono text-lg tracking-widest">{{ issued.code }}</strong><br>{{ issued.expiresAt | coachingDate }} tarihine kadar, bir kez kullanılabilir. Velin kodu girdiğinde isteği burada onaylarsın.</p>
      }
      <button type="button" class="mt-4 rounded-lg border border-(--ui-border) px-4 py-2 text-sm font-medium text-(--ui-brand-text) disabled:opacity-50 " [disabled]="busy()" (click)="createCode()">{{ code() ? 'Yeni kod oluştur' : 'Veli kodu oluştur' }}</button>
    </section>
  `
})
export class StudentParentLinksComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  private readonly toaster = inject(ToasterService);
  readonly links = signal<ParentLink[]>([]);
  readonly code = signal<ParentLinkCode | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void { this.load(); }

  relationshipLabel(value: string): string { return parentRelationshipLabel(value); }

  async createCode(): Promise<void> {
    await this.run(async () => this.code.set(await firstValueFrom(this.service.createCode())), 'Veli kodu oluşturulamadı.', false);
  }

  async approve(link: ParentLink): Promise<void> {
    if (!await this.toaster.confirm(`${link.parentName} senin Koçluk bilgilerini görebilsin mi? Bu bağlantıyı istediğin zaman kaldırabilirsin.`)) return;
    await this.run(() => firstValueFrom(this.service.approveLink(link.id)), 'Bağlantı onaylanamadı.');
  }

  async revoke(link: ParentLink): Promise<void> {
    if (!await this.toaster.confirm(`${link.parentName} ile veli bağlantısı kaldırılsın mı?`)) return;
    await this.run(() => firstValueFrom(this.service.revokeLink(link.id)), 'Bağlantı kaldırılamadı.');
  }

  private async run(action: () => Promise<unknown>, failure: string, reload = true): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(null);
    try { await action(); if (reload) this.load(); }
    catch (response) { this.error.set(apiMessage(response, failure)); }
    finally { this.busy.set(false); }
  }

  private load(): void {
    this.service.getLinks().subscribe({ next: links => this.links.set(links), error: () => this.error.set('Veli bağlantıların yüklenemedi.') });
  }
}
