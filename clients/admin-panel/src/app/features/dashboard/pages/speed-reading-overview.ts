import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { finalize, forkJoin, of } from 'rxjs';
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
  SpeedReadingReadingTextRequest,
  SpeedReadingReadingQuestion,
  SpeedReadingReadingQuestionRequest,
  SpeedReadingReadingQuestionUpdateRequest,
  SpeedReadingProgramTemplate,
  SpeedReadingProgramTemplateRequest,
  SpeedReadingLearningPathTemplate,
  SpeedReadingLearningPathTemplateRequest,
  SpeedReadingLearningPathNode,
  SpeedReadingLearningPathNodeRequest,
  SpeedReadingLearningPathNodeUpdateRequest,
  SpeedReadingLearningPathNodeContentRequest,
  SpeedReadingAchievement,
  SpeedReadingAchievementRequest
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
                      <button type="button" (click)="manageReadingTextQuestions(text)" class="rounded border border-indigo-300 px-2 py-1 text-xs text-indigo-700">Soruları yönet</button>
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

          @if (canManageContent() && selectedReadingTextId !== null) {
            <div class="mt-5 rounded-lg border border-indigo-200 bg-indigo-50/50 p-4">
              <div class="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h3 class="font-semibold text-gray-900 dark:text-white">Metin soruları</h3>
                  <p class="text-sm text-gray-600 dark:text-gray-300">{{ selectedReadingTextTitle }} · {{ readingQuestions().length }} soru</p>
                </div>
                <div class="flex gap-2">
                  <button type="button" (click)="startCreateQuestion()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni soru</button>
                  <button type="button" (click)="closeQuestionManager()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Kapat</button>
                </div>
              </div>

              @if (questionEditingId !== null) {
                <form class="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-indigo-200 bg-white p-4 sm:grid-cols-2" (ngSubmit)="saveQuestion()">
                  <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">Soru
                    <textarea name="questionText" [(ngModel)]="questionDraft.questionText" required maxlength="2000" rows="3"
                      class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800"></textarea>
                  </label>
                  <label class="text-sm text-gray-700 dark:text-gray-200">Soru türü
                    <select name="questionType" [(ngModel)]="questionDraft.type" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800">
                      <option [ngValue]="1">Gerçek anlam</option>
                      <option [ngValue]="2">Çıkarım</option>
                      <option [ngValue]="3">Değerlendirme</option>
                    </select>
                  </label>
                  <label class="text-sm text-gray-700 dark:text-gray-200">Bloom seviyesi
                    <select name="bloomLevel" [(ngModel)]="questionDraft.bloomLevel" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800">
                      <option [ngValue]="1">Hatırlama</option>
                      <option [ngValue]="2">Anlama</option>
                      <option [ngValue]="3">Uygulama</option>
                      <option [ngValue]="4">Analiz</option>
                      <option [ngValue]="5">Değerlendirme</option>
                      <option [ngValue]="6">Yaratma</option>
                    </select>
                  </label>
                  <label class="text-sm text-gray-700 dark:text-gray-200">Zorluk (0-10)
                    <input type="number" name="questionDifficulty" [(ngModel)]="questionDraft.difficultyLevel" min="0" max="10"
                      class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                  </label>
                  <label class="text-sm text-gray-700 dark:text-gray-200">Sıra
                    <input type="number" name="questionOrder" [(ngModel)]="questionDraft.orderIndex" min="0" max="10000"
                      class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                  </label>
                  <label class="text-sm text-gray-700 dark:text-gray-200">A şıkkı
                    <input name="optionA" [(ngModel)]="questionDraft.optionA" required maxlength="500"
                      class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                  </label>
                  <label class="text-sm text-gray-700 dark:text-gray-200">B şıkkı
                    <input name="optionB" [(ngModel)]="questionDraft.optionB" required maxlength="500"
                      class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                  </label>
                  <label class="text-sm text-gray-700 dark:text-gray-200">C şıkkı
                    <input name="optionC" [(ngModel)]="questionDraft.optionC" required maxlength="500"
                      class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                  </label>
                  <label class="text-sm text-gray-700 dark:text-gray-200">D şıkkı
                    <input name="optionD" [(ngModel)]="questionDraft.optionD" required maxlength="500"
                      class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                  </label>
                  <label class="text-sm text-gray-700 dark:text-gray-200">Doğru cevap
                    <select name="correctAnswer" [(ngModel)]="questionDraft.correctAnswer" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800">
                      <option value="A">A</option>
                      <option value="B">B</option>
                      <option value="C">C</option>
                      <option value="D">D</option>
                    </select>
                  </label>
                  <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">Açıklama
                    <textarea name="questionExplanation" [(ngModel)]="questionDraft.explanation" maxlength="2000" rows="2"
                      class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800"></textarea>
                  </label>
                  <div class="flex justify-end gap-2 sm:col-span-2">
                    <button type="button" (click)="cancelQuestionEdit()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Vazgeç</button>
                    <button type="submit" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50">
                      {{ saving() ? 'Kaydediliyor…' : (questionEditingId === 'new' ? 'Oluştur' : 'Kaydet') }}
                    </button>
                  </div>
                </form>
              }

              @if (readingQuestions().length) {
                <div class="mt-4 grid grid-cols-1 gap-3">
                  @for (question of readingQuestions(); track question.id) {
                    <article class="rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
                      <div class="flex items-start justify-between gap-3">
                        <div>
                          <p class="font-medium text-gray-900 dark:text-white">{{ question.orderIndex + 1 }}. {{ question.questionText }}</p>
                          <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">Tür {{ question.type }} · Bloom {{ question.bloomLevel }} · Zorluk {{ question.difficultyLevel }} · Doğru: {{ question.correctAnswer }}</p>
                        </div>
                        <div class="flex shrink-0 gap-2">
                          <button type="button" (click)="startEditQuestion(question)" class="rounded border border-gray-300 px-2 py-1 text-xs">Düzenle</button>
                          <button type="button" (click)="deleteQuestion(question)" class="rounded border border-red-300 px-2 py-1 text-xs text-red-700">Sil</button>
                        </div>
                      </div>
                      <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">A) {{ question.optionA }} · B) {{ question.optionB }} · C) {{ question.optionC }} · D) {{ question.optionD }}</p>
                    </article>
                  }
                </div>
              } @else if (!loading()) {
                <p class="mt-4 text-sm text-gray-500 dark:text-gray-400">Bu metin için henüz soru yok.</p>
              }
            </div>
          }
        </div>

        @if (canManagePrograms()) {
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <div class="flex items-center justify-between gap-3">
              <div>
                <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Program şablonları</h2>
                <p class="text-sm text-gray-500 dark:text-gray-400">Öğrenci programlarının seviye, süre ve haftalık plan ayarları.</p>
              </div>
              <button type="button" (click)="startCreateProgram()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni program</button>
            </div>

            @if (programEditingId !== null) {
              <form class="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-indigo-200 bg-indigo-50/50 p-4 sm:grid-cols-2" (ngSubmit)="saveProgram()">
                <label class="text-sm text-gray-700 dark:text-gray-200">Ad
                  <input name="programName" [(ngModel)]="programDraft.name" required maxlength="200" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Hedef yaş grubu kimliği
                  <input name="programAgeGroup" [(ngModel)]="programDraft.targetAgeGroupConfigurationId" required class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Min. değerlendirme puanı
                  <input type="number" name="programMinScore" [(ngModel)]="programDraft.minAssessmentScore" min="0" max="100" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Maks. değerlendirme puanı
                  <input type="number" name="programMaxScore" [(ngModel)]="programDraft.maxAssessmentScore" min="0" max="100" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Başlangıç zorluğu (0-10)
                  <input type="number" name="programInitialDifficulty" [(ngModel)]="programDraft.initialDifficultyLevel" min="0" max="10" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Maksimum zorluk (0-10)
                  <input type="number" name="programMaxDifficulty" [(ngModel)]="programDraft.maxDifficultyLevel" min="0" max="10" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Zorluk artış aralığı (hafta)
                  <input type="number" name="programIncreaseWeeks" [(ngModel)]="programDraft.weeksPerDifficultyIncrease" min="1" max="52" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Toplam hafta
                  <input type="number" name="programTotalWeeks" [(ngModel)]="programDraft.totalWeeks" min="1" max="520" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Toplam gün
                  <input type="number" name="programTotalDays" [(ngModel)]="programDraft.totalDays" min="1" max="3650" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Sıra
                  <input type="number" name="programDisplayOrder" [(ngModel)]="programDraft.displayOrder" min="0" max="10000" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Program tipi
                  <input type="number" name="programType" [(ngModel)]="programDraft.programType" min="0" max="100" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Sınav türü
                  <input name="programExamType" [(ngModel)]="programDraft.examType" maxlength="100" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">Açıklama
                  <textarea name="programDescription" [(ngModel)]="programDraft.description" maxlength="5000" rows="2" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800"></textarea>
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">Haftalık plan JSON
                  <textarea name="weeklyPatternJson" [(ngModel)]="programDraft.weeklyPatternJson" required rows="4" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 font-mono text-xs dark:border-gray-600 dark:bg-gray-800"></textarea>
                </label>
                <label class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-200"><input type="checkbox" name="programIsActive" [(ngModel)]="programDraft.isActive" /> Aktif</label>
                <label class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-200"><input type="checkbox" name="programIsAssessment" [(ngModel)]="programDraft.isAssessment" /> Değerlendirme programı</label>
                <div class="flex justify-end gap-2 sm:col-span-2">
                  <button type="button" (click)="cancelProgramEdit()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Vazgeç</button>
                  <button type="submit" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50">{{ saving() ? 'Kaydediliyor…' : (programEditingId === 'new' ? 'Oluştur' : 'Kaydet') }}</button>
                </div>
              </form>
            }

            @if (programTemplates().length) {
              <div class="mt-4 grid grid-cols-1 gap-3 lg:grid-cols-2">
                @for (program of programTemplates(); track program.id) {
                  <article class="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
                    <div class="flex items-start justify-between gap-3">
                      <div>
                        <h3 class="font-medium text-gray-900 dark:text-white">{{ program.name }}</h3>
                        <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">{{ program.totalWeeks }} hafta · {{ program.totalDays }} gün · Zorluk {{ program.initialDifficultyLevel }}-{{ program.maxDifficultyLevel }}</p>
                      </div>
                      <span class="rounded-full px-2 py-1 text-xs" [class.bg-emerald-100]="program.isActive" [class.text-emerald-700]="program.isActive" [class.bg-gray-100]="!program.isActive" [class.text-gray-600]="!program.isActive">{{ program.isActive ? 'Aktif' : 'Pasif' }}</span>
                    </div>
                    <p class="mt-2 line-clamp-2 text-sm text-gray-600 dark:text-gray-300">{{ program.description }}</p>
                    <div class="mt-3 flex gap-2">
                      <button type="button" (click)="startEditProgram(program)" class="rounded border border-gray-300 px-2 py-1 text-xs">Düzenle</button>
                      <button type="button" (click)="deleteProgram(program)" class="rounded border border-red-300 px-2 py-1 text-xs text-red-700">Sil</button>
                    </div>
                  </article>
                }
              </div>
            } @else if (!loading()) {
              <p class="mt-4 text-sm text-gray-500 dark:text-gray-400">Henüz program şablonu bulunamadı.</p>
            }
          </div>
        }

        @if (canManagePrograms()) {
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <div class="flex items-center justify-between gap-3">
              <div>
                <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Öğrenme yolu şablonları</h2>
                <p class="text-sm text-gray-500 dark:text-gray-400">Düğümler ve önkoşullar için üst seviye şablon yönetimi.</p>
              </div>
              <button type="button" (click)="startCreateLearningPathTemplate()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni yol</button>
            </div>

            @if (learningPathEditingId !== null) {
              <form class="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-indigo-200 bg-indigo-50/50 p-4 sm:grid-cols-2" (ngSubmit)="saveLearningPathTemplate()">
                <label class="text-sm text-gray-700 dark:text-gray-200">Ad
                  <input name="pathName" [(ngModel)]="learningPathDraft.name" required maxlength="200" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Hedef yaş grubu kimliği (isteğe bağlı)
                  <input name="pathAgeGroup" [(ngModel)]="learningPathDraft.targetAgeGroupConfigurationId" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Tahmini gün
                  <input type="number" name="pathEstimatedDays" [(ngModel)]="learningPathDraft.estimatedDays" min="1" max="3650" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="flex items-center gap-2 self-end text-sm text-gray-700 dark:text-gray-200"><input type="checkbox" name="pathIsActive" [(ngModel)]="learningPathDraft.isActive" /> Aktif</label>
                <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">Açıklama
                  <textarea name="pathDescription" [(ngModel)]="learningPathDraft.description" maxlength="5000" rows="2" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800"></textarea>
                </label>
                <div class="flex justify-end gap-2 sm:col-span-2">
                  <button type="button" (click)="cancelLearningPathTemplateEdit()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Vazgeç</button>
                  <button type="submit" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50">{{ saving() ? 'Kaydediliyor…' : (learningPathEditingId === 'new' ? 'Oluştur' : 'Kaydet') }}</button>
                </div>
              </form>
            }

            @if (learningPathTemplates().length) {
              <div class="mt-4 grid grid-cols-1 gap-3 lg:grid-cols-2">
                @for (template of learningPathTemplates(); track template.id) {
                  <article class="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
                    <div class="flex items-start justify-between gap-3">
                      <div>
                        <h3 class="font-medium text-gray-900 dark:text-white">{{ template.name }}</h3>
                        <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">{{ template.totalNodes }} düğüm · {{ template.estimatedDays }} gün</p>
                      </div>
                      <span class="rounded-full px-2 py-1 text-xs" [class.bg-emerald-100]="template.isActive" [class.text-emerald-700]="template.isActive" [class.bg-gray-100]="!template.isActive" [class.text-gray-600]="!template.isActive">{{ template.isActive ? 'Aktif' : 'Pasif' }}</span>
                    </div>
                    <p class="mt-2 line-clamp-2 text-sm text-gray-600 dark:text-gray-300">{{ template.description }}</p>
                    <div class="mt-3 flex gap-2">
                      <button type="button" (click)="manageLearningPathNodes(template)" class="rounded border border-indigo-300 px-2 py-1 text-xs text-indigo-700">Düğümleri yönet</button>
                      <button type="button" (click)="startEditLearningPathTemplate(template)" class="rounded border border-gray-300 px-2 py-1 text-xs">Düzenle</button>
                      <button type="button" (click)="deleteLearningPathTemplate(template)" class="rounded border border-red-300 px-2 py-1 text-xs text-red-700">Sil</button>
                    </div>
                  </article>
                }
              </div>
            } @else if (!loading()) {
              <p class="mt-4 text-sm text-gray-500 dark:text-gray-400">Henüz öğrenme yolu şablonu bulunamadı.</p>
            }

            @if (selectedLearningPathId !== null) {
              <div class="mt-5 rounded-lg border border-indigo-200 bg-indigo-50/50 p-4">
                <div class="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <h3 class="font-semibold text-gray-900 dark:text-white">Öğrenme yolu düğümleri</h3>
                    <p class="text-sm text-gray-600 dark:text-gray-300">{{ selectedLearningPathTitle }} · {{ learningPathNodes().length }} düğüm</p>
                  </div>
                  <div class="flex gap-2">
                    <button type="button" (click)="startCreateLearningPathNode()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni düğüm</button>
                    <button type="button" (click)="closeLearningPathNodes()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Kapat</button>
                  </div>
                </div>

                @if (learningPathNodeEditingId !== null) {
                  <form class="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-indigo-200 bg-white p-4 sm:grid-cols-2" (ngSubmit)="saveLearningPathNode()">
                    <label class="text-sm text-gray-700 dark:text-gray-200">Başlık
                      <input name="nodeTitle" [(ngModel)]="learningPathNodeDraft.title" required maxlength="250" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                    </label>
                    <label class="text-sm text-gray-700 dark:text-gray-200">Düğüm tipi
                      <input name="nodeType" [(ngModel)]="learningPathNodeDraft.nodeType" required maxlength="100" placeholder="Exercise" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                    </label>
                    <label class="text-sm text-gray-700 dark:text-gray-200">Ebeveyn düğüm
                      <select name="parentNodeId" [(ngModel)]="learningPathNodeDraft.parentNodeId" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800">
                        <option [ngValue]="null">Kök düğüm</option>
                        @for (parent of learningPathNodes(); track parent.id) {
                          @if (parent.id !== learningPathNodeEditingId) {
                            <option [ngValue]="parent.id">{{ parent.title }}</option>
                          }
                        }
                      </select>
                    </label>
                    <label class="text-sm text-gray-700 dark:text-gray-200">Sıra
                      <input type="number" name="nodeOrder" [(ngModel)]="learningPathNodeDraft.order" min="0" max="10000" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                    </label>
                    <label class="text-sm text-gray-700 dark:text-gray-200">İçerik tipi (isteğe bağlı)
                      <input name="nodeContentType" [(ngModel)]="learningPathNodeDraft.contentType" maxlength="100" placeholder="ReadingText" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                    </label>
                    <label class="text-sm text-gray-700 dark:text-gray-200">İçerik kimliği (isteğe bağlı)
                      <input name="nodeContentId" [(ngModel)]="learningPathNodeDraft.contentId" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                    </label>
                    <div class="flex justify-end gap-2 sm:col-span-2">
                      <button type="button" (click)="cancelLearningPathNodeEdit()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Vazgeç</button>
                      <button type="submit" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50">{{ saving() ? 'Kaydediliyor…' : (learningPathNodeEditingId === 'new' ? 'Oluştur' : 'Kaydet') }}</button>
                    </div>
                  </form>
                }

                @if (learningPathNodes().length) {
                  <div class="mt-4 grid grid-cols-1 gap-3">
                    @for (node of learningPathNodes(); track node.id) {
                      <article class="rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
                        <div class="flex items-start justify-between gap-3">
                          <div>
                            <p class="font-medium text-gray-900 dark:text-white">{{ node.order + 1 }}. {{ node.title }}</p>
                            <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">{{ node.nodeType }} · {{ node.parentNodeId ? 'Alt düğüm' : 'Kök' }} · {{ node.contents.length }} içerik · {{ node.prerequisiteNodeIds.length }} önkoşul</p>
                          </div>
                          <div class="flex shrink-0 gap-2">
                            <button type="button" (click)="manageLearningPathNodeRelations(node)" class="rounded border border-indigo-300 px-2 py-1 text-xs text-indigo-700">İlişkiler</button>
                            <button type="button" (click)="startEditLearningPathNode(node)" class="rounded border border-gray-300 px-2 py-1 text-xs">Düzenle</button>
                            <button type="button" (click)="deleteLearningPathNode(node)" class="rounded border border-red-300 px-2 py-1 text-xs text-red-700">Sil</button>
                          </div>
                        </div>
                      </article>
                    }
                  </div>
                } @else if (!loading()) {
                  <p class="mt-4 text-sm text-gray-500 dark:text-gray-400">Bu şablonda henüz düğüm yok.</p>
                }

                @if (selectedLearningPathNodeId !== null) {
                  <div class="mt-5 rounded-lg border border-indigo-200 bg-white p-4">
                    <div class="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <h4 class="font-semibold text-gray-900 dark:text-white">Düğüm içerikleri ve önkoşulları</h4>
                        <p class="text-sm text-gray-600 dark:text-gray-300">{{ selectedLearningPathNodeTitle }}</p>
                      </div>
                      <button type="button" (click)="closeLearningPathNodeRelations()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Kapat</button>
                    </div>
                    <div class="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
                      <label class="text-sm text-gray-700 dark:text-gray-200">İçerik türü
                        <select name="relationContentKind" [(ngModel)]="relationContentKind" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800">
                          <option value="exercise">Egzersiz</option>
                          <option value="readingText">Okuma metni</option>
                        </select>
                      </label>
                      @if (relationContentKind === 'exercise') {
                        <label class="text-sm text-gray-700 dark:text-gray-200">Egzersiz
                          <select name="relationExerciseId" [(ngModel)]="relationContentDraft.exerciseId" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800">
                            <option [ngValue]="null">Seçin</option>
                            @for (exercise of exercises(); track exercise.id) { <option [ngValue]="exercise.id">{{ exercise.title }}</option> }
                          </select>
                        </label>
                      } @else {
                        <label class="text-sm text-gray-700 dark:text-gray-200">Okuma metni
                          <select name="relationReadingTextId" [(ngModel)]="relationContentDraft.readingTextId" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800">
                            <option [ngValue]="null">Seçin</option>
                            @for (text of readingTexts(); track text.id) { <option [ngValue]="text.id">{{ text.title }}</option> }
                          </select>
                        </label>
                      }
                      <label class="text-sm text-gray-700 dark:text-gray-200">Açıklama
                        <input name="relationDescription" [(ngModel)]="relationContentDraft.description" maxlength="1000" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                      </label>
                    </div>
                    <div class="mt-3 flex justify-end">
                      <button type="button" (click)="addLearningPathNodeContent()" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50">İçerik ekle</button>
                    </div>
                    @if (selectedLearningPathNode()?.contents?.length) {
                      <div class="mt-3 space-y-2">
                        @for (content of selectedLearningPathNode()?.contents ?? []; track content.id) {
                          <div class="flex items-center justify-between rounded border border-gray-200 px-3 py-2 text-sm dark:border-gray-700">
                            <span>{{ content.exerciseId ? 'Egzersiz: ' + content.exerciseId : 'Okuma metni: ' + content.readingTextId }}{{ content.description ? ' · ' + content.description : '' }}</span>
                            <button type="button" (click)="deleteLearningPathNodeContent(content.id)" class="rounded border border-red-300 px-2 py-1 text-xs text-red-700">Sil</button>
                          </div>
                        }
                      </div>
                    }
                    <div class="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-[1fr_auto]">
                      <label class="text-sm text-gray-700 dark:text-gray-200">Önkoşul düğümü
                        <select name="relationPrerequisiteNodeId" [(ngModel)]="relationPrerequisiteNodeId" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800">
                          <option [ngValue]="null">Seçin</option>
                          @for (candidate of learningPathNodes(); track candidate.id) {
                            @if (candidate.id !== selectedLearningPathNodeId) { <option [ngValue]="candidate.id">{{ candidate.title }}</option> }
                          }
                        </select>
                      </label>
                      <button type="button" (click)="addLearningPathPrerequisite()" [disabled]="saving()" class="self-end rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50">Önkoşul ekle</button>
                    </div>
                    @if (selectedLearningPathNode()?.prerequisiteNodeIds?.length) {
                      <div class="mt-3 space-y-2">
                        @for (prerequisiteId of selectedLearningPathNode()?.prerequisiteNodeIds ?? []; track prerequisiteId) {
                          <div class="flex items-center justify-between rounded border border-gray-200 px-3 py-2 text-sm dark:border-gray-700">
                            <span>Önkoşul: {{ learningPathNodeTitle(prerequisiteId) }}</span>
                            <button type="button" (click)="deleteLearningPathPrerequisite(prerequisiteId)" class="rounded border border-red-300 px-2 py-1 text-xs text-red-700">Sil</button>
                          </div>
                        }
                      </div>
                    }
                  </div>
                }
              </div>
            }
          </div>
        }

        @if (canManageGamification()) {
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <div class="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Oyunlaştırma kazanımları</h2>
                <p class="text-sm text-gray-500 dark:text-gray-400">Öğrencilerin açabileceği başarı tanımlarını yönetin.</p>
              </div>
              <button type="button" (click)="startCreateAchievement()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni kazanım</button>
            </div>

            @if (achievementEditingId !== null) {
              <form class="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-indigo-200 bg-indigo-50/50 p-4 sm:grid-cols-2" (ngSubmit)="saveAchievement()">
                <label class="text-sm text-gray-700 dark:text-gray-200">Ad
                  <input name="achievementName" [(ngModel)]="achievementDraft.name" required maxlength="200" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Kategori
                  <input name="achievementCategory" [(ngModel)]="achievementDraft.category" required maxlength="50" placeholder="Reading" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Seviye
                  <input name="achievementTier" [(ngModel)]="achievementDraft.tier" required maxlength="50" placeholder="Bronze" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">XP ödülü
                  <input type="number" name="achievementXp" [(ngModel)]="achievementDraft.xpReward" min="0" max="1000000" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200 sm:col-span-2">Açıklama
                  <textarea name="achievementDescription" [(ngModel)]="achievementDraft.description" required maxlength="500" rows="2" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800"></textarea>
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Kriter tipi
                  <input name="achievementCriteriaType" [(ngModel)]="achievementDraft.criteriaType" required maxlength="100" placeholder="streak" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Kriter JSON
                  <input name="achievementCriteriaValue" [(ngModel)]="achievementDraft.criteriaValue" required placeholder="{&quot;days&quot;:7}" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 font-mono text-xs dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Emoji
                  <input name="achievementIconEmoji" [(ngModel)]="achievementDraft.iconEmoji" maxlength="10" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="text-sm text-gray-700 dark:text-gray-200">Sıra
                  <input type="number" name="achievementSortOrder" [(ngModel)]="achievementDraft.sortOrder" min="0" max="100000" class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 dark:border-gray-600 dark:bg-gray-800" />
                </label>
                <label class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-200"><input type="checkbox" name="achievementActive" [(ngModel)]="achievementDraft.isActive" /> Aktif</label>
                <label class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-200"><input type="checkbox" name="achievementRepeatable" [(ngModel)]="achievementDraft.isRepeatable" /> Tekrarlanabilir</label>
                <div class="flex justify-end gap-2 sm:col-span-2">
                  <button type="button" (click)="cancelAchievementEdit()" class="rounded-lg border border-gray-300 px-3 py-2 text-sm">Vazgeç</button>
                  <button type="submit" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50">{{ saving() ? 'Kaydediliyor…' : (achievementEditingId === 'new' ? 'Oluştur' : 'Kaydet') }}</button>
                </div>
              </form>
            }

            @if (achievements().length) {
              <div class="mt-4 grid grid-cols-1 gap-3 lg:grid-cols-2">
                @for (achievement of achievements(); track achievement.id) {
                  <article class="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
                    <div class="flex items-start justify-between gap-3">
                      <div>
                        <h3 class="font-medium text-gray-900 dark:text-white">{{ achievement.iconEmoji }} {{ achievement.name }}</h3>
                        <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">{{ achievement.category }} · {{ achievement.tier }} · {{ achievement.xpReward }} XP · {{ achievement.unlockedByUsersCount }} açan öğrenci</p>
                      </div>
                      <div class="flex shrink-0 gap-2">
                        <button type="button" (click)="startEditAchievement(achievement)" class="rounded border border-gray-300 px-2 py-1 text-xs">Düzenle</button>
                        <button type="button" (click)="deleteAchievement(achievement)" class="rounded border border-red-300 px-2 py-1 text-xs text-red-700">Sil</button>
                      </div>
                    </div>
                    <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">{{ achievement.description }}</p>
                  </article>
                }
              </div>
            } @else if (!loading()) {
              <p class="mt-4 text-sm text-gray-500 dark:text-gray-400">Henüz kazanım tanımlanmamış.</p>
            }
          </div>
        }
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
  readonly readingQuestions = signal<SpeedReadingReadingQuestion[]>([]);
  readonly programTemplates = signal<SpeedReadingProgramTemplate[]>([]);
  readonly learningPathTemplates = signal<SpeedReadingLearningPathTemplate[]>([]);
  readonly learningPathNodes = signal<SpeedReadingLearningPathNode[]>([]);
  readonly achievements = signal<SpeedReadingAchievement[]>([]);
  readonly saving = signal(false);
  readonly canManageContent = computed(() =>
    this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingContentManage));
  readonly canManagePrograms = computed(() =>
    this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingProgramManage));
  readonly canManageGamification = computed(() =>
    this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingGamificationManage));
  editingId: string | null = null;
  draft: SpeedReadingExerciseTypeRequest = this.emptyDraft();
  exerciseEditingId: string | null = null;
  exerciseDraft: SpeedReadingExerciseRequest = this.emptyExerciseDraft();
  readingTextEditingId: string | null = null;
  readingTextDraft: SpeedReadingReadingTextRequest = this.emptyReadingTextDraft();
  selectedReadingTextId: string | null = null;
  selectedReadingTextTitle = '';
  questionEditingId: string | null = null;
  questionDraft: SpeedReadingReadingQuestionRequest = this.emptyQuestionDraft('');
  programEditingId: string | null = null;
  programDraft: SpeedReadingProgramTemplateRequest = this.emptyProgramDraft();
  learningPathEditingId: string | null = null;
  learningPathDraft: SpeedReadingLearningPathTemplateRequest = this.emptyLearningPathDraft();
  selectedLearningPathId: string | null = null;
  selectedLearningPathTitle = '';
  learningPathNodeEditingId: string | null = null;
  learningPathNodeDraft: SpeedReadingLearningPathNodeRequest = this.emptyLearningPathNodeDraft('');
  selectedLearningPathNodeId: string | null = null;
  selectedLearningPathNodeTitle = '';
  relationContentKind: 'exercise' | 'readingText' = 'exercise';
  relationContentDraft: SpeedReadingLearningPathNodeContentRequest = this.emptyLearningPathNodeContentDraft('');
  relationPrerequisiteNodeId: string | null = null;
  achievementEditingId: string | null = null;
  achievementDraft: SpeedReadingAchievementRequest = this.emptyAchievementDraft();

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
      readingTexts: this.service.getReadingTexts(),
      programTemplates: this.canManagePrograms() ? this.service.getProgramTemplates() : of([]),
      learningPathTemplates: this.canManagePrograms() ? this.service.getLearningPathTemplates() : of([]),
      achievements: this.canManageGamification() ? this.service.getAchievementsForAdmin() : of({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0 })
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => {
        this.capabilities.set(value.capabilities);
        this.exerciseTypes.set(value.exerciseTypes.items);
        this.exercises.set(value.exercises.items);
        this.readingTexts.set(value.readingTexts);
        this.programTemplates.set(value.programTemplates);
        this.learningPathTemplates.set(value.learningPathTemplates);
        this.achievements.set(value.achievements.items);
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
      next: () => {
        if (this.selectedReadingTextId === text.id) {
          this.closeQuestionManager();
        }
        this.load();
      },
      error: () => this.error.set('Okuma metni silinemedi. Bağlı soruları kontrol edin.')
    });
  }

  manageReadingTextQuestions(text: SpeedReadingReadingText) {
    this.selectedReadingTextId = text.id;
    this.selectedReadingTextTitle = text.title;
    this.questionEditingId = null;
    this.service.getReadingText(text.id).subscribe({
      next: details => this.readingQuestions.set(details.questions),
      error: () => this.error.set('Okuma metni soruları yüklenemedi.')
    });
  }

  closeQuestionManager() {
    this.selectedReadingTextId = null;
    this.selectedReadingTextTitle = '';
    this.questionEditingId = null;
    this.readingQuestions.set([]);
  }

  startCreateQuestion() {
    if (this.selectedReadingTextId === null) return;
    this.questionEditingId = 'new';
    this.questionDraft = this.emptyQuestionDraft(this.selectedReadingTextId);
  }

  startEditQuestion(question: SpeedReadingReadingQuestion) {
    if (this.selectedReadingTextId === null) return;
    this.questionEditingId = question.id;
    this.questionDraft = {
      readingTextId: this.selectedReadingTextId,
      questionText: question.questionText,
      type: question.type,
      bloomLevel: question.bloomLevel,
      difficultyLevel: question.difficultyLevel,
      explanation: question.explanation,
      optionA: question.optionA,
      optionB: question.optionB,
      optionC: question.optionC,
      optionD: question.optionD,
      correctAnswer: question.correctAnswer,
      orderIndex: question.orderIndex
    };
  }

  cancelQuestionEdit() {
    this.questionEditingId = null;
  }

  saveQuestion() {
    if (this.questionEditingId === null || this.selectedReadingTextId === null) return;
    this.saving.set(true);
    const updateRequest: SpeedReadingReadingQuestionUpdateRequest = {
      questionText: this.questionDraft.questionText,
      type: this.questionDraft.type,
      bloomLevel: this.questionDraft.bloomLevel,
      difficultyLevel: this.questionDraft.difficultyLevel,
      explanation: this.questionDraft.explanation,
      optionA: this.questionDraft.optionA,
      optionB: this.questionDraft.optionB,
      optionC: this.questionDraft.optionC,
      optionD: this.questionDraft.optionD,
      correctAnswer: this.questionDraft.correctAnswer,
      orderIndex: this.questionDraft.orderIndex
    };
    const request$ = this.questionEditingId === 'new'
      ? this.service.createReadingQuestion({ ...this.questionDraft, readingTextId: this.selectedReadingTextId })
      : this.service.updateReadingQuestion(this.questionEditingId, updateRequest);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.questionEditingId = null;
        this.manageReadingTextQuestions({
          id: this.selectedReadingTextId!,
          title: this.selectedReadingTextTitle,
          wordCount: 0,
          category: '',
          difficultyLevel: 0,
          language: '',
          isActive: true,
          exerciseId: null
        });
      },
      error: () => this.error.set('Okuma sorusu kaydedilemedi.')
    });
  }

  deleteQuestion(question: SpeedReadingReadingQuestion) {
    if (!globalThis.confirm(`“${question.questionText}” sorusu silinsin mi?`)) return;
    this.service.deleteReadingQuestion(question.id).subscribe({
      next: () => {
        if (this.selectedReadingTextId !== null) {
          this.manageReadingTextQuestions({
            id: this.selectedReadingTextId,
            title: this.selectedReadingTextTitle,
            wordCount: 0,
            category: '',
            difficultyLevel: 0,
            language: '',
            isActive: true,
            exerciseId: null
          });
        }
      },
      error: () => this.error.set('Okuma sorusu silinemedi.')
    });
  }

  startCreateProgram() {
    this.programEditingId = 'new';
    this.programDraft = this.emptyProgramDraft();
  }

  startEditProgram(program: SpeedReadingProgramTemplate) {
    this.programEditingId = program.id;
    this.programDraft = {
      name: program.name,
      description: program.description,
      targetAgeGroupConfigurationId: program.targetAgeGroupConfigurationId,
      minAssessmentScore: program.minAssessmentScore,
      maxAssessmentScore: program.maxAssessmentScore,
      weeklyPatternJson: program.weeklyPatternJson,
      initialDifficultyLevel: program.initialDifficultyLevel,
      weeksPerDifficultyIncrease: program.weeksPerDifficultyIncrease,
      maxDifficultyLevel: program.maxDifficultyLevel,
      totalWeeks: program.totalWeeks,
      totalDays: program.totalDays,
      isActive: program.isActive,
      displayOrder: program.displayOrder,
      programType: program.programType,
      examType: program.examType,
      isAssessment: program.isAssessment
    };
  }

  cancelProgramEdit() {
    this.programEditingId = null;
  }

  saveProgram() {
    if (this.programEditingId === null) return;
    this.saving.set(true);
    const request$ = this.programEditingId === 'new'
      ? this.service.createProgramTemplate(this.programDraft)
      : this.service.updateProgramTemplate(this.programEditingId, this.programDraft);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.programEditingId = null;
        this.load();
      },
      error: () => this.error.set('Program şablonu kaydedilemedi.')
    });
  }

  deleteProgram(program: SpeedReadingProgramTemplate) {
    if (!globalThis.confirm(`“${program.name}” program şablonu silinsin mi?`)) return;
    this.service.deleteProgramTemplate(program.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Program şablonu silinemedi. Bağlı öğrenci ilerlemesini kontrol edin.')
    });
  }

  startCreateLearningPathTemplate() {
    this.learningPathEditingId = 'new';
    this.learningPathDraft = this.emptyLearningPathDraft();
  }

  startEditLearningPathTemplate(template: SpeedReadingLearningPathTemplate) {
    this.learningPathEditingId = template.id;
    this.learningPathDraft = {
      name: template.name,
      targetAgeGroupConfigurationId: template.targetAgeGroupConfigurationId,
      description: template.description,
      estimatedDays: template.estimatedDays,
      isActive: template.isActive
    };
  }

  cancelLearningPathTemplateEdit() {
    this.learningPathEditingId = null;
  }

  saveLearningPathTemplate() {
    if (this.learningPathEditingId === null) return;
    this.saving.set(true);
    const request$ = this.learningPathEditingId === 'new'
      ? this.service.createLearningPathTemplate(this.learningPathDraft)
      : this.service.updateLearningPathTemplate(this.learningPathEditingId, this.learningPathDraft);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.learningPathEditingId = null;
        this.load();
      },
      error: () => this.error.set('Öğrenme yolu şablonu kaydedilemedi.')
    });
  }

  deleteLearningPathTemplate(template: SpeedReadingLearningPathTemplate) {
    if (!globalThis.confirm(`“${template.name}” öğrenme yolu silinsin mi?`)) return;
    this.service.deleteLearningPathTemplate(template.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Öğrenme yolu silinemedi. Bağlı düğüm ve ilerlemeleri kontrol edin.')
    });
  }

  manageLearningPathNodes(template: SpeedReadingLearningPathTemplate) {
    this.selectedLearningPathId = template.id;
    this.selectedLearningPathTitle = template.name;
    this.learningPathNodeEditingId = null;
    this.service.getLearningPathTemplateDetails(template.id).subscribe({
      next: details => this.learningPathNodes.set(details.nodes),
      error: () => this.error.set('Öğrenme yolu düğümleri yüklenemedi.')
    });
  }

  closeLearningPathNodes() {
    this.selectedLearningPathId = null;
    this.selectedLearningPathTitle = '';
    this.learningPathNodeEditingId = null;
    this.learningPathNodes.set([]);
    this.closeLearningPathNodeRelations();
  }

  startCreateLearningPathNode() {
    if (this.selectedLearningPathId === null) return;
    this.learningPathNodeEditingId = 'new';
    this.learningPathNodeDraft = this.emptyLearningPathNodeDraft(this.selectedLearningPathId);
  }

  startEditLearningPathNode(node: SpeedReadingLearningPathNode) {
    if (this.selectedLearningPathId === null) return;
    this.learningPathNodeEditingId = node.id;
    this.learningPathNodeDraft = {
      templateId: this.selectedLearningPathId,
      parentNodeId: node.parentNodeId,
      nodeType: node.nodeType,
      title: node.title,
      contentType: node.contentType,
      contentId: node.contentId,
      order: node.order
    };
  }

  cancelLearningPathNodeEdit() {
    this.learningPathNodeEditingId = null;
  }

  saveLearningPathNode() {
    if (this.learningPathNodeEditingId === null || this.selectedLearningPathId === null) return;
    this.saving.set(true);
    const updateRequest: SpeedReadingLearningPathNodeUpdateRequest = {
      parentNodeId: this.learningPathNodeDraft.parentNodeId,
      nodeType: this.learningPathNodeDraft.nodeType,
      title: this.learningPathNodeDraft.title,
      contentType: this.learningPathNodeDraft.contentType,
      contentId: this.learningPathNodeDraft.contentId,
      order: this.learningPathNodeDraft.order
    };
    const request$ = this.learningPathNodeEditingId === 'new'
      ? this.service.createLearningPathNode({ ...this.learningPathNodeDraft, templateId: this.selectedLearningPathId })
      : this.service.updateLearningPathNode(this.learningPathNodeEditingId, updateRequest);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.learningPathNodeEditingId = null;
        this.manageLearningPathNodes({
          id: this.selectedLearningPathId!,
          name: this.selectedLearningPathTitle,
          targetAgeGroupConfigurationId: null,
          description: null,
          totalNodes: 0,
          estimatedDays: 1,
          isActive: true
        });
      },
      error: () => this.error.set('Öğrenme yolu düğümü kaydedilemedi.')
    });
  }

  deleteLearningPathNode(node: SpeedReadingLearningPathNode) {
    if (!globalThis.confirm(`“${node.title}” düğümü silinsin mi?`)) return;
    this.service.deleteLearningPathNode(node.id).subscribe({
      next: () => {
        if (this.selectedLearningPathId !== null) {
          this.manageLearningPathNodes({
            id: this.selectedLearningPathId,
            name: this.selectedLearningPathTitle,
            targetAgeGroupConfigurationId: null,
            description: null,
            totalNodes: 0,
            estimatedDays: 1,
            isActive: true
          });
        }
      },
      error: () => this.error.set('Öğrenme yolu düğümü silinemedi. Bağlı öğeleri kontrol edin.')
    });
  }

  manageLearningPathNodeRelations(node: SpeedReadingLearningPathNode) {
    this.selectedLearningPathNodeId = node.id;
    this.selectedLearningPathNodeTitle = node.title;
    this.relationContentKind = 'exercise';
    this.relationContentDraft = this.emptyLearningPathNodeContentDraft(node.id);
    this.relationPrerequisiteNodeId = null;
  }

  closeLearningPathNodeRelations() {
    this.selectedLearningPathNodeId = null;
    this.selectedLearningPathNodeTitle = '';
    this.relationPrerequisiteNodeId = null;
  }

  selectedLearningPathNode(): SpeedReadingLearningPathNode | undefined {
    return this.learningPathNodes().find(item => item.id === this.selectedLearningPathNodeId);
  }

  learningPathNodeTitle(nodeId: string): string {
    return this.learningPathNodes().find(item => item.id === nodeId)?.title ?? nodeId;
  }

  addLearningPathNodeContent() {
    if (this.selectedLearningPathNodeId === null) return;
    const request: SpeedReadingLearningPathNodeContentRequest = {
      nodeId: this.selectedLearningPathNodeId,
      exerciseId: this.relationContentKind === 'exercise' ? this.relationContentDraft.exerciseId : null,
      readingTextId: this.relationContentKind === 'readingText' ? this.relationContentDraft.readingTextId : null,
      description: this.relationContentDraft.description
    };
    this.saving.set(true);
    this.service.createLearningPathNodeContent(request).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.relationContentDraft = this.emptyLearningPathNodeContentDraft(this.selectedLearningPathNodeId!);
        this.reloadLearningPathNodes();
      },
      error: () => this.error.set('Düğüm içeriği eklenemedi.')
    });
  }

  deleteLearningPathNodeContent(contentId: string) {
    if (!globalThis.confirm('Bu düğüm içeriği silinsin mi?')) return;
    this.service.deleteLearningPathNodeContent(contentId).subscribe({
      next: () => this.reloadLearningPathNodes(),
      error: () => this.error.set('Düğüm içeriği silinemedi.')
    });
  }

  addLearningPathPrerequisite() {
    if (this.selectedLearningPathNodeId === null || this.relationPrerequisiteNodeId === null) return;
    this.saving.set(true);
    this.service.createLearningPathPrerequisite({
      nodeId: this.selectedLearningPathNodeId,
      prerequisiteNodeId: this.relationPrerequisiteNodeId
    }).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.relationPrerequisiteNodeId = null;
        this.reloadLearningPathNodes();
      },
      error: () => this.error.set('Önkoşul eklenemedi. Aynı şablon ve döngü kurallarını kontrol edin.')
    });
  }

  deleteLearningPathPrerequisite(prerequisiteNodeId: string) {
    if (this.selectedLearningPathNodeId === null || !globalThis.confirm('Bu önkoşul silinsin mi?')) return;
    this.service.deleteLearningPathPrerequisite(
      this.selectedLearningPathNodeId,
      prerequisiteNodeId
    ).subscribe({
      next: () => this.reloadLearningPathNodes(),
      error: () => this.error.set('Önkoşul silinemedi.')
    });
  }

  startCreateAchievement() {
    this.achievementEditingId = 'new';
    this.achievementDraft = this.emptyAchievementDraft();
  }

  startEditAchievement(achievement: SpeedReadingAchievement) {
    this.achievementEditingId = achievement.id;
    this.achievementDraft = {
      name: achievement.name,
      description: achievement.description,
      category: achievement.category,
      tier: achievement.tier,
      iconUrl: achievement.iconUrl,
      iconEmoji: achievement.iconEmoji,
      criteriaType: achievement.criteriaType,
      criteriaValue: achievement.criteriaValue,
      triggerType: achievement.triggerType,
      triggerValue: achievement.triggerValue,
      isRepeatable: achievement.isRepeatable,
      xpReward: achievement.xpReward,
      isActive: achievement.isActive,
      sortOrder: achievement.sortOrder
    };
  }

  cancelAchievementEdit() {
    this.achievementEditingId = null;
  }

  saveAchievement() {
    if (this.achievementEditingId === null) return;
    this.saving.set(true);
    const request$ = this.achievementEditingId === 'new'
      ? this.service.createAchievement(this.achievementDraft)
      : this.service.updateAchievement(this.achievementEditingId, this.achievementDraft);
    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.achievementEditingId = null;
        this.load();
      },
      error: () => this.error.set('Kazanım kaydedilemedi. Kriter JSON ve bağlı öğrenci kayıtlarını kontrol edin.')
    });
  }

  deleteAchievement(achievement: SpeedReadingAchievement) {
    if (!globalThis.confirm(`“${achievement.name}” kazanımı silinsin mi?`)) return;
    this.service.deleteAchievement(achievement.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Kazanım silinemedi. Öğrenci kazanım kayıtları varsa pasif hale getirin.')
    });
  }

  private reloadLearningPathNodes() {
    if (this.selectedLearningPathId === null) return;
    this.service.getLearningPathTemplateDetails(this.selectedLearningPathId).subscribe({
      next: details => this.learningPathNodes.set(details.nodes),
      error: () => this.error.set('Öğrenme yolu düğümleri yenilenemedi.')
    });
  }

  private emptyAchievementDraft(): SpeedReadingAchievementRequest {
    return {
      name: '',
      description: '',
      category: 'Reading',
      tier: 'Bronze',
      iconUrl: '',
      iconEmoji: '🏅',
      criteriaType: 'activity_count',
      criteriaValue: '{}',
      triggerType: null,
      triggerValue: null,
      isRepeatable: false,
      xpReward: 0,
      isActive: true,
      sortOrder: 0
    };
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

  private emptyQuestionDraft(readingTextId: string): SpeedReadingReadingQuestionRequest {
    return {
      readingTextId,
      questionText: '',
      type: 1,
      bloomLevel: 2,
      difficultyLevel: 0,
      explanation: '',
      optionA: '',
      optionB: '',
      optionC: '',
      optionD: '',
      correctAnswer: 'A',
      orderIndex: this.readingQuestions().length
    };
  }

  private emptyProgramDraft(): SpeedReadingProgramTemplateRequest {
    return {
      name: '',
      description: '',
      targetAgeGroupConfigurationId: '',
      minAssessmentScore: 0,
      maxAssessmentScore: 100,
      weeklyPatternJson: '{}',
      initialDifficultyLevel: 0,
      weeksPerDifficultyIncrease: 1,
      maxDifficultyLevel: 10,
      totalWeeks: 1,
      totalDays: 7,
      isActive: true,
      displayOrder: 0,
      programType: 0,
      examType: '',
      isAssessment: false
    };
  }

  private emptyLearningPathDraft(): SpeedReadingLearningPathTemplateRequest {
    return {
      name: '',
      targetAgeGroupConfigurationId: null,
      description: '',
      estimatedDays: 1,
      isActive: true
    };
  }

  private emptyLearningPathNodeDraft(templateId: string): SpeedReadingLearningPathNodeRequest {
    return {
      templateId,
      parentNodeId: null,
      nodeType: 'Exercise',
      title: '',
      contentType: null,
      contentId: null,
      order: this.learningPathNodes().length
    };
  }

  private emptyLearningPathNodeContentDraft(nodeId: string): SpeedReadingLearningPathNodeContentRequest {
    return {
      nodeId,
      exerciseId: null,
      readingTextId: null,
      description: ''
    };
  }
}
