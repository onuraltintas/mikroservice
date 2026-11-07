import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingDatePipe } from '../coaching-labels';
import { CoachingParentService, ParentLink, ParentLinkCode, ParentLinkCodeStatus } from '../parent/coaching-parent.service';
import { parentCodeNote, staleParentCode } from '../onboarding/onboarding-model';
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
        <p class="mt-4 rounded-xl bg-(--ui-brand-soft) p-4 text-sm ">Velinle paylaş: <strong class="font-mono text-lg tracking-widest">{{ issued.code }}</strong><br>{{ note(issued.expiresAt) }} Velin kodu girdiğinde isteği burada onaylarsın.</p>
      } @else if (expired(); as old) {
        <p class="mt-4 rounded-xl bg-(--ui-brand-soft) p-4 text-sm" data-testid="expired-code">Veli kodunun süresi doldu ({{ old.expiresAt | coachingDate }}). Velin bağlanacaksa yeni bir kod oluştur.</p>
      } @else if (active(); as current) {
        <!-- E4 #21: a code made in the wizard (or earlier here); only a hash is stored, so its expiry is shown, not the code. -->
        <p class="mt-4 rounded-xl bg-(--ui-brand-soft) p-4 text-sm" data-testid="active-code">Aktif bir veli kodun var: {{ note(current.expiresAt) }}<br>Kodu güvenliğin için yalnız oluşturduğunda gösteriyoruz; kaybettiysen yeni kod oluştur.</p>
      }
      @if (confirmReplace()) {
        <div class="mt-4 rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm dark:border-amber-700 dark:bg-amber-950" role="group" aria-labelledby="replace-code-text" data-testid="replace-code">
          <p id="replace-code-text">Yeni kod oluşturursan eski kod hemen geçersiz olur. Velin eski kodu girerse bağlantı kurulamaz.</p>
          <span class="mt-2 flex gap-2">
            <button type="button" class="rounded-lg bg-(--ui-brand) px-3 py-1 font-medium text-white disabled:opacity-50" [disabled]="busy()" (click)="createCode()">Evet, yeni kod oluştur</button>
            <button type="button" class="rounded-lg border border-slate-300 px-3 py-1 disabled:opacity-50 dark:border-slate-700" [disabled]="busy()" (click)="confirmReplace.set(false)">Vazgeç</button>
          </span>
        </div>
      } @else {
        <button type="button" class="mt-4 rounded-lg border border-(--ui-border) px-4 py-2 text-sm font-medium text-(--ui-brand-text) disabled:opacity-50 " [disabled]="busy()" (click)="askCode()">{{ hasCode() ? 'Yeni kod oluştur' : 'Veli kodu oluştur' }}</button>
      }
    </section>
  `
})
export class StudentParentLinksComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  private readonly toaster = inject(ToasterService);
  readonly links = signal<ParentLink[]>([]);
  readonly code = signal<ParentLinkCode | null>(null);
  /** E4 #21: an unused code made earlier (in the wizard or here). */
  private readonly rawCode = signal<ParentLinkCodeStatus | null>(null);
  /** The earlier code while it can still be used. */
  readonly active = computed(() => { const code = this.rawCode(); return code && !code.expired ? code : null; });
  readonly confirmReplace = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void { this.load(); }

  relationshipLabel(value: string): string { return parentRelationshipLabel(value); }

  /** E4 review: an expired code is not replaced by asking (it no longer works anyway). */
  hasCode() { return !!this.code() || !!this.active(); }
  /** F2 (E4 c): a code whose expiry is long past is not mentioned any more. */
  expired() { const code = this.code() ? null : this.rawCode(); return code?.expired && !staleParentCode(code) ? code : null; }
  note(expiresAt: string) { return parentCodeNote(expiresAt); }
  /** E4 #21: a new code replaces the old one (the server revokes it), so the student is asked in the page first. */
  askCode(): void { if (this.hasCode()) this.confirmReplace.set(true); else void this.createCode(); }

  async createCode(): Promise<void> {
    await this.run(async () => {
      const issued = await firstValueFrom(this.service.createCode());
      this.code.set(issued); this.rawCode.set({ createdAt: new Date().toISOString(), expiresAt: issued.expiresAt }); this.confirmReplace.set(false);
    }, 'Veli kodu oluşturulamadı.', false);
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
    this.service.getActiveCode().subscribe({ next: code => this.rawCode.set(code), error: () => this.rawCode.set(null) });
    this.service.getLinks().subscribe({ next: links => this.links.set(links), error: () => this.error.set('Veli bağlantıların yüklenemedi.') });
  }
}
