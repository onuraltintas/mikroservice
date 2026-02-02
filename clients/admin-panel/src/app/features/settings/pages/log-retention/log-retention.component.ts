import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SystemLogService, RetentionPolicy, CreateRetentionPolicyRequest } from '../../../../core/services/settings/system-log.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
    selector: 'app-log-retention',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="p-4 md:p-6 min-h-full">
        <!-- Header -->
        <div class="mb-6">
            <h1 class="text-xl md:text-2xl font-bold text-gray-900 dark:text-white">Log Saklama Ayarları</h1>
            <p class="text-gray-500 dark:text-gray-400 text-sm">Logların ne kadar süreyle saklanacağını yapılandırın.</p>
        </div>

        <!-- Seq Dashboard Link -->
        <div class="bg-indigo-50 dark:bg-indigo-900/20 p-6 rounded-xl border border-indigo-100 dark:border-indigo-800 flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8">
            <div>
                <h4 class="text-lg font-bold text-indigo-900 dark:text-indigo-300">Seq Dashboard</h4>
                <p class="text-sm text-indigo-700 dark:text-indigo-400 mt-1">Gelişmiş analiz, grafikler ve uyarılar için Seq arayüzünü kullanın.</p>
            </div>
            <a [href]="seqUrl" target="_blank" class="px-6 py-3 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 flex items-center gap-2 text-sm font-medium shadow-sm transition-all hover:shadow-indigo-200 dark:hover:shadow-none">
                Seq Panelini Aç <span class="material-icons text-sm">open_in_new</span>
            </a>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- Policies List -->
            <div class="lg:col-span-2">
                <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 overflow-hidden shadow-sm">
                    <div class="p-4 border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-900 flex justify-between items-center">
                        <h3 class="font-bold text-gray-800 dark:text-white flex items-center gap-2">
                            <span class="material-icons text-gray-500">history</span>
                            Mevcut Politikalar
                        </h3>
                        <button (click)="loadRetentionPolicies()" class="text-indigo-600 dark:text-indigo-400 hover:text-indigo-800 dark:hover:text-indigo-300 text-sm font-medium flex items-center gap-1">
                            <span class="material-icons text-sm">refresh</span> Yenile
                        </button>
                    </div>
                    
                    <table class="w-full text-left">
                        <thead class="bg-gray-50 dark:bg-gray-900/50">
                            <tr>
                                <th class="px-6 py-3 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Süre</th>
                                <th class="px-6 py-3 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Uygulanan Filtre</th>
                                <th class="px-6 py-3 text-xs font-medium text-gray-500 dark:text-gray-400 w-20">İşlem</th>
                            </tr>
                        </thead>
                        <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
                            <tr *ngFor="let policy of retentionPolicies()" class="hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors">
                                <td class="px-6 py-4 text-sm text-gray-800 dark:text-gray-200 font-medium">
                                    {{ policy.retentionDays }} Gün
                                </td>
                                <td class="px-6 py-4 text-sm text-gray-600 dark:text-gray-400 font-mono text-xs">
                                    <span *ngIf="policy.removedSignalExpression" 
                                          [class]="policy.signalTitle ? 'px-2 py-1 bg-indigo-100 dark:bg-indigo-900/30 text-indigo-700 dark:text-indigo-300 rounded font-medium' : 'px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded text-gray-700 dark:text-gray-300 font-mono'"
                                          [title]="getSignalTooltip(policy.removedSignalExpression)">
                                        {{ policy.signalTitle || formatSignal(policy.removedSignalExpression) }}
                                    </span>
                                    <span *ngIf="!policy.removedSignalExpression" class="text-gray-400 italic">
                                        Tüm Loglar
                                    </span>
                                </td>
                                <td class="px-6 py-4 text-right">
                                    <button (click)="deletePolicy(policy.id)" class="text-red-500 hover:text-red-700 p-2 rounded-lg hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors" title="Sil">
                                        <span class="material-icons text-sm">delete</span>
                                    </button>
                                </td>
                            </tr>
                            <tr *ngIf="retentionPolicies().length === 0">
                                <td colspan="3" class="px-6 py-12 text-center text-gray-500 text-sm">
                                    <span class="material-icons text-3xl mb-2 opacity-50">policy</span>
                                    <p>Henüz bir politika tanımlanmamış.</p>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>

            <!-- Create New Policy -->
            <div class="lg:col-span-1">
                <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm sticky top-6">
                    <div class="p-4 border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-900">
                        <h3 class="font-bold text-gray-800 dark:text-white flex items-center gap-2">
                            <span class="material-icons text-gray-500">add_circle</span>
                            Yeni Politika Ekle
                        </h3>
                    </div>
                    
                    <div class="p-6 space-y-4">
                        <div>
                            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Saklama Süresi (Gün)</label>
                            <div class="relative">
                                <input type="number" [(ngModel)]="newPolicyDays" min="1" class="w-full pl-3 pr-10 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm outline-none focus:ring-2 focus:ring-indigo-500 text-gray-900 dark:text-white transition-all">
                                <span class="absolute right-3 top-2 text-gray-400 text-sm">Gün</span>
                            </div>
                            <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">Bu süreden eski loglar silinir.</p>
                        </div>

                        <div>
                            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Log Seviyesi (Opsiyonel)</label>
                            <select [(ngModel)]="newPolicyLevel" class="w-full px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm outline-none focus:ring-2 focus:ring-indigo-500 text-gray-900 dark:text-white transition-all">
                                <option [ngValue]="null">Tümü (Önerilir)</option>
                                <option value="Information">Information (Bilgi)</option>
                                <option value="Warning">Warning (Uyarı)</option>
                                <option value="Error">Error (Hata)</option>
                            </select>
                            <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">Sadece seçilen seviyedeki loglara uygulanır.</p>
                        </div>

                        <div class="pt-2">
                            <button (click)="createPolicy()" [disabled]="creatingPolicy || !newPolicyDays" class="w-full px-4 py-2.5 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 disabled:opacity-50 text-sm font-medium transition-colors flex justify-center items-center gap-2">
                                <span *ngIf="creatingPolicy" class="material-icons animate-spin text-sm">autorenew</span>
                                {{ creatingPolicy ? 'Ekleniyor...' : 'Politikayı Ekle' }}
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
    `
})
export class LogRetentionComponent implements OnInit {
    private logService = inject(SystemLogService);
    private toaster = inject(ToasterService);

    retentionPolicies = signal<RetentionPolicy[]>([]);
    seqUrl = 'http://localhost:5341';

    newPolicyDays = 30;
    newPolicyLevel: string | null = null;
    creatingPolicy = false;

    ngOnInit() {
        this.loadRetentionPolicies();
        this.loadSeqUrl();
    }

    loadSeqUrl() {
        this.logService.getSeqUrl().subscribe({
            next: (res) => this.seqUrl = res.url,
            error: () => { }
        });
    }

    loadRetentionPolicies() {
        this.logService.getRetentionPolicies().subscribe({
            next: (policies) => this.retentionPolicies.set(policies),
            error: (err) => console.error('Failed to load retention policies', err)
        });
    }

    createPolicy() {
        if (!this.newPolicyDays) return;

        this.creatingPolicy = true;
        const request: CreateRetentionPolicyRequest = {
            retentionDays: this.newPolicyDays,
            logLevel: this.newPolicyLevel || undefined
        };

        this.logService.createRetentionPolicy(request).subscribe({
            next: () => {
                this.creatingPolicy = false;
                this.newPolicyDays = 30;
                this.newPolicyLevel = null;
                this.loadRetentionPolicies();
            },
            error: (err) => {
                console.error('Failed to create policy', err);
                this.creatingPolicy = false;
                this.toaster.error('Politika eklenemedi.');
            }
        });
    }

    async deletePolicy(id: string) {
        const confirmed = await this.toaster.confirm(
            'Politikayı Sil',
            'Bu saklama politikasını silmek istediğinize emin misiniz?',
            'Evet, Sil',
            'Vazgeç'
        );

        if (!confirmed) return;

        this.logService.deleteRetentionPolicy(id).subscribe({
            next: () => {
                this.loadRetentionPolicies();
                this.toaster.success('Politika başarıyla silindi.');
            },
            error: (err) => {
                console.error('Failed to delete policy', err);
                this.toaster.error('Politika silinemedi.');
            }
        });
    }

    formatSignal(expression: any): string {
        if (!expression) return '';
        if (typeof expression === 'string') return expression;

        // Handle various JsonElement/Object structures
        // Backend returns JsonElement which might be serialized as regular object
        const signalId = expression.SignalId || expression.signalId || expression.signalid;

        if (signalId) return `Seviye Filtreli`;
        return 'Filtreli';
    }

    getSignalTooltip(expression: any): string {
        return JSON.stringify(expression);
    }
}
