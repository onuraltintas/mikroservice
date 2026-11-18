import { Component, inject, signal, OnInit, computed, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfigurationService, Configuration, ConfigurationDataType } from '../../../../core/services/settings/configuration.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { AuthService, hasRequiredRole } from '../../../../core/auth/auth.service';

const MFA_POLICY_MODES = new Set(['required', 'mutations', 'disabled']);

interface MfaCategoryPolicyDefinition {
    category: string;
    label: string;
    description: string;
}

interface MfaCategoryPolicyView extends MfaCategoryPolicyDefinition {
    config: Configuration;
}

const MFA_CATEGORY_POLICIES: MfaCategoryPolicyDefinition[] = [
    { category: 'users', label: 'Kullanıcılar', description: 'Kullanıcı, oturum ve MFA yönetimi' },
    { category: 'roles-permissions', label: 'Roller ve izinler', description: 'Rol ve yetki değişiklikleri' },
    { category: 'institutions', label: 'Kurumlar', description: 'Kurum ve kurum yöneticisi işlemleri' },
    { category: 'system', label: 'Sistem', description: 'Sistem ayarları, operasyon kayıtları ve yönetici oturum MFA\'sı' },
    { category: 'speed-reading', label: 'Hızlı okuma', description: 'Katalog, program ve ilerleme yönetimi' },
    { category: 'coaching', label: 'Koçluk', description: 'Atama, oturum, sınav ve hedef yönetimi' },
    { category: 'cms', label: 'CMS ve iletişim', description: 'Sayfa, medya, e-posta ve destek yönetimi' }
];

