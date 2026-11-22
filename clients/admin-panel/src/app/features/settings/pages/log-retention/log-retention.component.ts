import { Component, inject, signal, OnInit, PLATFORM_ID, computed } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SystemLogService, RetentionPolicy, CreateRetentionPolicyRequest } from '../../../../core/services/settings/system-log.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { AuthService, hasRequiredRole } from '../../../../core/auth/auth.service';

@Component({
    selector: 'app-log-retention',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="p-4 md:p-6 min-h-full">
        <!-- Header -->
        <div class="mb-6">
            <h1 class="text-xl md:text-2xl font-bold text-(--ui-text)">Kayıt saklama ayarları</h1>
            <p class="text-(--ui-text-muted) text-sm">Kayıtların ne kadar süre saklanacağını ayarlayın.</p>
        </div>

        <!-- Seq Dashboard Link -->
        <div class="bg-(--ui-brand-soft) p-6 rounded-xl border border-(--ui-brand-soft) flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8">
            <div>
                <h4 class="text-lg font-bold text-(--ui-brand-text)">Seq paneli</h4>
                <p class="text-sm text-(--ui-brand-text) mt-1">Gelişmiş analiz, grafikler ve uyarılar için Seq arayüzünü kullanın.</p>
            </div>
            <a [href]="seqUrl" target="_blank" class="px-6 py-3 bg-(--ui-brand) text-(--ui-brand-contrast) rounded-lg hover:bg-(--ui-brand) flex items-center gap-2 text-sm font-medium shadow-sm transition-all">
                Seq panelini aç <span class="material-icons text-sm">open_in_new</span>
            </a>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- Policies List -->
            <div class="lg:col-span-2">
                <div class="bg-(--ui-surface) rounded-xl border border-(--ui-border) overflow-hidden shadow-sm">
                    <div class="p-4 border-b border-(--ui-border) bg-(--ui-surface-muted) flex justify-between items-center">
                        <h3 class="font-bold text-(--ui-text) flex items-center gap-2">
                            <span class="material-icons text-(--ui-text-muted)">history</span>
                            Mevcut politikalar
                        </h3>
                        <button (click)="loadRetentionPolicies()" class="text-(--ui-brand-text) hover:text-(--ui-brand-text) text-sm font-medium flex items-center gap-1">
                            <span class="material-icons text-sm">refresh</span> Yenile
                        </button>
                    </div>
                    
                    <table class="w-full text-left">
                        <thead class="bg-(--ui-surface-muted)">
                            <tr>
                                <th class="px-6 py-3 text-xs font-medium text-(--ui-text-muted)">Süre</th>
                                <th class="px-6 py-3 text-xs font-medium text-(--ui-text-muted)">Uygulanan filtre</th>
                                <th class="px-6 py-3 text-xs font-medium text-(--ui-text-muted) w-20">İşlem</th>
                            </tr>
                        </thead>
                        <tbody class="divide-y divide-(--ui-border)">
                            <tr *ngFor="let policy of retentionPolicies()" class="hover:bg-(--ui-surface-muted) transition-colors">
                                <td class="px-6 py-4 text-sm text-(--ui-text) font-medium">
                                    {{ policy.retentionDays }} Gün
                                </td>
                                <td class="px-6 py-4 text-sm text-(--ui-text-muted) font-mono text-xs">
                                    <span *ngIf="policy.removedSignalExpression" 
                                          [class]="policy.signalTitle ? 'px-2 py-1 bg-(--ui-brand-soft) text-(--ui-brand-text) rounded font-medium' : 'px-2 py-1 bg-(--ui-surface-muted) rounded text-(--ui-text) font-mono'"
                                          [title]="getSignalTooltip(policy.removedSignalExpression)">
                                        {{ policy.signalTitle || formatSignal(policy.removedSignalExpression) }}
                                    </span>
                                    <span *ngIf="!policy.removedSignalExpression" class="text-(--ui-text-muted) italic">
                                        Tüm loglar
                                    </span>
                                </td>
                                <td class="px-6 py-4 text-right">
                                    <button *ngIf="canManage()" (click)="deletePolicy(policy.id)" class="text-(--ui-danger) hover:text-(--ui-danger) p-2 rounded-lg hover:bg-(--ui-danger-soft) transition-colors" title="Sil">
                                        <span class="material-icons text-sm">delete</span>
                                    </button>
                                </td>
                            </tr>
                            <tr *ngIf="retentionPolicies().length === 0">
                                <td colspan="3" class="px-6 py-12 text-center text-(--ui-text-muted) text-sm">
                                    <span class="material-icons text-3xl mb-2 opacity-50">policy</span>
                                    <p>Henüz bir politika tanımlanmamış.</p>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>

            <!-- Create New Policy -->
            <div *ngIf="canManage()" class="lg:col-span-1">
                <div class="bg-(--ui-surface) rounded-xl border border-(--ui-border) shadow-sm sticky top-6">
                    <div class="p-4 border-b border-(--ui-border) bg-(--ui-surface-muted)">
                        <h3 class="font-bold text-(--ui-text) flex items-center gap-2">
                            <span class="material-icons text-(--ui-text-muted)">add_circle</span>
                            Yeni politika ekle
                        </h3>
                    </div>
                    
                    <div class="p-6 space-y-4">
                        <div>
                            <label class="block text-sm font-medium text-(--ui-text) mb-1">Saklama süresi (gün)</label>
                            <div class="relative">
                                <input type="number" [(ngModel)]="newPolicyDays" min="1" class="w-full pl-3 pr-10 py-2 rounded-lg border border-(--ui-border) bg-(--ui-surface) text-sm outline-none focus:ring-2 focus:ring-(--ui-brand) text-(--ui-text) transition-all">
                                <span class="absolute right-3 top-2 text-(--ui-text-muted) text-sm">Gün</span>
                            </div>
                            <p class="text-xs text-(--ui-text-muted) mt-1">Bu süreden eski loglar silinir.</p>
                        </div>

                        <div>
                            <label class="block text-sm font-medium text-(--ui-text) mb-1">Kayıt düzeyi (isteğe bağlı)</label>
                            <select [(ngModel)]="newPolicyLevel" class="w-full px-3 py-2 rounded-lg border border-(--ui-border) bg-(--ui-surface) text-sm outline-none focus:ring-2 focus:ring-(--ui-brand) text-(--ui-text) transition-all">
                                <option [ngValue]="null">Tümü (Önerilir)</option>
                                <option value="Information">Information (Bilgi)</option>
                                <option value="Warning">Warning (Uyarı)</option>
                                <option value="Error">Error (Hata)</option>
                            </select>
                            <p class="text-xs text-(--ui-text-muted) mt-1">Sadece seçilen seviyedeki loglara uygulanır.</p>
                        </div>

                        <div class="pt-2">
                            <button (click)="createPolicy()" [disabled]="creatingPolicy || !newPolicyDays" class="w-full px-4 py-2.5 bg-(--ui-brand) text-(--ui-brand-contrast) rounded-lg hover:bg-(--ui-brand) disabled:opacity-50 text-sm font-medium transition-colors flex justify-center items-center gap-2">
                                <span *ngIf="creatingPolicy" class="material-icons animate-spin text-sm">autorenew</span>
                                {{ creatingPolicy ? 'Ekleniyor...' : 'Politikayı ekle' }}
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
    private platformId = inject(PLATFORM_ID);
    private authService = inject(AuthService);
    canManage = computed(() => hasRequiredRole(this.authService.userProfile(), 'SystemAdmin'));

    retentionPolicies = signal<RetentionPolicy[]>([]);
    seqUrl = 'http://localhost:5341';

    newPolicyDays = 30;
    newPolicyLevel: string | null = null;
    creatingPolicy = false;

    ngOnInit() {
        if (!isPlatformBrowser(this.platformId)) {
            return;
        }

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
            'Bu saklama politikasını silmek istediğinize emin misiniz?',
            { title: 'Politikayı Sil', confirmText: 'Evet, Sil', cancelText: 'Vazgeç' }
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
