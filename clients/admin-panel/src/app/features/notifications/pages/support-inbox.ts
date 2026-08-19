import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { computed } from '@angular/core';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { SupportAdminService, SupportRequestDto } from '../../../core/services/support-admin.service';

@Component({
  selector: 'app-support-inbox',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="space-y-6">
      <header><h1 class="text-2xl font-bold text-gray-900 dark:text-white">Destek Gelen Kutusu</h1><p class="text-sm text-gray-500">Kullanıcı taleplerini filtreleyin, notlandırın ve güvenli e-posta kuyruğundan yanıtlayın.</p></header>
      <div class="flex flex-wrap gap-3"><input class="rounded-lg border p-2 dark:bg-gray-900" [(ngModel)]="search" (keyup.enter)="load()" placeholder="E-posta veya konu ara"><select class="rounded-lg border p-2 dark:bg-gray-900" [(ngModel)]="processed" (change)="load()"><option [ngValue]="undefined">Tümü</option><option [ngValue]="false">Açık</option><option [ngValue]="true">İşlenmiş</option></select><button class="rounded-lg border px-4 py-2" (click)="load()">Yenile</button></div>
      @if (error()) { <div class="rounded-lg bg-red-50 p-3 text-red-700" role="alert">{{ error() }}</div> }
      <div class="overflow-x-auto rounded-xl border bg-white dark:border-gray-700 dark:bg-gray-800"><table class="min-w-full text-left text-sm"><thead class="border-b bg-gray-50 dark:border-gray-700 dark:bg-gray-900"><tr><th class="p-3">Talep</th><th class="p-3">Gönderen</th><th class="p-3">Tarih</th><th class="p-3">Durum</th><th class="p-3">İşlem</th></tr></thead><tbody>
        @for (request of requests(); track request.id) { <tr class="border-b align-top dark:border-gray-700"><td class="max-w-md p-3"><div class="font-semibold">{{ request.subject }}</div><div class="mt-1 whitespace-pre-wrap text-gray-600 dark:text-gray-300">{{ request.message }}</div></td><td class="p-3">{{ request.firstName }} {{ request.lastName }}<br><span class="text-xs text-gray-500">{{ request.email }}</span></td><td class="p-3 whitespace-nowrap">{{ request.createdAt | date:'short' }}</td><td class="p-3"><span [class]="request.isProcessed ? 'text-emerald-600' : 'text-amber-600'">{{ request.isProcessed ? 'İşlendi' : 'Açık' }}</span></td><td class="min-w-64 p-3">@if (canReply()) { <textarea class="mb-2 w-full rounded border p-2 text-sm dark:bg-gray-900" [(ngModel)]="notes[request.id]" placeholder="Admin notu" maxlength="2000"></textarea><textarea class="mb-2 w-full rounded border p-2 text-sm dark:bg-gray-900" [(ngModel)]="replies[request.id]" placeholder="Yanıt (e-posta kuyruğuna alınır)" maxlength="2000"></textarea><div class="flex gap-2"><button class="rounded bg-indigo-600 px-3 py-1 text-white" (click)="reply(request)">Yanıtla</button><button class="rounded border px-3 py-1" (click)="process(request)">İşlendi</button></div> } @else { <span class="text-xs text-gray-500">Salt okunur</span> }</td></tr> } @empty { <tr><td colspan="5" class="p-8 text-center text-gray-500">Talep bulunamadı.</td></tr> }
      </tbody></table></div><div class="text-sm text-gray-500">Toplam {{ totalCount() }} talep</div>
    </section>
  `
})
export class SupportInboxComponent {
  private readonly service = inject(SupportAdminService);
  private readonly authService = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);
  canReply = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.supportReply));
  requests = signal<SupportRequestDto[]>([]); totalCount = signal(0); error = signal<string | null>(null);
  search = ''; processed: boolean | undefined = false; notes: Record<string, string> = {}; replies: Record<string, string> = {};
  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }
  load() { this.service.getAll(1, 100, this.processed, this.search).subscribe({ next: response => { this.requests.set(response.items); this.totalCount.set(response.totalCount); }, error: () => this.error.set('Destek talepleri yüklenemedi.') }); }
  process(request: SupportRequestDto) { this.service.process(request.id, this.notes[request.id]).subscribe({ next: () => this.load(), error: () => this.error.set('Talep işaretlenemedi.') }); }
  reply(request: SupportRequestDto) { const message = this.replies[request.id]?.trim(); if (!message) return; this.service.reply(request.id, message).subscribe({ next: () => { this.replies[request.id] = ''; this.load(); }, error: () => this.error.set('Yanıt gönderilemedi.') }); }
}
