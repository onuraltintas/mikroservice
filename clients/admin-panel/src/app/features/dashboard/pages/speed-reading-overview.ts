import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { AuthService } from '../../../core/auth/auth.service';
import {
  SpeedReadingAdminService,
  SpeedReadingCapabilities,
  SpeedReadingExercise,
  SpeedReadingExerciseRequest,
  SpeedReadingExerciseType,
  SpeedReadingExerciseTypeRequest,
  SpeedReadingReadingText,
  SpeedReadingReadingTextRequest
} from '../../../core/services/speed-reading-admin.service';

@Component({
  selector: 'app-speed-reading-overview',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Hızlı Okuma</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">Bağımsız hızlı okuma servisinin yönetim görünümü.</p>
        </div>
        <button type="button" (click)="load()" [disabled]="loading()"
          class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">
          {{ loading() ? 'Yükleniyor…' : 'Yenile' }}
        </button>
      </div>

      @if (error()) {
        <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div>
      }

      @if (loading() && !capabilities()) {
        <div class="rounded-xl bg-white p-6 shadow-sm dark:bg-gray-800">Servis bilgisi yükleniyor…</div>
      } @else if (capabilities(); as data) {
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Çalışma modu</p>
            <p class="mt-2 text-2xl font-bold text-gray-900 dark:text-white">{{ data.mode }}</p>
          </div>
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Koçluk entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-emerald-600]="data.coachingIntegrationEnabled" [class.text-gray-500]="!data.coachingIntegrationEnabled">
              {{ data.coachingIntegrationEnabled ? 'Açık' : 'Kapalı' }}
            </p>
          </div>
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Bildirim entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-emerald-600]="data.notificationIntegrationEnabled" [class.text-gray-500]="!data.notificationIntegrationEnabled">
              {{ data.notificationIntegrationEnabled ? 'Açık' : 'Kapalı' }}
            </p>
          </div>
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Abonelik entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-emerald-600]="data.subscriptionIntegrationEnabled" [class.text-gray-500]="!data.subscriptionIntegrationEnabled">
              {{ data.subscriptionIntegrationEnabled ? 'Açık' : 'Kapalı' }}
            </p>
          </div>
        </div>

        <div class="rounded-xl border border-indigo-100 bg-indigo-50 p-5 text-sm text-indigo-900 dark:border-indigo-900/50 dark:bg-indigo-950/30 dark:text-indigo-200">
          Mevcut hızlı okuma veritabanı korunarak içerik kataloğu bu servisten okunuyor. İçerik değişiklikleri yalnızca ContentManage yetkisine sahip yöneticilere açıktır ve her mutation idempotency ile audit edilir.
        </div>

        <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <div class="flex items-center justify-between gap-3">
              <div>
                <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Egzersiz türleri</h2>
              <p class="text-sm text-gray-500 dark:text-gray-400">Katalog motorlarının ve görünür adlarının yönetimi.</p>
              </div>
              <div class="flex items-center gap-2">
                <span class="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-600 dark:bg-gray-700 dark:text-gray-300">
                  {{ exerciseTypes().length }} tür
                </span>
                @if (canManageContent()) {
                  <button type="button" (click)="startCreate()"
                    class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni tür</button>
                }
              </div>
          </div>

          @if (canManageContent() && editingId !== null) {
            <form class="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-indigo-200 bg-indigo-50/50 p-4 sm:grid-cols-2"
              (ngSubmit)="saveExerciseType()">
              <label class="text-sm text-gray-700 dark:text-gray-200">Teknik ad
                <input name="name" [(ngModel)]="draft.name" required maxlength="100"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Görünen ad
                <input name="displayName" [(ngModel)]="draft.displayName" required maxlength="150"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Motor tipi
                <input name="engineType" [(ngModel)]="draft.engineType" required maxlength="100"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Renk (#RRGGBB)
                <input name="colorCode" [(ngModel)]="draft.colorCode" maxlength="7" placeholder="#2563eb"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">İkon adı
                <input name="iconName" [(ngModel)]="draft.iconName" maxlength="100" placeholder="grid"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Sıra
                <input type="number" name="sortOrder" [(ngModel)]="draft.sortOrder" min="0" max="10000"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">Açıklama
                <textarea name="description" [(ngModel)]="draft.description" maxlength="1000" rows="2"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800"></textarea>
              </label>
              <label class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-200">
                <input type="checkbox" name="isActive" [(ngModel)]="draft.isActive" /> Aktif
              </label>
              <div class="flex justify-end gap-2 sm:col-span-2">
                <button type="button" (click)="cancelEdit()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Vazgeç</button>
                <button type="submit" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50">
                  {{ saving() ? 'Kaydediliyor…' : (editingId === 'new' ? 'Oluştur' : 'Kaydet') }}
                </button>
              </div>
            </form>
          }

          @if (exerciseTypes().length) {
            <div class="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              @for (type of exerciseTypes(); track type.id) {
                <article class="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
                  <div class="flex items-center gap-3">
                    <span class="h-3 w-3 rounded-full" [style.backgroundColor]="type.colorCode"></span>
                    <h3 class="font-medium text-gray-900 dark:text-white">{{ type.displayName || type.name }}</h3>
                  </div>
                  <p class="mt-2 text-xs text-gray-500 dark:text-gray-400">{{ type.engineType || 'Genel egzersiz' }}</p>
                  <p class="mt-2 line-clamp-2 text-sm text-gray-600 dark:text-gray-300">{{ type.description }}</p>
                  @if (canManageContent()) {
                    <div class="mt-3 flex gap-2">
                      <button type="button" (click)="startEdit(type)" class="rounded border border-gray-300 px-2 py-1 text-xs">Düzenle</button>
                      <button type="button" (click)="deleteExerciseType(type)" class="rounded border border-red-300 px-2 py-1 text-xs text-red-700">Sil</button>
                    </div>
                  }
                </article>
              }
            </div>
          } @else if (!loading()) {
            <p class="mt-4 text-sm text-gray-500 dark:text-gray-400">Katalogda gösterilecek aktif egzersiz türü bulunamadı.</p>
          }
        </div>

        <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="flex items-center justify-between gap-3">
            <div>
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Egzersizler</h2>
              <p class="text-sm text-gray-500 dark:text-gray-400">Egzersiz başlığı, zorluk seviyesi ve motor konfigürasyonu.</p>
            </div>
            @if (canManageContent()) {
              <button type="button" (click)="startCreateExercise()"
                class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni egzersiz</button>
            }
          </div>

          @if (canManageContent() && exerciseEditingId !== null) {
            <form class="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-indigo-200 bg-indigo-50/50 p-4 sm:grid-cols-2"
              (ngSubmit)="saveExercise()">
              <label class="text-sm text-gray-700 dark:text-gray-200">Başlık
                <input name="exerciseTitle" [(ngModel)]="exerciseDraft.title" required maxlength="200"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Egzersiz türü
                <select name="exerciseTypeId" [(ngModel)]="exerciseDraft.exerciseTypeId" required
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800">
                  <option value="">Tür seçin</option>
                  @for (type of exerciseTypes(); track type.id) {
                    <option [value]="type.id">{{ type.displayName || type.name }}</option>
                  }
                </select>
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Zorluk (0-10)
                <input type="number" name="difficultyLevel" [(ngModel)]="exerciseDraft.difficultyLevel" min="0" max="10"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">Açıklama
                <textarea name="exerciseDescription" [(ngModel)]="exerciseDraft.description" maxlength="2000" rows="2"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800"></textarea>
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">Konfigürasyon JSON
                <textarea name="configurationJson" [(ngModel)]="exerciseDraft.configurationJson" required rows="4"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 font-mono text-xs dark:border-gray-600 dark:bg-gray-800"></textarea>
              </label>
              <div class="flex justify-end gap-2 sm:col-span-2">
                <button type="button" (click)="cancelExerciseEdit()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Vazgeç</button>
                <button type="submit" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50">
                  {{ saving() ? 'Kaydediliyor…' : (exerciseEditingId === 'new' ? 'Oluştur' : 'Kaydet') }}
                </button>
              </div>
            </form>
          }

          @if (exercises().length) {
            <div class="mt-4 grid grid-cols-1 gap-3 lg:grid-cols-2">
              @for (exercise of exercises(); track exercise.id) {
                <article class="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
                  <div class="flex items-start justify-between gap-3">
                    <div>
                      <h3 class="font-medium text-gray-900 dark:text-white">{{ exercise.title }}</h3>
                      <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">{{ exercise.exerciseTypeName }} · Zorluk {{ exercise.difficultyLevel }}</p>
                    </div>
                    @if (canManageContent()) {
                      <div class="flex shrink-0 gap-2">
                        <button type="button" (click)="startEditExercise(exercise)" class="rounded border border-gray-300 px-2 py-1 text-xs">Düzenle</button>
                        <button type="button" (click)="deleteExercise(exercise)" class="rounded border border-red-300 px-2 py-1 text-xs text-red-700">Sil</button>
                      </div>
                    }
                  </div>
                  <p class="mt-2 line-clamp-2 text-sm text-gray-600 dark:text-gray-300">{{ exercise.description }}</p>
                </article>
              }
            </div>
          } @else if (!loading()) {
            <p class="mt-4 text-sm text-gray-500 dark:text-gray-400">Katalogda gösterilecek egzersiz bulunamadı.</p>
          }
        </div>

        <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="flex items-center justify-between gap-3">
            <div>
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Okuma metinleri</h2>
              <p class="text-sm text-gray-500 dark:text-gray-400">Metin, seviye, dil ve bağlı egzersiz içeriklerini yönetin.</p>
            </div>
            <div class="flex items-center gap-2">
              <span class="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-600 dark:bg-gray-700 dark:text-gray-300">
                {{ readingTexts().length }} metin
              </span>
              @if (canManageContent()) {
                <button type="button" (click)="startCreateReadingText()"
                  class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni metin</button>
              }
            </div>
          </div>

          @if (canManageContent() && readingTextEditingId !== null) {
            <form class="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-indigo-200 bg-indigo-50/50 p-4 sm:grid-cols-2"
              (ngSubmit)="saveReadingText()">
              <label class="text-sm text-gray-700 dark:text-gray-200">Başlık
                <input name="readingTextTitle" [(ngModel)]="readingTextDraft.title" required maxlength="300"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Kategori
                <input name="readingTextCategory" [(ngModel)]="readingTextDraft.category" required maxlength="100"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Dil
                <input name="readingTextLanguage" [(ngModel)]="readingTextDraft.language" required maxlength="20" placeholder="tr"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Bağlı egzersiz
                <select name="readingTextExerciseId" [(ngModel)]="readingTextDraft.exerciseId"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800">
                  <option [ngValue]="null">Bağlantısız</option>
                  @for (exercise of exercises(); track exercise.id) {
                    <option [ngValue]="exercise.id">{{ exercise.title }}</option>
                  }
                </select>
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Kelime sayısı
                <input type="number" name="readingTextWordCount" [(ngModel)]="readingTextDraft.wordCount" required min="1" max="1000000"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Zorluk (0-10)
                <input type="number" name="readingTextDifficulty" [(ngModel)]="readingTextDraft.difficultyLevel" min="0" max="10"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Önerilen min. seviye
                <input type="number" name="readingTextMinLevel" [(ngModel)]="readingTextDraft.recommendedMinLevel" min="0" max="100"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200">Önerilen maks. seviye
                <input type="number" name="readingTextMaxLevel" [(ngModel)]="readingTextDraft.recommendedMaxLevel" min="0" max="100"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">İçerik
                <textarea name="readingTextContent" [(ngModel)]="readingTextDraft.content" required maxlength="1000000" rows="6"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800"></textarea>
              </label>
              <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">Etiketler (virgülle ayrılmış)
                <input name="readingTextTags" [(ngModel)]="readingTextDraft.tags" maxlength="2000"
                  class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
              </label>
              <label class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-200">
                <input type="checkbox" name="readingTextIsActive" [(ngModel)]="readingTextDraft.isActive" /> Aktif
              </label>
              <div class="flex justify-end gap-2 sm:col-span-2">
                <button type="button" (click)="cancelReadingTextEdit()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Vazgeç</button>
                <button type="submit" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50">
                  {{ saving() ? 'Kaydediliyor…' : (readingTextEditingId === 'new' ? 'Oluştur' : 'Kaydet') }}
                </button>
              </div>
            </form>
          }

          @if (readingTexts().length) {
            <div class="mt-4 grid grid-cols-1 gap-3 lg:grid-cols-2">
              @for (text of readingTexts(); track text.id) {
                <article class="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
                  <div class="flex items-start justify-between gap-3">
                    <div>
                      <h3 class="font-medium text-gray-900 dark:text-white">{{ text.title }}</h3>
                      <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">{{ text.category }} · {{ text.language }} · {{ text.wordCount }} kelime · Zorluk {{ text.difficultyLevel }}</p>
                    </div>
                    <span class="rounded-full px-2 py-1 text-xs" [class.bg-emerald-100]="text.isActive" [class.text-emerald-700]="text.isActive" [class.bg-gray-100]="!text.isActive" [class.text-gray-600]="!text.isActive">
                      {{ text.isActive ? 'Aktif' : 'Pasif' }}
                    </span>
                  </div>
                  @if (canManageContent()) {
                    <div class="mt-3 flex gap-2">
                      <button type="button" (click)="startEditReadingText(text)" class="rounded border border-gray-300 px-2 py-1 text-xs">Düzenle</button>
                      <button type="button" (click)="deleteReadingText(text)" class="rounded border border-red-300 px-2 py-1 text-xs text-red-700">Sil</button>
                    </div>
                  }
                </article>
              }
            </div>
          } @else if (!loading()) {
            <p class="mt-4 text-sm text-gray-500 dark:text-gray-400">Katalogda gösterilecek okuma metni bulunamadı.</p>
          }
        </div>
      }
    </section>
  `
})
export class SpeedReadingOverviewComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService);
  private readonly authService = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly capabilities = signal<SpeedReadingCapabilities | null>(null);
  readonly exerciseTypes = signal<SpeedReadingExerciseType[]>([]);
  readonly exercises = signal<SpeedReadingExercise[]>([]);
  readonly readingTexts = signal<SpeedReadingReadingText[]>([]);
  readonly saving = signal(false);
  readonly canManageContent = computed(() =>
    this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingContentManage));
  editingId: string | null = null;
  draft: SpeedReadingExerciseTypeRequest = this.emptyDraft();
  exerciseEditingId: string | null = null;
  exerciseDraft: SpeedReadingExerciseRequest = this.emptyExerciseDraft();
  readingTextEditingId: string | null = null;
  readingTextDraft: SpeedReadingReadingTextRequest = this.emptyReadingTextDraft();

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load() {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      capabilities: this.service.getCapabilities(),
      exerciseTypes: this.service.getExerciseTypes(),
      exercises: this.service.getExercises(),
      readingTexts: this.service.getReadingTexts()
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => {
        this.capabilities.set(value.capabilities);
        this.exerciseTypes.set(value.exerciseTypes.items);
        this.exercises.set(value.exercises.items);
        this.readingTexts.set(value.readingTexts);
      },
      error: () => this.error.set('Hızlı okuma servis bilgisi yüklenemedi.')
    });
  }

  startCreate() {
    this.editingId = 'new';
    this.draft = this.emptyDraft();
  }

  startEdit(type: SpeedReadingExerciseType) {
    this.editingId = type.id;
    this.draft = {
      name: type.name,
      displayName: type.displayName,
      description: type.description,
      iconName: type.iconName,
      colorCode: type.colorCode,
      sortOrder: type.sortOrder,
      isActive: type.isActive,
      engineType: type.engineType,
      categoryId: type.categoryId
    };
  }

  cancelEdit() {
    this.editingId = null;
  }

  saveExerciseType() {
    if (this.editingId === null) return;
    this.saving.set(true);
    const request$ = this.editingId === 'new'
      ? this.service.createExerciseType(this.draft)
      : this.service.updateExerciseType(this.editingId, this.draft);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.editingId = null;
        this.load();
      },
      error: () => this.error.set('Egzersiz türü kaydedilemedi.')
    });
  }

  deleteExerciseType(type: SpeedReadingExerciseType) {
    if (!globalThis.confirm(`“${type.displayName || type.name}” türü silinsin mi?`)) return;
    this.service.deleteExerciseType(type.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Egzersiz türü silinemedi.')
    });
  }

  startCreateExercise() {
    this.exerciseEditingId = 'new';
    this.exerciseDraft = this.emptyExerciseDraft();
  }

  startEditExercise(exercise: SpeedReadingExercise) {
    this.exerciseEditingId = exercise.id;
    this.exerciseDraft = {
      title: exercise.title,
      description: exercise.description,
      difficultyLevel: exercise.difficultyLevel,
      exerciseTypeId: exercise.exerciseTypeId,
      configurationJson: exercise.configurationJson,
      targetAgeGroupConfigurationId: exercise.targetAgeGroupConfigurationId
    };
  }

  cancelExerciseEdit() {
    this.exerciseEditingId = null;
  }

  saveExercise() {
    if (this.exerciseEditingId === null) return;
    this.saving.set(true);
    const request$ = this.exerciseEditingId === 'new'
      ? this.service.createExercise(this.exerciseDraft)
      : this.service.updateExercise(this.exerciseEditingId, this.exerciseDraft);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.exerciseEditingId = null;
        this.load();
      },
      error: () => this.error.set('Egzersiz kaydedilemedi.')
    });
  }

  deleteExercise(exercise: SpeedReadingExercise) {
    if (!globalThis.confirm(`“${exercise.title}” egzersizi silinsin mi?`)) return;
    this.service.deleteExercise(exercise.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Egzersiz silinemedi. Bağlı okuma metinlerini kontrol edin.')
    });
  }

  startCreateReadingText() {
    this.readingTextEditingId = 'new';
    this.readingTextDraft = this.emptyReadingTextDraft();
  }

  startEditReadingText(text: SpeedReadingReadingText) {
    this.readingTextEditingId = text.id;
    this.readingTextDraft = {
      title: text.title,
      content: '',
      wordCount: text.wordCount,
      category: text.category,
      difficultyLevel: text.difficultyLevel,
      targetAgeGroupConfigurationId: null,
      language: text.language,
      isActive: text.isActive,
      tags: '',
      recommendedMinLevel: 0,
      recommendedMaxLevel: 100,
      exerciseId: text.exerciseId
    };
    this.service.getReadingText(text.id).subscribe({
      next: details => {
        this.readingTextDraft = {
          ...this.readingTextDraft,
          content: details.content,
          targetAgeGroupConfigurationId: details.targetAgeGroupConfigurationId,
          tags: details.tags.join(', '),
          recommendedMinLevel: details.recommendedMinLevel,
          recommendedMaxLevel: details.recommendedMaxLevel
        };
      },
      error: () => this.error.set('Okuma metni ayrıntıları yüklenemedi.')
    });
  }

  cancelReadingTextEdit() {
    this.readingTextEditingId = null;
  }

  saveReadingText() {
    if (this.readingTextEditingId === null) return;
    this.saving.set(true);
    const request$ = this.readingTextEditingId === 'new'
      ? this.service.createReadingText(this.readingTextDraft)
      : this.service.updateReadingText(this.readingTextEditingId, this.readingTextDraft);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.readingTextEditingId = null;
        this.load();
      },
      error: () => this.error.set('Okuma metni kaydedilemedi.')
    });
  }

  deleteReadingText(text: SpeedReadingReadingText) {
    if (!globalThis.confirm(`“${text.title}” okuma metni silinsin mi?`)) return;
    this.service.deleteReadingText(text.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Okuma metni silinemedi. Bağlı soruları kontrol edin.')
    });
  }

  private emptyDraft(): SpeedReadingExerciseTypeRequest {
    return {
      name: '',
      displayName: '',
      description: '',
      iconName: '',
      colorCode: '',
      sortOrder: 0,
      isActive: true,
      engineType: '',
      categoryId: null
    };
  }

  private emptyExerciseDraft(): SpeedReadingExerciseRequest {
    return {
      title: '',
      description: '',
      difficultyLevel: 0,
      exerciseTypeId: '',
      configurationJson: '{}',
      targetAgeGroupConfigurationId: null
    };
  }

  private emptyReadingTextDraft(): SpeedReadingReadingTextRequest {
    return {
      title: '',
      content: '',
      wordCount: 1,
      category: '',
      difficultyLevel: 0,
      targetAgeGroupConfigurationId: null,
      language: 'tr',
      isActive: true,
      tags: '',
      recommendedMinLevel: 0,
      recommendedMaxLevel: 100,
      exerciseId: null
    };
  }
}
