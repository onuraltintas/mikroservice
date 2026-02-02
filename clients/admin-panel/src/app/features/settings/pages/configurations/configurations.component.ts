import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfigurationService, Configuration, ConfigurationDataType } from '../../../../core/services/settings/configuration.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
    selector: 'app-configurations',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="p-4 md:p-6 min-h-full">
        <!-- Header -->
        <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8">
            <div>
                <h1 class="text-xl md:text-2xl font-bold text-gray-900 dark:text-white">Sistem Operasyon Merkezi</h1>
                <p class="text-gray-500 dark:text-gray-400 text-sm">Sistem parametrelerini ve modül durumlarını dinamik olarak yönetin.</p>
            </div>
            <div class="flex gap-2">
                <button (click)="refreshRedisCache()" class="px-4 py-2 bg-indigo-50 text-indigo-700 dark:bg-indigo-900/30 dark:text-indigo-300 rounded-lg hover:bg-indigo-100 transition-all flex items-center gap-2 text-sm font-medium">
                    <span class="material-icons text-sm">cached</span> Önbelleği (Redis) Temizle
                </button>
                <button (click)="showCreateModal = true" class="px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 transition-all flex items-center gap-2 text-sm font-medium shadow-sm">
                    <span class="material-icons text-sm">add</span> Yeni Ayar
                </button>
            </div>
        </div>

        <!-- NEW: Maintenance Mode Operations Center -->
        <div class="mb-10 animate-in fade-in slide-in-from-top-4 duration-700">
            <!-- Updated Background for Light/Dark Mode -->
            <div class="bg-gradient-to-br from-white to-gray-50 dark:from-indigo-950 dark:to-gray-900 rounded-3xl p-6 md:p-8 shadow-xl relative overflow-hidden group border border-gray-100 dark:border-gray-800">
                <!-- Background Decoration -->
                <div class="absolute -right-10 -top-10 w-40 h-40 bg-indigo-500/5 dark:bg-indigo-500/10 rounded-full blur-3xl group-hover:bg-indigo-500/10 dark:group-hover:bg-indigo-500/20 transition-all duration-700"></div>
                <div class="absolute -left-10 -bottom-10 w-40 h-40 bg-blue-500/5 dark:bg-blue-500/10 rounded-full blur-3xl group-hover:bg-blue-500/10 dark:group-hover:bg-blue-500/20 transition-all duration-700"></div>

                <div class="relative">
                    <div class="flex flex-col lg:flex-row items-center justify-between gap-8 mb-8 pb-8 border-b border-gray-200 dark:border-white/5">
                        <!-- Icon & Title -->
                        <div class="flex flex-col items-center lg:items-start text-center lg:text-left">
                            <div class="w-16 h-16 bg-indigo-100 dark:bg-indigo-500/20 rounded-2xl flex items-center justify-center mb-4 border border-indigo-200 dark:border-indigo-500/30 shadow-inner">
                                <span class="material-icons text-indigo-600 dark:text-indigo-400 text-3xl">construction</span>
                            </div>
                            <h2 class="text-2xl font-bold text-gray-900 dark:text-white mb-2">Bakım Modu Operasyon Merkezi</h2>
                            <div class="flex items-center gap-4">
                                <p class="text-gray-500 dark:text-indigo-200/60 max-w-sm text-sm">Sistemi genel olarak kilitleyebilir veya modülleri tek tek yönetebilirsiniz.</p>
                                <div class="bg-indigo-50 dark:bg-indigo-500/20 px-3 py-1 rounded-full border border-indigo-100 dark:border-indigo-500/30">
                                    <span class="text-[10px] font-bold text-indigo-700 dark:text-indigo-300 uppercase tracking-widest">
                                        Data: {{ configs().length }} Ayar Yüklü
                                    </span>
                                </div>
                                <button (click)="loadConfigs()" class="p-1.5 hover:bg-gray-100 dark:hover:bg-white/10 rounded-lg transition-colors text-gray-400 dark:text-indigo-300" title="Verileri Yenile">
                                    <span class="material-icons text-sm">refresh</span>
                                </button>
                            </div>
                        </div>

                        <!-- GLOBAL MASTER SWITCH -->
                        <div *ngIf="globalConfig() as global" class="bg-white dark:bg-white/5 p-6 rounded-3xl border border-gray-200 dark:border-white/10 flex items-center gap-6 shadow-sm dark:shadow-2xl backdrop-blur-md">
                            <div class="text-right">
                                <div class="text-[10px] font-bold text-red-500 dark:text-red-400 uppercase tracking-widest mb-1">Genel Sistem Kilidi</div>
                                <div class="text-xs text-gray-500 dark:text-indigo-200/60">Tüm trafiği anında keser</div>
                            </div>
                            <label class="relative inline-flex items-center cursor-pointer scale-110">
                                <input type="checkbox" [checked]="global.value === 'true'" (change)="updateValue(global, $event)" class="sr-only peer">
                                <div class="w-14 h-7 bg-gray-200 dark:bg-gray-700 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-1 after:left-1 after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-red-600"></div>
                            </label>
                        </div>
                    </div>

                    <div class="flex flex-col lg:flex-row items-start gap-8">
                        <!-- Operations UI (Individual) -->
                        <div class="flex-1 w-full flex flex-col sm:flex-row items-center gap-4 bg-white/50 dark:bg-white/5 p-4 rounded-2xl border border-gray-200 dark:border-white/10 backdrop-blur-sm">
                            <!-- Service Selector -->
                            <div class="flex-1 w-full">
                                <label class="block text-[10px] font-bold text-gray-500 dark:text-indigo-300 uppercase tracking-widest mb-2 px-1">Bağımsız Modül Yönetimi</label>
                                <div class="relative group/select">
                                    <select 
                                        [(ngModel)]="selectedService" 
                                        (change)="onServiceChange()"
                                        class="w-full bg-white dark:bg-gray-800/80 border border-gray-300 dark:border-gray-600 rounded-xl px-4 py-3 text-gray-900 dark:text-white appearance-none focus:ring-2 focus:ring-indigo-500 outline-none transition-all cursor-pointer hover:border-indigo-400">
                                        <option value="" disabled>Modül seçiniz...</option>
                                        <option *ngFor="let srv of discoveryServices()" [value]="srv">{{ srv | uppercase }} Modülü</option>
                                    </select>
                                    <span class="absolute right-4 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none material-icons">expand_more</span>
                                </div>
                            </div>

                            <!-- Big Toggle Button -->
                            <div *ngIf="selectedServiceConfig() as config" class="sm:border-l border-gray-200 dark:border-white/10 sm:pl-8 flex flex-col items-center animate-in zoom-in duration-300 min-w-[140px]">
                                <label class="block text-[10px] font-bold text-gray-500 dark:text-indigo-300 uppercase tracking-widest mb-3">Modül Durumu</label>
                                <label class="relative inline-flex items-center cursor-pointer">
                                    <input type="checkbox" [checked]="config.value === 'true'" (change)="updateValue(config, $event)" class="sr-only peer">
                                    <div class="w-16 h-8 bg-gray-200 dark:bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-1 after:left-1 after:bg-white after:rounded-full after:h-6 after:w-6 after:transition-all peer-checked:bg-gradient-to-r peer-checked:from-amber-500 peer-checked:to-orange-600"></div>
                                </label>
                                <span class="mt-2 text-[10px] font-black tracking-tighter" [class]="config.value === 'true' ? 'text-amber-600 dark:text-amber-400' : 'text-gray-400'">
                                    {{ config.value === 'true' ? 'MODÜL KAPALI' : 'MODÜL AÇIK' }}
                                </span>
                            </div>

                            <!-- No Selection State -->
                            <div *ngIf="!selectedService" class="sm:border-l border-gray-200 dark:border-white/10 sm:pl-8 flex flex-col items-center opacity-30 min-w-[140px]">
                                 <label class="block text-[10px] font-bold text-gray-500 dark:text-indigo-300 uppercase tracking-widest mb-3">Durum</label>
                                 <div class="w-16 h-8 bg-gray-300 dark:bg-gray-700 rounded-full"></div>
                                 <span class="mt-2 text-[10px] font-bold text-gray-500 uppercase">Seçim Yok</span>
                            </div>

                        <!-- Missing Config State: Now with AUTO-CREATE button -->
                        <div *ngIf="selectedService && !selectedServiceConfig()" class="sm:border-l border-gray-200 dark:border-white/10 sm:pl-8 flex flex-col items-center min-w-[140px]">
                            <label class="block text-[10px] font-bold text-amber-600 dark:text-amber-300 uppercase tracking-widest mb-2 px-1">Bağlantı Eksik</label>
                            <button 
                                (click)="quickCreateServiceConfig(selectedService)"
                                class="px-3 py-2 bg-amber-50 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400 border border-amber-200 dark:border-amber-500/30 rounded-xl text-[10px] font-bold hover:bg-amber-500 hover:text-white transition-all flex items-center gap-1 group">
                                <span class="material-icons text-sm group-hover:rotate-180 transition-all duration-500">add_circle</span> AYAR OLUŞTUR
                            </button>
                            <span class="mt-2 text-[9px] text-gray-500 text-center leading-tight">Bu modül henüz sisteme <br> tanımlanmamış.</span>
                        </div>
                    </div>

                        <!-- Summary: Who is in Maintenance? -->
                        <div class="w-full lg:w-72 bg-gray-50 dark:bg-black/20 rounded-2xl p-4 border border-gray-200 dark:border-white/5 self-stretch flex flex-col">
                            <h3 class="text-xs font-bold text-gray-700 dark:text-indigo-300 uppercase tracking-widest mb-4 flex items-center gap-2">
                                <span class="w-1.5 h-1.5 bg-amber-500 rounded-full animate-pulse"></span>
                    Aktif Bakım Listesi
                            </h3>
                            <div class="flex flex-wrap gap-2 overflow-y-auto max-h-32 pr-2">
                                <div *ngIf="passiveServices().length === 0" class="text-[11px] text-gray-500 italic py-2">
                                    Şu an tüm modüller aktif durumdadır.
                                </div>
                                <div *ngFor="let srv of passiveServices()" class="bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-500/20 px-3 py-1.5 rounded-lg text-xs font-bold flex items-center gap-2 animate-in slide-in-from-right-2">
                                    <span class="material-icons text-sm">warning</span> {{ srv | uppercase }}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Groups Navigation -->
        <div class="flex justify-between items-center mb-6">
            <div class="flex gap-2 overflow-x-auto pb-2 scrollbar-hide">
                <button 
                    (click)="selectedGroup.set('All')" 
                    [class]="selectedGroup() === 'All' ? 'bg-indigo-600 text-white' : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-400 hover:bg-gray-50'"
                    class="px-4 py-1.5 rounded-full text-sm font-medium transition-all whitespace-nowrap shadow-sm border border-transparent">
                    Tüm Ayarlar
                </button>
                <button 
                    *ngFor="let group of groups()" 
                    (click)="selectedGroup.set(group)" 
                    [class]="selectedGroup() === group ? 'bg-indigo-600 text-white' : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-400 hover:bg-gray-50'"
                    class="px-4 py-1.5 rounded-full text-sm font-medium transition-all whitespace-nowrap shadow-sm border border-transparent">
                    {{ group }}
                </button>
            </div>
            
            <div class="hidden md:block relative w-64">
                <span class="absolute left-3 top-2.5 text-gray-400 material-icons text-sm">search</span>
                <input 
                    type="text" 
                    [(ngModel)]="searchQuery" 
                    placeholder="Ayarlarda ara..."
                    class="w-full pl-10 pr-4 py-2 rounded-xl border border-gray-100 dark:border-gray-700 bg-white dark:bg-gray-800 focus:ring-2 focus:ring-indigo-500 outline-none text-xs transition-all shadow-sm">
            </div>
        </div>

        <!-- Config Grid (Updated to SHOW Maintenance items as well) -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4 gap-6">
            <div *ngFor="let config of displayConfigs()" class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 shadow-sm hover:shadow-md transition-all group overflow-hidden">
                <!-- Card Header -->
                <div class="p-5 border-b border-gray-50 dark:border-gray-700/50 flex justify-between items-start bg-gray-50/50 dark:bg-gray-800/50">
                    <div class="flex-1 min-w-0 pr-4">
                        <div class="flex items-center gap-2 mb-1">
                            <span class="px-2 py-0.5 bg-gray-200 dark:bg-gray-700 text-gray-600 dark:text-gray-400 rounded text-[10px] font-bold uppercase tracking-wider">{{ config.group }}</span>
                            <span *ngIf="config.isPublic" class="px-2 py-0.5 bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400 rounded text-[10px] font-bold uppercase tracking-wider">Public</span>
                        </div>
                        <h3 class="font-bold text-gray-900 dark:text-white truncate text-sm" [title]="config.key">{{ config.key }}</h3>
                    </div>
                    <button (click)="deleteConfig(config)" class="text-gray-400 hover:text-red-500 transition-colors p-1 opacity-0 group-hover:opacity-100">
                        <span class="material-icons text-lg">delete_outline</span>
                    </button>
                </div>

                <!-- Card Content -->
                <div class="p-5">
                    <p class="text-xs text-gray-500 dark:text-gray-400 mb-4 line-clamp-2 min-h-[32px]">{{ config.description }}</p>
                    
                    <div class="space-y-3">
                        <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest">Ayar Değeri</label>
                        
                        <!-- Value Editor based on DataType -->
                        <ng-container [ngSwitch]="config.dataType">
                            <!-- Boolean -->
                            <div *ngSwitchCase="2" class="flex items-center">
                                <label class="relative inline-flex items-center cursor-pointer">
                                    <input type="checkbox" [checked]="config.value === 'true'" (change)="updateValue(config, $event)" class="sr-only peer">
                                    <div class="w-11 h-6 bg-gray-200 peer-focus:outline-none rounded-full peer dark:bg-gray-700 peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all dark:border-gray-600 peer-checked:bg-indigo-600"></div>
                                    <span class="ml-3 text-sm font-medium text-gray-600 dark:text-gray-300">{{ config.value === 'true' ? 'Açık' : 'Kapalı' }}</span>
                                </label>
                            </div>

                            <!-- Number -->
                            <div *ngSwitchCase="1">
                                <input type="number" [value]="config.value" (change)="updateValue(config, $event)" class="w-full px-3 py-1.5 bg-gray-50 dark:bg-gray-900/50 border border-gray-200 dark:border-gray-700 rounded-lg text-sm focus:ring-1 focus:ring-indigo-500 outline-none transition-all">
                            </div>

                            <!-- Secret -->
                            <div *ngSwitchCase="4" class="relative">
                                <input [type]="showSecrets.has(config.id) ? 'text' : 'password'" [value]="config.value" (change)="updateValue(config, $event)" class="w-full pl-3 pr-10 py-1.5 bg-gray-50 dark:bg-gray-900/50 border border-gray-200 dark:border-gray-700 rounded-lg text-sm font-mono overflow-hidden truncate">
                                <button (click)="toggleSecret(config.id)" class="absolute right-2 top-1.5 text-gray-400 hover:text-indigo-500">
                                    <span class="material-icons text-sm">{{ showSecrets.has(config.id) ? 'visibility_off' : 'visibility' }}</span>
                                </button>
                            </div>

                            <!-- Default / String -->
                            <div *ngSwitchCase="0">
                                <textarea [value]="config.value" (change)="updateValue(config, $event)" rows="2" class="w-full px-3 py-1.5 bg-gray-50 dark:bg-gray-900/50 border border-gray-200 dark:border-gray-700 rounded-lg text-sm focus:ring-1 focus:ring-indigo-500 outline-none transition-all resize-none"></textarea>
                            </div>

                            <!-- Fallback -->
                            <div *ngSwitchDefault>
                                <input type="text" [value]="config.value" (change)="updateValue(config, $event)" class="w-full px-3 py-1.5 bg-gray-50 dark:bg-gray-900/50 border border-gray-200 dark:border-gray-700 rounded-lg text-sm focus:ring-1 focus:ring-indigo-500 outline-none transition-all">
                            </div>
                        </ng-container>
                    </div>
                </div>
            </div>
        </div>

        <!-- Empty State -->
        <div *ngIf="filteredConfigs().length === 0" class="flex flex-col items-center justify-center py-20 bg-white dark:bg-gray-800 rounded-3xl border border-dashed border-gray-200 dark:border-gray-700 mt-6">
            <span class="material-icons text-5xl text-gray-300 dark:text-gray-700 mb-4">settings_suggest</span>
            <p class="text-gray-500 dark:text-gray-400">Arama kriterlerine uygun ayar bulunamadı.</p>
        </div>
    </div>

    <!-- Create Modal Overlay -->
    <div *ngIf="showCreateModal" class="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-white dark:bg-gray-800 rounded-3xl shadow-2xl w-full max-w-md overflow-hidden animate-in zoom-in duration-200">
            <div class="px-6 py-4 border-b border-gray-50 dark:border-gray-700 flex justify-between items-center bg-gray-50/50 dark:bg-gray-800/50">
                <h3 class="font-bold text-gray-900 dark:text-white">Yeni Ayar Ekle</h3>
                <button (click)="showCreateModal = false" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200">
                    <span class="material-icons">close</span>
                </button>
            </div>
            <div class="p-6 space-y-4">
                <div>
                    <label class="block text-xs font-bold text-gray-400 uppercase tracking-widest mb-1.5">Ayar Anahtarı (Key)</label>
                    <input type="text" [ngModel]="newConfig.key" (ngModelChange)="onKeyChange($event)" placeholder="System.MaintenanceMode" class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-xl text-sm focus:ring-2 focus:ring-indigo-500 outline-none transition-all">
                </div>
                <div>
                    <label class="block text-xs font-bold text-gray-400 uppercase tracking-widest mb-1.5">Veri Tipi</label>
                    <select [(ngModel)]="newConfig.dataType" class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-xl text-sm focus:ring-2 focus:ring-indigo-500 outline-none transition-all">
                        <option [ngValue]="0">Metin (String)</option>
                        <option [ngValue]="1">Sayı (Number)</option>
                        <option [ngValue]="2">Aç/Kapat (Boolean)</option>
                        <option [ngValue]="3">JSON</option>
                        <option [ngValue]="4">Gizli (Secret/API Key)</option>
                    </select>
                </div>
                <div>
                    <label class="block text-xs font-bold text-gray-400 uppercase tracking-widest mb-1.5">Başlangıç Değeri</label>
                    <input type="text" [(ngModel)]="newConfig.value" placeholder="true veya değer girin" class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-xl text-sm focus:ring-2 focus:ring-indigo-500 outline-none transition-all">
                </div>
                <div>
                    <label class="block text-xs font-bold text-gray-400 uppercase tracking-widest mb-1.5">Grup</label>
                    <input type="text" [(ngModel)]="newConfig.group" placeholder="System, Auth, Mail" class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-xl text-sm focus:ring-2 focus:ring-indigo-500 outline-none transition-all">
                </div>
                <div>
                    <label class="block text-xs font-bold text-gray-400 uppercase tracking-widest mb-1.5">Açıklama</label>
                    <textarea [(ngModel)]="newConfig.description" rows="2" class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-xl text-sm focus:ring-2 focus:ring-indigo-500 outline-none transition-all"></textarea>
                </div>
                <div class="flex items-center gap-2">
                    <input type="checkbox" [(ngModel)]="newConfig.isPublic" id="isPublic" class="w-4 h-4 text-indigo-600 bg-gray-100 border-gray-300 rounded focus:ring-indigo-500 dark:focus:ring-indigo-600 dark:ring-offset-gray-800 focus:ring-2 dark:bg-gray-700 dark:border-gray-600">
                    <label for="isPublic" class="text-sm font-medium text-gray-700 dark:text-gray-300">Public (Frontend görsün mü?)</label>
                </div>
            </div>
            <div class="px-6 py-4 bg-gray-50 dark:bg-gray-800/50 flex gap-2">
                <button (click)="showCreateModal = false" class="flex-1 px-4 py-2 text-gray-600 dark:text-gray-400 font-medium text-sm hover:bg-gray-100 dark:hover:bg-gray-700 rounded-xl transition-all">İptal</button>
                <button (click)="createConfig()" class="flex-1 px-4 py-2 bg-indigo-600 text-white font-medium text-sm rounded-xl hover:bg-indigo-700 shadow-lg shadow-indigo-600/20 transition-all">Kaydet</button>
            </div>
        </div>
    </div>
    `
})
export class ConfigurationsComponent implements OnInit {
    private configService = inject(ConfigurationService);
    private toaster = inject(ToasterService);

    configs = signal<Configuration[]>([]);
    discoveryServices = signal<string[]>([]);
    selectedService = '';
    selectedGroup = signal<string>('All');
    searchQuery = '';
    showCreateModal = false;
    showSecrets = new Set<string>();

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
    displayConfigs = computed(() => {
        return this.filteredConfigs(); // ARTIK maintenance ayarları da gösteriliyor!
    });

    filteredConfigs = computed(() => {
        let items = this.configs();

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

    ngOnInit() {
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
            error: (err) => this.toaster.error('Ayar oluşturulamadı.')
        });
    }

    deleteConfig(config: Configuration) {
        this.toaster.confirm('Ayarı Sil', `"${config.key}" ayarını silmek istediğinize emin misiniz?`).then(confirmed => {
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

    toggleSecret(id: string) {
        if (this.showSecrets.has(id)) {
            this.showSecrets.delete(id);
        } else {
            this.showSecrets.add(id);
        }
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