@Component({
    selector: 'app-configurations',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="p-4 md:p-6 min-h-full">
        <!-- Header -->
        <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8">
            <div>
                <h1 class="text-xl md:text-2xl font-bold text-(--ui-text)">Sistem Operasyon Merkezi</h1>
                <p class="text-(--ui-text-muted) text-sm">Sistem parametrelerini ve modül durumlarını dinamik olarak yönetin.</p>
            </div>
            @if (canManage()) { <div class="flex gap-2">
                <button (click)="refreshRedisCache()" class="px-4 py-2 bg-(--ui-brand-soft) text-(--ui-brand-text) rounded-lg hover:bg-(--ui-brand-soft) transition-all flex items-center gap-2 text-sm font-medium">
                    <span class="material-icons text-sm">cached</span> Önbelleği (Redis) Temizle
                </button>
                <button (click)="showCreateModal = true" class="px-4 py-2 bg-(--ui-brand) text-(--ui-brand-contrast) rounded-lg hover:bg-(--ui-brand) transition-all flex items-center gap-2 text-sm font-medium shadow-sm">
                    <span class="material-icons text-sm">add</span> Yeni Ayar
                </button>
            </div> }
        </div>

        <!-- NEW: Maintenance Mode Operations Center -->
        <div class="mb-10 animate-in fade-in slide-in-from-top-4 duration-700">
            <!-- Updated Background for Light/Dark Mode -->
            <div class="rounded-3xl p-6 md:p-8 shadow-xl relative overflow-hidden group border border-(--ui-border)">
                <!-- Background Decoration -->
                <div class="absolute -right-10 -top-10 w-40 h-40 bg-(--ui-brand) rounded-full blur-3xl group-hover:bg-(--ui-brand) transition-all duration-700"></div>
                <div class="absolute -left-10 -bottom-10 w-40 h-40 bg-(--ui-brand) rounded-full blur-3xl group-hover:bg-(--ui-brand) transition-all duration-700"></div>

                <div class="relative">
                    <div class="flex flex-col lg:flex-row items-center justify-between gap-8 mb-8 pb-8 border-b border-(--ui-border)">
                        <!-- Icon & Title -->
                        <div class="flex flex-col items-center lg:items-start text-center lg:text-left">
                            <div class="w-16 h-16 bg-(--ui-brand-soft) rounded-2xl flex items-center justify-center mb-4 border border-(--ui-brand-soft) shadow-inner">
                                <span class="material-icons text-(--ui-brand-text) text-3xl">construction</span>
                            </div>
                            <h2 class="text-2xl font-bold text-(--ui-text) mb-2">Bakım Modu Operasyon Merkezi</h2>
                            <div class="flex items-center gap-4">
                                <p class="text-(--ui-text-muted) max-w-sm text-sm">Sistemi genel olarak kilitleyebilir veya modülleri tek tek yönetebilirsiniz.</p>
                                <div class="bg-(--ui-brand-soft) px-3 py-1 rounded-full border border-(--ui-brand-soft)">
                                    <span class="text-[10px] font-bold text-(--ui-brand-text)">
                                        Data: {{ configs().length }} Ayar Yüklü
                                    </span>
                                </div>
                                <button (click)="loadConfigs()" class="p-1.5 hover:bg-(--ui-surface-muted) rounded-lg transition-colors text-(--ui-text-muted)" title="Verileri Yenile">
                                    <span class="material-icons text-sm">refresh</span>
                                </button>
                            </div>
                        </div>

                        <!-- GLOBAL MASTER SWITCH -->
                        <div *ngIf="globalConfig() as global" class="bg-(--ui-surface) p-6 rounded-3xl border border-(--ui-border) flex items-center gap-6 shadow-sm backdrop-blur-md">
                            <div class="text-right">
                                <div class="text-[10px] font-bold text-(--ui-danger) mb-1">Genel Sistem Kilidi</div>
                                <div class="text-xs text-(--ui-text-muted)">Tüm trafiği anında keser</div>
                            </div>
                            <label class="relative inline-flex items-center cursor-pointer scale-110">
                                <input type="checkbox" [checked]="global.value === 'true'" (change)="updateValue(global, $event)" [disabled]="!canManage()" class="sr-only peer">
                                <div class="w-14 h-7 bg-(--ui-surface-muted) peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-1 after:left-1 after:bg-(--ui-surface) after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-(--ui-danger)"></div>
                            </label>
                        </div>
                    </div>

                    <div class="flex flex-col lg:flex-row items-start gap-8">
                        <!-- Operations UI (Individual) -->
                        <div class="flex-1 w-full flex flex-col sm:flex-row items-center gap-4 bg-(--ui-surface)/50 p-4 rounded-2xl border border-(--ui-border) backdrop-blur-sm">
                            <!-- Service Selector -->
                            <div class="flex-1 w-full">
                                <label class="block text-[10px] font-bold text-(--ui-text-muted) mb-2 px-1">Bağımsız Modül Yönetimi</label>
                                <div class="relative group/select">
                                    <select 
                                        [(ngModel)]="selectedService" 
                                        (change)="onServiceChange()"
                                        class="w-full bg-(--ui-surface) border border-(--ui-border) rounded-xl px-4 py-3 text-(--ui-text) appearance-none focus:ring-2 focus:ring-(--ui-brand) outline-none transition-all cursor-pointer hover:border-(--ui-brand)">
                                        <option value="" disabled>Modül seçiniz...</option>
                                        <option *ngFor="let srv of discoveryServices()" [value]="srv">{{ srv | uppercase }} Modülü</option>
                                    </select>
                                    <span class="absolute right-4 top-1/2 -translate-y-1/2 text-(--ui-text-muted) pointer-events-none material-icons">expand_more</span>
                                </div>
                            </div>

                            <!-- Big Toggle Button -->
                            <div *ngIf="selectedServiceConfig() as config" class="sm:border-l border-(--ui-border) sm:pl-8 flex flex-col items-center animate-in zoom-in duration-300 min-w-[140px]">
                                <label class="block text-[10px] font-bold text-(--ui-text-muted) mb-3">Modül Durumu</label>
                                <label class="relative inline-flex items-center cursor-pointer">
                                    <input type="checkbox" [checked]="config.value === 'true'" (change)="updateValue(config, $event)" [disabled]="!canManage()" class="sr-only peer">
                                    <div class="w-16 h-8 bg-(--ui-surface-muted) peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-1 after:left-1 after:bg-(--ui-surface) after:rounded-full after:h-6 after:w-6 after:transition-all peer-checked: peer-checked:from-(--ui-warning) peer-checked:"></div>
                                </label>
                                <span class="mt-2 text-[10px] font-black tracking-tighter" [class]="config.value === 'true' ? 'text-(--ui-warning)' : 'text-(--ui-text-muted)'">
                                    {{ config.value === 'true' ? 'MODÜL KAPALI' : 'MODÜL AÇIK' }}
                                </span>
                            </div>

                            <!-- No Selection State -->
                            <div *ngIf="!selectedService" class="sm:border-l border-(--ui-border) sm:pl-8 flex flex-col items-center opacity-30 min-w-[140px]">
                                 <label class="block text-[10px] font-bold text-(--ui-text-muted) mb-3">Durum</label>
                                 <div class="w-16 h-8 bg-(--ui-border) rounded-full"></div>
                                 <span class="mt-2 text-[10px] font-bold text-(--ui-text-muted)">Seçim Yok</span>
                            </div>

                        <!-- Missing Config State: Now with AUTO-CREATE button -->
                        <div *ngIf="selectedService && !selectedServiceConfig()" class="sm:border-l border-(--ui-border) sm:pl-8 flex flex-col items-center min-w-[140px]">
                            <label class="block text-[10px] font-bold text-(--ui-warning) mb-2 px-1">Bağlantı Eksik</label>
                            @if (canManage()) { <button
                                (click)="quickCreateServiceConfig(selectedService)"
                                class="px-3 py-2 bg-(--ui-warning-soft) text-(--ui-warning) border border-(--ui-warning-soft) rounded-xl text-[10px] font-bold hover:bg-(--ui-warning) hover:text-white transition-all flex items-center gap-1 group">
                                <span class="material-icons text-sm group-hover:rotate-180 transition-all duration-500">add_circle</span> Ayar oluştur
                            </button> }
                            <span class="mt-2 text-[9px] text-(--ui-text-muted) text-center leading-tight">Bu modül henüz sisteme <br> tanımlanmamış.</span>
                        </div>
                    </div>

                        <!-- Summary: Who is in Maintenance? -->
                        <div class="w-full lg:w-72 bg-(--ui-surface-muted) rounded-2xl p-4 border border-(--ui-border) self-stretch flex flex-col">
                            <h3 class="text-xs font-bold text-(--ui-text) mb-4 flex items-center gap-2">
                                <span class="w-1.5 h-1.5 bg-(--ui-warning) rounded-full animate-pulse"></span>
                    Aktif Bakım Listesi
                            </h3>
                            <div class="flex flex-wrap gap-2 overflow-y-auto max-h-32 pr-2">
                                <div *ngIf="passiveServices().length === 0" class="text-[11px] text-(--ui-text-muted) italic py-2">
                                    Şu an tüm modüller aktif durumdadır.
                                </div>
                                <div *ngFor="let srv of passiveServices()" class="bg-(--ui-warning-soft) text-(--ui-warning) border border-(--ui-warning-soft) px-3 py-1.5 rounded-lg text-xs font-bold flex items-center gap-2 animate-in slide-in-from-right-2">
                                    <span class="material-icons text-sm">warning</span> {{ srv | uppercase }}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- MFA Policy Center -->
        <div *ngIf="mfaPolicies().length > 0" class="mb-10 rounded-3xl border border-(--ui-brand-soft) bg-(--ui-brand-soft) p-6 md:p-8 shadow-sm">
            <div class="flex flex-col lg:flex-row lg:items-end lg:justify-between gap-3 mb-6">
                <div>
                    <div class="flex items-center gap-3 mb-2">
                        <div class="w-10 h-10 rounded-xl bg-(--ui-brand) text-(--ui-brand-contrast) flex items-center justify-center shadow-lg">
                            <span class="material-icons">verified_user</span>
                        </div>
                        <h2 class="text-xl font-bold text-(--ui-text)">Yönetici MFA politikaları</h2>
                    </div>
                    <p class="text-sm text-(--ui-text-muted) max-w-3xl">
                        Yönetim işlemlerini kategori bazında koruyun. Varsayılan seçim yalnızca ekleme, güncelleme ve silme gibi değişikliklerde MFA ister.
                    </p>
                </div>
                <span class="text-xs text-(--ui-brand-text) bg-(--ui-surface)/70 border border-(--ui-brand-soft) rounded-full px-3 py-1.5">
                    Değişiklikler merkezi olarak tüm API'lere uygulanır
                </span>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
                <div *ngFor="let policy of mfaPolicies()" class="bg-(--ui-surface) rounded-2xl border border-(--ui-brand-soft) p-4">
                    <div class="flex items-start justify-between gap-3 mb-3">
                        <div>
                            <h3 class="font-semibold text-(--ui-text) text-sm">{{ policy.label }}</h3>
                            <p class="text-[11px] text-(--ui-text-muted) mt-1">{{ policy.description }}</p>
                        </div>
                        <span class="material-icons text-(--ui-brand-text) text-lg">admin_panel_settings</span>
                    </div>
                    <label class="block text-[10px] font-bold text-(--ui-text-muted) mb-1.5">MFA kapsamı</label>
                    <select
                        [value]="policy.config.value"
                        (change)="updateMfaPolicy(policy.config, $event)"
                        [disabled]="!canManage()"
                        class="w-full px-3 py-2 bg-(--ui-surface-muted) border border-(--ui-border) rounded-xl text-sm text-(--ui-text) focus:ring-2 focus:ring-(--ui-brand) outline-none">
                        <option value="required">Tüm işlemler</option>
                        <option value="mutations">Sadece değişiklikler</option>
                        <option value="disabled">MFA kapalı</option>
                    </select>
                </div>
            </div>
        </div>

        <!-- Groups Navigation -->
        <div class="flex justify-between items-center mb-6">
            <div class="flex gap-2 overflow-x-auto pb-2 scrollbar-hide">
                <button 
                    (click)="selectedGroup.set('All')" 
                    [class]="selectedGroup() === 'All' ? 'bg-(--ui-brand) text-white' : 'bg-(--ui-surface) text-(--ui-text-muted) hover:bg-(--ui-surface-muted)'"
                    class="px-4 py-1.5 rounded-full text-sm font-medium transition-all whitespace-nowrap shadow-sm border border-transparent">
                    Tüm Ayarlar
                </button>
                <button 
                    *ngFor="let group of groups()" 
                    (click)="selectedGroup.set(group)" 
                    [class]="selectedGroup() === group ? 'bg-(--ui-brand) text-white' : 'bg-(--ui-surface) text-(--ui-text-muted) hover:bg-(--ui-surface-muted)'"
                    class="px-4 py-1.5 rounded-full text-sm font-medium transition-all whitespace-nowrap shadow-sm border border-transparent">
                    {{ group }}
                </button>
            </div>
            
            <div class="hidden md:block relative w-64">
                <span class="absolute left-3 top-2.5 text-(--ui-text-muted) material-icons text-sm">search</span>
                <input 
                    type="text" 
                    [(ngModel)]="searchQuery" 
                    placeholder="Ayarlarda ara..."
                    class="w-full pl-10 pr-4 py-2 rounded-xl border border-(--ui-border) bg-(--ui-surface) focus:ring-2 focus:ring-(--ui-brand) outline-none text-xs transition-all shadow-sm">
            </div>
        </div>

        <!-- Config Grid (Updated to SHOW Maintenance items as well) -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4 gap-6">
            <div *ngFor="let config of displayConfigs()" class="bg-(--ui-surface) rounded-2xl border border-(--ui-border) shadow-sm hover:shadow-md transition-all group overflow-hidden">
                <!-- Card Header -->
                <div class="p-5 border-b border-(--ui-border) flex justify-between items-start bg-(--ui-surface-muted)">
                    <div class="flex-1 min-w-0 pr-4">
                        <div class="flex items-center gap-2 mb-1">
                            <span class="px-2 py-0.5 bg-(--ui-surface-muted) text-(--ui-text-muted) rounded text-[10px] font-bold">{{ config.group }}</span>
                            <span *ngIf="config.isPublic" class="px-2 py-0.5 bg-(--ui-success-soft) text-(--ui-success) rounded text-[10px] font-bold">Public</span>
                        </div>
                        <h3 class="font-bold text-(--ui-text) truncate text-sm" [title]="config.key">{{ config.key }}</h3>
                    </div>
                    @if (canManage()) { <button (click)="deleteConfig(config)" class="text-(--ui-text-muted) hover:text-(--ui-danger) transition-colors p-1 opacity-0 group-hover:opacity-100">
                        <span class="material-icons text-lg">delete_outline</span>
                    </button> }
                </div>

                <!-- Card Content -->
                <div class="p-5">
                    <p class="text-xs text-(--ui-text-muted) mb-4 line-clamp-2 min-h-[32px]">{{ config.description }}</p>
                    
                    <div class="space-y-3">
                        <label class="block text-[10px] font-bold text-(--ui-text-muted)">Ayar Değeri</label>
                        
                        <!-- Value Editor based on DataType -->
                        <ng-container [ngSwitch]="config.dataType">
                            <!-- Boolean -->
                            <div *ngSwitchCase="2" class="flex items-center">
                                <label class="relative inline-flex items-center cursor-pointer">
                                    <input type="checkbox" [checked]="config.value === 'true'" (change)="updateValue(config, $event)" [disabled]="!canManage()" class="sr-only peer">
                                    <div class="w-11 h-6 bg-(--ui-surface-muted) peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-(--ui-surface) after:border-(--ui-border) after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-(--ui-brand)"></div>
                                    <span class="ml-3 text-sm font-medium text-(--ui-text-muted)">{{ config.value === 'true' ? 'Açık' : 'Kapalı' }}</span>
                                </label>
                            </div>

                            <!-- Number -->
                            <div *ngSwitchCase="1">
                                <input type="number" [value]="config.value" (change)="updateValue(config, $event)" [disabled]="!canManage()" class="w-full px-3 py-1.5 bg-(--ui-surface-muted) border border-(--ui-border) rounded-lg text-sm focus:ring-1 focus:ring-(--ui-brand) outline-none transition-all">
                            </div>

                            <!-- Default / String -->
                            <div *ngSwitchCase="0">
                                <textarea [value]="config.value" (change)="updateValue(config, $event)" [disabled]="!canManage()" rows="2" class="w-full px-3 py-1.5 bg-(--ui-surface-muted) border border-(--ui-border) rounded-lg text-sm focus:ring-1 focus:ring-(--ui-brand) outline-none transition-all resize-none"></textarea>
                            </div>

                            <!-- Fallback -->
                            <div *ngSwitchDefault>
                                <input type="text" [value]="config.value" (change)="updateValue(config, $event)" [disabled]="!canManage()" class="w-full px-3 py-1.5 bg-(--ui-surface-muted) border border-(--ui-border) rounded-lg text-sm focus:ring-1 focus:ring-(--ui-brand) outline-none transition-all">
                            </div>
                        </ng-container>
                    </div>
                </div>
            </div>
        </div>

        <!-- Empty State -->
        <div *ngIf="filteredConfigs().length === 0" class="flex flex-col items-center justify-center py-20 bg-(--ui-surface) rounded-3xl border border-dashed border-(--ui-border) mt-6">
            <span class="material-icons text-5xl text-(--ui-text-muted) mb-4">settings_suggest</span>
            <p class="text-(--ui-text-muted)">Arama kriterlerine uygun ayar bulunamadı.</p>
        </div>
    </div>

    <!-- Create Modal Overlay -->
    <div *ngIf="showCreateModal && canManage()" class="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-(--ui-surface) rounded-3xl shadow-2xl w-full max-w-md overflow-hidden animate-in zoom-in duration-200">
            <div class="px-6 py-4 border-b border-(--ui-border) flex justify-between items-center bg-(--ui-surface-muted)">
                <h3 class="font-bold text-(--ui-text)">Yeni Ayar Ekle</h3>
                <button (click)="showCreateModal = false" class="text-(--ui-text-muted) hover:text-(--ui-text-muted)">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="p-6 space-y-4">
                <div>
                    <label class="block text-xs font-bold text-(--ui-text-muted) mb-1.5">Ayar Anahtarı (Key)</label>
                    <input type="text" [ngModel]="newConfig.key" (ngModelChange)="onKeyChange($event)" placeholder="System.MaintenanceMode" class="w-full px-4 py-2 bg-(--ui-surface-muted) border border-(--ui-border) rounded-xl text-sm focus:ring-2 focus:ring-(--ui-brand) outline-none transition-all">
                </div>
                <div>
                    <label class="block text-xs font-bold text-(--ui-text-muted) mb-1.5">Veri Tipi</label>
                    <select [(ngModel)]="newConfig.dataType" class="w-full px-4 py-2 bg-(--ui-surface-muted) border border-(--ui-border) rounded-xl text-sm focus:ring-2 focus:ring-(--ui-brand) outline-none transition-all">
                        <option [ngValue]="0">Metin (String)</option>
                        <option [ngValue]="1">Sayı (Number)</option>
                        <option [ngValue]="2">Aç/Kapat (Boolean)</option>
                        <option [ngValue]="3">JSON</option>
                    </select>
                </div>
                <div>
                    <label class="block text-xs font-bold text-(--ui-text-muted) mb-1.5">Başlangıç Değeri</label>
                    <input type="text" [(ngModel)]="newConfig.value" placeholder="true veya değer girin" class="w-full px-4 py-2 bg-(--ui-surface-muted) border border-(--ui-border) rounded-xl text-sm focus:ring-2 focus:ring-(--ui-brand) outline-none transition-all">
                </div>
                <div>
                    <label class="block text-xs font-bold text-(--ui-text-muted) mb-1.5">Grup</label>
                    <input type="text" [(ngModel)]="newConfig.group" placeholder="System, Auth, Mail" class="w-full px-4 py-2 bg-(--ui-surface-muted) border border-(--ui-border) rounded-xl text-sm focus:ring-2 focus:ring-(--ui-brand) outline-none transition-all">
                </div>
                <div>
                    <label class="block text-xs font-bold text-(--ui-text-muted) mb-1.5">Açıklama</label>
                    <textarea [(ngModel)]="newConfig.description" rows="2" class="w-full px-4 py-2 bg-(--ui-surface-muted) border border-(--ui-border) rounded-xl text-sm focus:ring-2 focus:ring-(--ui-brand) outline-none transition-all"></textarea>
                </div>
                <div class="flex items-center gap-2">
                    <input type="checkbox" [(ngModel)]="newConfig.isPublic" id="isPublic" class="w-4 h-4 text-(--ui-brand-text) bg-(--ui-surface-muted) border-(--ui-border) rounded focus:ring-(--ui-brand) focus:ring-2">
                    <label for="isPublic" class="text-sm font-medium text-(--ui-text)">Public (Frontend görsün mü?)</label>
                </div>
            </div>
            <div class="px-6 py-4 bg-(--ui-surface-muted) flex gap-2">
                <button (click)="showCreateModal = false" class="flex-1 px-4 py-2 text-(--ui-text-muted) font-medium text-sm hover:bg-(--ui-surface-muted) rounded-xl transition-all">İptal</button>
                <button (click)="createConfig()" class="flex-1 px-4 py-2 bg-(--ui-brand) text-(--ui-brand-contrast) font-medium text-sm rounded-xl hover:bg-(--ui-brand) shadow-lg transition-all">Kaydet</button>
            </div>
        </div>
    </div>
    `
})
export class ConfigurationsComponent implements OnInit {
    private configService = inject(ConfigurationService);
    private toaster = inject(ToasterService);
    private platformId = inject(PLATFORM_ID);
    private authService = inject(AuthService);
    canManage = computed(() => hasRequiredRole(this.authService.userProfile(), 'SystemAdmin'));

    configs = signal<Configuration[]>([]);
    discoveryServices = signal<string[]>([]);
    selectedService = '';
    selectedGroup = signal<string>('All');
    searchQuery = '';
    showCreateModal = false;

    newConfig = {
        key: '',
        value: '',
        description: '',
        dataType: ConfigurationDataType.String,
        group: 'General',
        isPublic: false
    };

    groups = computed(() => {
        const uniqueGroups = new Set(this.configs().map(c => c.group));
        return Array.from(uniqueGroups).sort();
    });

    // Helper for Global System Lock
    globalConfig = computed(() => {
        return this.configs().find(c => c.key.toLowerCase() === 'system.maintenancemode');
    });

    // Tracking currently passive (maintenance enabled) services
    passiveServices = computed(() => {
        return this.configs()
            .filter(c => c.value === 'true' && c.key.toLowerCase().startsWith('maintenance.'))
            .map(c => c.key.replace('maintenance.', ''));
    });

    // Helper for Maintenance Mode Center
    selectedServiceConfig = computed(() => {
        const services = this.configs();
        if (!this.selectedService || services.length === 0) return null;

        const lowerSelected = this.selectedService.trim().toLowerCase();
        const expectedKey = `maintenance.${lowerSelected}`;

        return services.find(c => {
            return c.key.toLowerCase() === expectedKey;
        });
    });

    // Don't show Maintenance configs in the main grid as they are in the Operation Center
    displayConfigs = computed(() =>
        this.filteredConfigs().filter(config => !config.key.toLowerCase().startsWith('security.mfa.'))
    );

    filteredConfigs = computed(() => {
        let items = this.configs().filter(config => !config.key.toLowerCase().startsWith('security.mfa.'));

        if (this.selectedGroup() !== 'All') {
            items = items.filter(c => c.group === this.selectedGroup());
        }

        if (this.searchQuery) {
            const query = this.searchQuery.toLowerCase();
            items = items.filter(c =>
                c.key.toLowerCase().includes(query) ||
                c.description.toLowerCase().includes(query)
            );
        }

        return items;
    });

    mfaPolicies = computed(() => {
        return MFA_CATEGORY_POLICIES
            .map(definition => {
                const config = this.configs().find(item =>
                    item.key.toLowerCase() === `security.mfa.${definition.category}`);
                return config ? { ...definition, config } : null;
            })
            .filter((policy): policy is MfaCategoryPolicyView => policy !== null);
    });

    ngOnInit() {
        if (!isPlatformBrowser(this.platformId)) {
            return;
        }

        this.loadConfigs();
        this.loadDiscoveryServices();
    }

    loadConfigs() {
        this.configService.getConfigurations().subscribe({
            next: (data) => this.configs.set(data),
            error: () => this.toaster.error('Ayarlar yüklenemedi.')
        });
    }

    loadDiscoveryServices() {
        this.configService.getDiscoveryServices().subscribe({
            next: (services) => this.discoveryServices.set(services),
            error: () => console.warn('Servis keşfi başarısız oldu.')
        });
    }

    onServiceChange() {
        // Simple log for tracking
    }

    quickCreateServiceConfig(serviceName: string) {
        const key = `maintenance.${serviceName.toLowerCase().trim()}`;
        this.configService.createConfiguration({
            key: key,
            value: 'false',
            description: `${serviceName.toUpperCase()} servisini bakım moduna alır.`,
            dataType: ConfigurationDataType.Boolean,
            group: 'Maintenance',
            isPublic: true
        }).subscribe({
            next: () => {
                this.toaster.success(`${serviceName} bakım ayarı oluşturuldu.`);
                this.loadConfigs();
            },
            error: () => this.toaster.error('Ayar oluşturulamadı.')
        });
    }

    updateValue(config: Configuration, event: any) {
        let value = '';
        if (config.dataType === ConfigurationDataType.Boolean) {
            value = event.target.checked ? 'true' : 'false';
        } else {
            value = event.target.value;
        }

        this.configService.updateConfiguration(config.key, { value }).subscribe({
            next: () => {
                this.toaster.success(`${config.key} güncellendi.`);
                this.loadConfigs();
            },
            error: () => this.toaster.error('Güncelleme başarısız.')
        });
    }

    updateMfaPolicy(config: Configuration, event: Event) {
        const value = (event.target as HTMLSelectElement | null)?.value?.toLowerCase();
        if (!value || !MFA_POLICY_MODES.has(value)) {
            this.toaster.error('Geçersiz MFA kapsamı seçildi.');
            return;
        }

        const category = config.key.replace(/^security\.mfa\./i, '').trim();
        this.configService.updateMfaPolicy(category, { value }).subscribe({
            next: () => {
                this.toaster.success(`${config.key.replace('security.mfa.', '')} MFA politikası güncellendi.`);
                this.loadConfigs();
            },
            error: () => this.toaster.error('MFA politikası güncellenemedi.')
        });
    }

    createConfig() {
        if (!this.newConfig.key || !this.newConfig.value) {
            this.toaster.warning('Lütfen zorunlu alanları doldurun.');
            return;
        }

        this.configService.createConfiguration(this.newConfig as any).subscribe({
            next: () => {
                this.toaster.success('Yeni ayar oluşturuldu.');
                this.showCreateModal = false;
                this.loadConfigs();
                this.resetNewConfig();
            },
            error: () => this.toaster.error('Ayar oluşturulamadı.')
        });
    }

    deleteConfig(config: Configuration) {
        this.toaster.confirm(`"${config.key}" ayarını silmek istediğinize emin misiniz?`, { title: 'Ayarı sil' }).then(confirmed => {
            if (confirmed) {
                this.configService.deleteConfiguration(config.key).subscribe({
                    next: () => {
                        this.toaster.success('Ayar silindi.');
                        this.loadConfigs();
                    },
                    error: () => this.toaster.error('Silme işlemi başarısız.')
                });
            }
        });
    }

    refreshRedisCache() {
        this.configService.refreshCache().subscribe({
            next: () => this.toaster.success('Redis cache başarıyla yenilendi.'),
            error: () => this.toaster.error('Cache yenilenemedi.')
        });
    }

    onKeyChange(value: string) {
        this.newConfig.key = value;
        const normalizedKey = value.toLowerCase().trim();

        const existing = this.configs().find(c => c.key.toLowerCase() === normalizedKey);

        if (existing) {
            // Auto-fill form with existing values
            this.newConfig.value = existing.value;
            this.newConfig.description = existing.description;
            this.newConfig.group = existing.group;
            this.newConfig.dataType = existing.dataType;
            this.newConfig.isPublic = existing.isPublic;

            this.toaster.info('Mevcut ayar bilgileri yüklendi.');
        }
    }

    private resetNewConfig() {
        this.newConfig = {
            key: '',
            value: '',
            description: '',
            dataType: ConfigurationDataType.String,
            group: 'General',
            isPublic: false
        };
    }
}
