import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
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
          <h1 class="text-xl md:text-2xl font-bold text-gray-900 dark:text-white">Sistem Logları</h1>
          <p class="text-gray-500 dark:text-gray-400 text-sm">Uygulama genelindeki hata ve işlem kayıtları</p>
        </div>
        <button (click)="refresh()" class="px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 transition-colors flex items-center gap-2 text-sm font-medium">
            <span class="material-icons text-sm">refresh</span> Yenile
        </button>
      </div>

      <!-- Filters -->
      <div class="bg-white dark:bg-gray-800 p-4 rounded-xl border border-gray-200 dark:border-gray-700 mb-6 flex flex-wrap gap-4 items-end">
        
        <div class="flex-1 min-w-[180px]">
          <label class="block text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">Arama</label>
          <div class="relative">
            <span class="material-icons absolute left-3 top-2.5 text-gray-400 text-sm">search</span>
            <input 
              type="text" 
              [(ngModel)]="searchTerm" 
              (ngModelChange)="onSearchChange($event)"
              placeholder="Mesaj veya Exception ara..." 
              class="w-full pl-9 pr-4 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all text-sm"
            >
          </div>
        </div>

        <div class="w-44">
          <label class="block text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">Uygulama</label>
          <select [(ngModel)]="selectedApplication" (change)="onFilterChange()" class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-indigo-500 text-sm outline-none">
            <option value="">Tüm Uygulamalar</option>
            <option *ngFor="let app of applications()" [value]="app">{{ app }}</option>
          </select>
        </div>

        <div class="w-36">
          <label class="block text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">Seviye</label>
          <select [(ngModel)]="selectedLevel" (change)="onFilterChange()" class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-indigo-500 text-sm outline-none">
            <option value="">Tümü</option>
            <option value="Information">Information</option>
            <option value="Warning">Warning</option>
            <option value="Error">Error</option>
            <option value="Fatal">Fatal</option>
          </select>
        </div>

      </div>

      <!-- Log Table -->
      <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 overflow-hidden shadow-sm">
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse min-w-[800px]">
            <thead>
              <tr class="bg-gray-50 dark:bg-gray-900 border-b border-gray-200 dark:border-gray-700 text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                <th class="px-4 py-3 font-medium w-[140px]">Tarih</th>
                <th class="px-4 py-3 font-medium w-[100px]">Seviye</th>
                <th class="px-4 py-3 font-medium w-[120px]">Uygulama</th>
                <th class="px-4 py-3 font-medium">Mesaj</th>
                <th class="px-4 py-3 font-medium w-[60px]"></th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
              <tr *ngFor="let log of logs()" class="hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors group">
                <td class="px-4 py-3 text-xs text-gray-500 dark:text-gray-400 whitespace-nowrap">
                  {{ log.timestamp | date:'dd.MM.yy HH:mm:ss' }}
                </td>
                <td class="px-4 py-3">
                  <span class="px-2 py-0.5 rounded-full text-xs font-medium"
                    [ngClass]="getLevelClass(log.level)">
                    {{ log.level }}
                  </span>
                </td>
                <td class="px-4 py-3 text-xs text-gray-600 dark:text-gray-300 whitespace-nowrap">
                  {{ log.application || '-' }}
                </td>
                <td class="px-4 py-3 text-gray-800 dark:text-gray-200 font-mono text-xs">
                  <div class="truncate max-w-[400px] lg:max-w-[600px]" [title]="log.message">{{ log.message }}</div>
                  <div *ngIf="log.exception" class="text-red-500 dark:text-red-400 text-[10px] mt-1 truncate max-w-[400px] lg:max-w-[600px] opacity-80">
                    {{ extractExceptionMessage(log.exception) }}
                  </div>
                </td>
                <td class="px-4 py-3 text-right">
                  <button (click)="openDetail(log)" class="text-indigo-600 dark:text-indigo-400 hover:text-indigo-900 dark:hover:text-indigo-300 text-xs font-medium">
                    Detay
                  </button>
                </td>
              </tr>
              <tr *ngIf="logs().length === 0 && !loading()">
                <td colspan="5" class="px-6 py-12 text-center text-gray-400 dark:text-gray-500">
                    <span class="material-icons text-4xl mb-2">history_toggle_off</span>
                    <p>Kayıt bulunamadı.</p>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Pagination -->
        <div class="bg-gray-50 dark:bg-gray-900 px-4 py-3 border-t border-gray-200 dark:border-gray-700 flex flex-col sm:flex-row items-center justify-between gap-3">
            <div class="text-xs text-gray-500 dark:text-gray-400 text-center sm:text-left">
                <span class="font-medium text-gray-700 dark:text-gray-300">{{ totalCount() }}</span> kayıttan 
                <span class="font-medium text-gray-700 dark:text-gray-300">{{ getStartIndex() }}</span> - 
                <span class="font-medium text-gray-700 dark:text-gray-300">{{ getEndIndex() }}</span> arası gösteriliyor
            </div>
            <div class="flex items-center gap-2">
                <button 
                  [disabled]="currentPage === 1"
                  (click)="changePage(currentPage - 1)"
                  class="p-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-300 disabled:opacity-40 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
                  <span class="material-icons text-sm">chevron_left</span>
                </button>
                
                <!-- Page Numbers -->
                <div class="flex items-center gap-1">
                  <ng-container *ngFor="let page of getVisiblePages()">
                    <button *ngIf="page !== '...'"
                      (click)="changePage(+page)"
                      [class.bg-indigo-600]="currentPage === page"
                      [class.text-white]="currentPage === page"
                      [class.bg-white]="currentPage !== page"
                      [class.dark:bg-gray-800]="currentPage !== page"
                      [class.text-gray-700]="currentPage !== page"
                      [class.dark:text-gray-300]="currentPage !== page"
                      class="w-8 h-8 rounded-lg border border-gray-300 dark:border-gray-600 text-xs font-medium hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
                      {{ page }}
                    </button>
                    <span *ngIf="page === '...'" class="px-1 text-gray-400">...</span>
                  </ng-container>
                </div>
                
                <button 
                  [disabled]="currentPage >= getTotalPages()"
                  (click)="changePage(currentPage + 1)"
                  class="p-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-300 disabled:opacity-40 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
                  <span class="material-icons text-sm">chevron_right</span>
                </button>
                
                <span class="text-xs text-gray-500 dark:text-gray-400 ml-2 hidden sm:inline">
                  Sayfa <span class="font-medium text-gray-700 dark:text-gray-300">{{ currentPage }}</span> / <span class="font-medium text-gray-700 dark:text-gray-300">{{ getTotalPages() }}</span>
                </span>
            </div>
        </div>
      </div>

    </div>

    <!-- Detail Modal -->
    <div *ngIf="selectedLog" class="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4" (click)="closeDetail()">
        <div class="bg-white dark:bg-gray-800 rounded-xl w-full max-w-4xl max-h-[90vh] overflow-hidden shadow-2xl flex flex-col" (click)="$event.stopPropagation()">
            <div class="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex justify-between items-center bg-gray-50 dark:bg-gray-900">
                <h3 class="font-bold text-gray-800 dark:text-white flex items-center gap-2">
                     <span class="px-2 py-1 rounded-full text-xs font-medium" [ngClass]="getLevelClass(selectedLog.level)">{{ selectedLog.level }}</span>
                     Log Detayı
                </h3>
                <button (click)="closeDetail()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="p-6 overflow-y-auto font-mono text-sm">
                <div class="grid grid-cols-2 gap-4 mb-6 text-xs text-gray-500 dark:text-gray-400">
                    <div><strong class="text-gray-700 dark:text-gray-300">Tarih:</strong> {{ selectedLog.timestamp | date:'medium' }}</div>
                    <div><strong class="text-gray-700 dark:text-gray-300">Uygulama:</strong> {{ selectedLog.application }}</div>
                </div>

                <div class="mb-4">
                    <h4 class="text-xs font-bold text-gray-700 dark:text-gray-300 uppercase mb-2">Mesaj</h4>
                    <div class="bg-gray-50 dark:bg-gray-900 p-4 rounded border border-gray-200 dark:border-gray-700 text-gray-800 dark:text-gray-200 break-words whitespace-pre-wrap">{{ selectedLog.message }}</div>
                </div>

                <div *ngIf="selectedLog.exception" class="mb-4">
                    <h4 class="text-xs font-bold text-red-700 dark:text-red-400 uppercase mb-2">Stack Trace / Exception</h4>
                    <div class="bg-red-50 dark:bg-red-900/20 p-4 rounded border border-red-200 dark:border-red-800 text-red-800 dark:text-red-300 break-words whitespace-pre-wrap overflow-x-auto text-xs">{{ selectedLog.exception }}</div>
                </div>

                <div *ngIf="selectedLog.properties" class="mb-4">
                    <h4 class="text-xs font-bold text-gray-700 dark:text-gray-300 uppercase mb-2">Properties (JSON)</h4>
                    <pre class="bg-gray-900 text-gray-100 p-4 rounded text-xs overflow-x-auto">{{ formatJson(selectedLog.properties) }}</pre>
                </div>
            </div>
            <div class="p-4 border-t border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-900 text-right">
                <button (click)="closeDetail()" class="px-4 py-2 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-600 text-sm font-medium">Kapat</button>
            </div>
        </div>
    </div>
  `,
  styles: []
})
export class SystemLogsComponent implements OnInit {
  private logService = inject(SystemLogService);

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
      case 'Error': return 'bg-red-500/20 text-red-600 dark:text-red-400';
      case 'Fatal': return 'bg-red-600/30 text-red-700 dark:text-red-300 font-bold';
      case 'Warning': return 'bg-amber-500/20 text-amber-600 dark:text-amber-400';
      case 'Information': return 'bg-blue-500/20 text-blue-600 dark:text-blue-400';
      default: return 'bg-gray-500/20 text-gray-600 dark:text-gray-400';
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
