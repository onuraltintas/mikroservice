import { Component, inject, signal, OnInit, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SystemLogService, LogEntry, LogFilterRequest } from '../../../../core/services/settings/system-log.service';
import { debounceTime, distinctUntilChanged, Subject } from 'rxjs';

@Component({
  selector: 'app-system-logs',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-4 md:p-6 min-h-full">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 mb-6">
        <div>
          <h1 class="text-xl md:text-2xl font-bold text-(--ui-text)">Sistem kayıtları</h1>
          <p class="text-(--ui-text-muted) text-sm">Uygulama genelindeki hata ve işlem kayıtları</p>
        </div>
        <button (click)="refresh()" class="px-4 py-2 bg-(--ui-brand) text-(--ui-brand-contrast) rounded-lg hover:bg-(--ui-brand) transition-colors flex items-center gap-2 text-sm font-medium">
            <span class="material-icons text-sm">refresh</span> Yenile
        </button>
      </div>

      <!-- Filters -->
      <div class="bg-(--ui-surface) p-4 rounded-xl border border-(--ui-border) mb-6 flex flex-wrap gap-4 items-end">
        
        <div class="flex-1 min-w-[180px]">
          <label class="block text-xs font-medium text-(--ui-text) mb-1">Arama</label>
          <div class="relative">
            <span class="material-icons absolute left-3 top-2.5 text-(--ui-text-muted) text-sm">search</span>
            <input 
              type="text" 
              [(ngModel)]="searchTerm" 
              (ngModelChange)="onSearchChange($event)"
              placeholder="Mesaj veya Exception ara..." 
              class="w-full pl-9 pr-4 py-2 rounded-lg border border-(--ui-border) bg-(--ui-surface) text-(--ui-text) focus:ring-2 focus:ring-(--ui-brand) focus:border-(--ui-brand) outline-none transition-all text-sm"
            >
          </div>
        </div>

        <div class="w-44">
          <label class="block text-xs font-medium text-(--ui-text) mb-1">Uygulama</label>
          <select [(ngModel)]="selectedApplication" (change)="onFilterChange()" class="w-full px-3 py-2 rounded-lg border border-(--ui-border) bg-(--ui-surface) text-(--ui-text) focus:ring-2 focus:ring-(--ui-brand) text-sm outline-none">
            <option value="">Tüm uygulamalar</option>
            <option *ngFor="let app of applications()" [value]="app">{{ app }}</option>
          </select>
        </div>

        <div class="w-36">
          <label class="block text-xs font-medium text-(--ui-text) mb-1">Seviye</label>
          <select [(ngModel)]="selectedLevel" (change)="onFilterChange()" class="w-full px-3 py-2 rounded-lg border border-(--ui-border) bg-(--ui-surface) text-(--ui-text) focus:ring-2 focus:ring-(--ui-brand) text-sm outline-none">
            <option value="">Tümü</option>
            <option value="Information">Information</option>
            <option value="Warning">Warning</option>
            <option value="Error">Error</option>
            <option value="Fatal">Fatal</option>
          </select>
        </div>

      </div>

      <!-- Log Table -->
      <div class="bg-(--ui-surface) rounded-xl border border-(--ui-border) overflow-hidden shadow-sm">
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse min-w-[800px]">
            <thead>
              <tr class="bg-(--ui-surface-muted) border-b border-(--ui-border) text-xs text-(--ui-text-muted)">
                <th class="px-4 py-3 font-medium w-[140px]">Tarih</th>
                <th class="px-4 py-3 font-medium w-[100px]">Seviye</th>
                <th class="px-4 py-3 font-medium w-[120px]">Uygulama</th>
                <th class="px-4 py-3 font-medium">Mesaj</th>
                <th class="px-4 py-3 font-medium w-[60px]"></th>
              </tr>
            </thead>
            <tbody class="divide-y divide-(--ui-border)">
              <tr *ngFor="let log of logs()" class="hover:bg-(--ui-surface-muted) transition-colors group">
                <td class="px-4 py-3 text-xs text-(--ui-text-muted) whitespace-nowrap">
                  {{ log.timestamp | date:'dd.MM.yy HH:mm:ss' }}
                </td>
                <td class="px-4 py-3">
                  <span class="px-2 py-0.5 rounded-full text-xs font-medium"
                    [ngClass]="getLevelClass(log.level)">
                    {{ log.level }}
                  </span>
                </td>
                <td class="px-4 py-3 text-xs text-(--ui-text-muted) whitespace-nowrap">
                  {{ log.application || '-' }}
                </td>
                <td class="px-4 py-3 text-(--ui-text) font-mono text-xs">
                  <div class="truncate max-w-[400px] lg:max-w-[600px]" [title]="log.message">{{ log.message }}</div>
                  <div *ngIf="log.exception" class="text-(--ui-danger) text-[10px] mt-1 truncate max-w-[400px] lg:max-w-[600px] opacity-80">
                    {{ extractExceptionMessage(log.exception) }}
                  </div>
                </td>
                <td class="px-4 py-3 text-right">
                  <button (click)="openDetail(log)" class="text-(--ui-brand-text) hover:text-(--ui-brand-text) text-xs font-medium">
                    Detay
                  </button>
                </td>
              </tr>
              <tr *ngIf="logs().length === 0 && !loading()">
                <td colspan="5" class="px-6 py-12 text-center text-(--ui-text-muted)">
                    <span class="material-icons text-4xl mb-2">history_toggle_off</span>
                    <p>Kayıt bulunamadı.</p>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Pagination -->
        <div class="bg-(--ui-surface-muted) px-4 py-3 border-t border-(--ui-border) flex flex-col sm:flex-row items-center justify-between gap-3">
            <div class="text-xs text-(--ui-text-muted) text-center sm:text-left">
                <span class="font-medium text-(--ui-text)">{{ totalCount() }}</span> kayıttan 
                <span class="font-medium text-(--ui-text)">{{ getStartIndex() }}</span> - 
                <span class="font-medium text-(--ui-text)">{{ getEndIndex() }}</span> arası gösteriliyor
            </div>
            <div class="flex items-center gap-2">
                <button 
                  [disabled]="currentPage === 1"
                  (click)="changePage(currentPage - 1)"
                  class="p-2 rounded-lg border border-(--ui-border) bg-(--ui-surface) text-(--ui-text) disabled:opacity-40 hover:bg-(--ui-surface-muted) transition-colors">
                  <span class="material-icons text-sm">chevron_left</span>
                </button>
                
                <!-- Page Numbers -->
                <div class="flex items-center gap-1">
                  <ng-container *ngFor="let page of getVisiblePages()">
                    <button *ngIf="page !== '...'"
                      (click)="changePage(+page)"
                      [class.bg-(--ui-brand)]="currentPage === page"
                      [class.text-white]="currentPage === page"
                      [class.bg-(--ui-surface)]="currentPage !== page"
                      [class.text-(--ui-text)]="currentPage !== page"
                      class="w-8 h-8 rounded-lg border border-(--ui-border) text-xs font-medium hover:bg-(--ui-surface-muted) transition-colors">
                      {{ page }}
                    </button>
                    <span *ngIf="page === '...'" class="px-1 text-(--ui-text-muted)">...</span>
                  </ng-container>
                </div>
                
                <button 
                  [disabled]="currentPage >= getTotalPages()"
                  (click)="changePage(currentPage + 1)"
                  class="p-2 rounded-lg border border-(--ui-border) bg-(--ui-surface) text-(--ui-text) disabled:opacity-40 hover:bg-(--ui-surface-muted) transition-colors">
                  <span class="material-icons text-sm">chevron_right</span>
                </button>
                
                <span class="text-xs text-(--ui-text-muted) ml-2 hidden sm:inline">
                  Sayfa <span class="font-medium text-(--ui-text)">{{ currentPage }}</span> / <span class="font-medium text-(--ui-text)">{{ getTotalPages() }}</span>
                </span>
            </div>
        </div>
      </div>

    </div>

    <!-- Detail Modal -->
    <div *ngIf="selectedLog" class="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4" (click)="closeDetail()">
        <div class="bg-(--ui-surface) rounded-xl w-full max-w-4xl max-h-[90vh] overflow-hidden shadow-2xl flex flex-col" (click)="$event.stopPropagation()">
            <div class="px-6 py-4 border-b border-(--ui-border) flex justify-between items-center bg-(--ui-surface-muted)">
                <h3 class="font-bold text-(--ui-text) flex items-center gap-2">
                     <span class="px-2 py-1 rounded-full text-xs font-medium" [ngClass]="getLevelClass(selectedLog.level)">{{ selectedLog.level }}</span>
                     Log detayı
                </h3>
                <button (click)="closeDetail()" class="text-(--ui-text-muted) hover:text-(--ui-text-muted)">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="p-6 overflow-y-auto font-mono text-sm">
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-6 text-xs text-(--ui-text-muted)">
                    <div><strong class="text-(--ui-text)">Tarih:</strong> {{ selectedLog.timestamp | date:'medium' }}</div>
                    <div><strong class="text-(--ui-text)">Uygulama:</strong> {{ selectedLog.application }}</div>
                </div>

                <div class="mb-4">
                    <h4 class="text-xs font-bold text-(--ui-text) mb-2">Mesaj</h4>
                    <div class="bg-(--ui-surface-muted) p-4 rounded border border-(--ui-border) text-(--ui-text) break-words whitespace-pre-wrap">{{ selectedLog.message }}</div>
                </div>

                <div *ngIf="selectedLog.exception" class="mb-4">
                    <h4 class="text-xs font-bold text-(--ui-danger) mb-2">Stack Trace / Exception</h4>
                    <div class="bg-(--ui-danger-soft) p-4 rounded border border-(--ui-danger-soft) text-(--ui-danger) break-words whitespace-pre-wrap overflow-x-auto text-xs">{{ selectedLog.exception }}</div>
                </div>

                <div *ngIf="selectedLog.properties" class="mb-4">
                    <h4 class="text-xs font-bold text-(--ui-text) mb-2">Properties (JSON)</h4>
                    <pre class="bg-(--ui-text) text-(--ui-text-muted) p-4 rounded text-xs overflow-x-auto">{{ formatJson(selectedLog.properties) }}</pre>
                </div>
            </div>
            <div class="p-4 border-t border-(--ui-border) bg-(--ui-surface-muted) text-right">
                <button (click)="closeDetail()" class="px-4 py-2 bg-(--ui-surface) border border-(--ui-border) text-(--ui-text) rounded-lg hover:bg-(--ui-surface-muted) text-sm font-medium">Kapat</button>
            </div>
        </div>
    </div>
  `,
  styles: []
})
export class SystemLogsComponent implements OnInit {
  private logService = inject(SystemLogService);
  private platformId = inject(PLATFORM_ID);

  logs = signal<LogEntry[]>([]);
  totalCount = signal<number>(0);
  loading = signal<boolean>(false);
  applications = signal<string[]>([]);

  searchTerm = '';
  selectedLevel = '';
  selectedApplication = '';
  currentPage = 1;
  pageSize = 25;

  selectedLog: LogEntry | null = null;
  Math = Math;

  private searchSubject = new Subject<string>();

  ngOnInit() {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.searchSubject.pipe(
      debounceTime(500),
      distinctUntilChanged()
    ).subscribe(() => {
      this.currentPage = 1;
      this.loadLogs();
    });

    this.loadApplications();
    this.loadLogs();
  }

  loadApplications() {
    this.logService.getApplications().subscribe({
      next: (apps) => {
        this.applications.set(apps);
      },
      error: (err) => {
        console.error('Failed to fetch applications', err);
      }
    });
  }

  loadLogs() {
    this.loading.set(true);
    const filter: LogFilterRequest = {
      page: this.currentPage,
      pageSize: this.pageSize,
      level: this.selectedLevel || undefined,
      application: this.selectedApplication || undefined,
      searchTerm: this.searchTerm || undefined
    };

    this.logService.getLogs(filter).subscribe({
      next: (res) => {
        this.logs.set(res.logs);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Logs fetching failed', err);
        this.loading.set(false);
      }
    });
  }

  refresh() {
    this.loadApplications();
    this.loadLogs();
  }

  onSearchChange(value: string) {
    this.searchSubject.next(value);
  }

  onFilterChange() {
    this.currentPage = 1;
    this.loadLogs();
  }

  changePage(page: number) {
    if (page >= 1 && page <= this.getTotalPages()) {
      this.currentPage = page;
      this.loadLogs();
    }
  }

  getTotalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }

  getStartIndex(): number {
    if (this.totalCount() === 0) return 0;
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  getEndIndex(): number {
    return Math.min(this.currentPage * this.pageSize, this.totalCount());
  }

  getVisiblePages(): (number | string)[] {
    const total = this.getTotalPages();
    const current = this.currentPage;
    const pages: (number | string)[] = [];

    if (total <= 7) {
      for (let i = 1; i <= total; i++) pages.push(i);
    } else {
      pages.push(1);

      if (current > 3) pages.push('...');

      const start = Math.max(2, current - 1);
      const end = Math.min(total - 1, current + 1);

      for (let i = start; i <= end; i++) pages.push(i);

      if (current < total - 2) pages.push('...');

      pages.push(total);
    }

    return pages;
  }

  getLevelClass(level: string): string {
    switch (level) {
      case 'Error': return 'bg-(--ui-danger) text-(--ui-danger)';
      case 'Fatal': return 'bg-(--ui-danger) text-(--ui-danger) font-bold';
      case 'Warning': return 'bg-(--ui-warning) text-(--ui-warning)';
      case 'Information': return 'bg-(--ui-brand) text-(--ui-brand-text)';
      default: return 'bg-(--ui-text) text-(--ui-text-muted)';
    }
  }

  extractExceptionMessage(exception: string): string {
    return exception.split('\n')[0];
  }

  openDetail(log: LogEntry) {
    this.selectedLog = log;
  }

  closeDetail() {
    this.selectedLog = null;
  }

  formatJson(json: string | undefined): string {
    if (!json) return '';
    try {
      return JSON.stringify(JSON.parse(json), null, 2);
    } catch {
      return json;
    }
  }
}
