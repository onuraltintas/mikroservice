import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Observable, finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { ToasterService } from '../../../core/services/toaster.service';
import {
  SpeedReadingAchievement,
  SpeedReadingAchievementRequest,
  SpeedReadingAdminService,
  SpeedReadingExercise,
  SpeedReadingExerciseRequest,
  SpeedReadingExerciseType,
  SpeedReadingExerciseTypeRequest,
  SpeedReadingLearningPathNode,
  SpeedReadingLearningPathNodeContent,
  SpeedReadingLearningPathNodeContentRequest,
  SpeedReadingLearningPathNodeContentUpdateRequest,
  SpeedReadingLearningPathNodeRequest,
  SpeedReadingLearningPathPrerequisiteRequest,
  SpeedReadingLearningPathTemplate,
  SpeedReadingLearningPathTemplateRequest,
  SpeedReadingProgramTemplate,
  SpeedReadingProgramTemplateRequest,
  SpeedReadingReadingText,
  SpeedReadingReadingQuestion,
  SpeedReadingReadingQuestionRequest,
  SpeedReadingReadingQuestionUpdateRequest,
  SpeedReadingReadingTextRequest,
  SpeedReadingReadingTextDetails,
  SpeedReadingAchievementStats
} from '../../../core/services/speed-reading-admin.service';

type CoreTab = 'content' | 'programs' | 'achievements';
type ContentTab = 'exercise-types' | 'exercises' | 'reading-texts';
type ProgramTab = 'programs' | 'learning-paths';

@Component({
  selector: 'app-speed-reading-catalog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  template: `
    <main class="space-y-6" aria-labelledby="catalog-title">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Hızlı Okuma servisi</p>
        <h1 id="catalog-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Katalog, program ve başarı yönetimi</h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">İçerik kataloglarını, öğrenme planlarını ve gamification tanımlarını yetki kapsamına göre yönetin.</p>
      </header>

      <nav class="ui-tab-list flex flex-wrap gap-2" aria-label="Katalog sekmeleri">
         @for (tab of visibleCoreTabs(); track tab.value) {
          <button type="button" (click)="selectCoreTab(tab.value)" [attr.aria-pressed]="selectedCoreTab() === tab.value" [class.bg-indigo-600]="selectedCoreTab() === tab.value" [class.text-white]="selectedCoreTab() === tab.value" class="ui-tab rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 dark:border-gray-600 dark:text-gray-200">{{ tab.label }}</button>
        }
      </nav>

      @if (error()) { <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }

      @if (selectedCoreTab() === 'content') {
        <section class="space-y-4">
          <div class="flex flex-wrap items-end justify-between gap-3">
            <div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">İçerik katalogları</h2><p class="muted">Egzersiz türleri, egzersizler ve okuma metinleri.</p></div>
            <button type="button" class="primary" (click)="startContentCreate()">{{ contentTab() === 'exercise-types' ? 'Yeni egzersiz türü' : contentTab() === 'exercises' ? 'Yeni egzersiz' : 'Yeni okuma metni' }}</button>
          </div>
          <nav class="ui-tab-list flex flex-wrap gap-2" aria-label="İçerik sekmeleri">
            @for (tab of contentTabs; track tab.value) { <button type="button" class="ui-tab secondary" [attr.aria-pressed]="contentTab() === tab.value" [class.bg-gray-100]="contentTab() === tab.value" (click)="selectContentTab(tab.value)">{{ tab.label }}</button> }
          </nav>

          @if (contentTab() === 'exercise-types') {
            @if (typeEditing()) {
              <div class="dialog-backdrop" role="presentation" (click)="typeEditing.set(false)">
                <form class="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="type-dialog-title" (ngSubmit)="saveExerciseType()" (click)="$event.stopPropagation()">
                  <div class="dialog-header"><div><p class="dialog-eyebrow">Egzersiz türü</p><h3 id="type-dialog-title">{{ typeEditingId ? 'Egzersiz türünü düzenle' : 'Yeni egzersiz türü' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="typeEditing.set(false)"><mat-icon>close</mat-icon></button></div>
                  <div class="dialog-body space-y-5">
                    <div class="form-grid"><label>Teknik ad<input [(ngModel)]="typeDraft.name" name="typeName" required maxlength="100" /></label><label>Öğrencide görünen ad<input [(ngModel)]="typeDraft.displayName" name="typeDisplayName" required maxlength="150" /></label><label>Motor tipi<input [(ngModel)]="typeDraft.engineType" name="typeEngine" required maxlength="100" /></label><label>Sıra<input type="number" [(ngModel)]="typeDraft.sortOrder" name="typeOrder" min="0" /></label><label class="wide">Açıklama<textarea [(ngModel)]="typeDraft.description" name="typeDescription" maxlength="1000"></textarea></label></div>
                    <fieldset><legend>Renk paleti</legend><div class="palette-grid">@for (color of exerciseTypeColors; track color.value) {<button type="button" class="color-swatch" [class.selected]="typeDraft.colorCode === color.value" [style.backgroundColor]="color.value" [attr.aria-label]="color.label" [attr.title]="color.label" [attr.aria-pressed]="typeDraft.colorCode === color.value" (click)="typeDraft.colorCode = color.value">@if (typeDraft.colorCode === color.value) {<mat-icon>check</mat-icon>}</button>}<label class="custom-color">Özel renk<input type="color" [(ngModel)]="typeDraft.colorCode" name="typeCustomColor" aria-label="Özel renk seç" /></label></div></fieldset>
                    <fieldset><legend>İkon seçimi</legend><div class="icon-grid">@for (icon of exerciseTypeIcons; track icon.value) {<button type="button" [class.selected]="typeDraft.iconName === icon.value" [attr.aria-pressed]="typeDraft.iconName === icon.value" [attr.title]="icon.label" (click)="typeDraft.iconName = icon.value"><mat-icon [style.color]="typeDraft.iconName === icon.value ? typeDraft.colorCode : null">{{ icon.value }}</mat-icon><span>{{ icon.label }}</span></button>}</div></fieldset>
                    <label class="check"><input type="checkbox" [(ngModel)]="typeDraft.isActive" name="typeActive" /> Aktif</label>
                  </div>
                  <div class="dialog-footer"><button type="button" class="secondary" (click)="typeEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">{{ saving() ? 'Kaydediliyor…' : 'Kaydet' }}</button></div>
                </form>
              </div>
            }
            <div class="data-card"><table class="data-table"><thead><tr><th>Tür</th><th>Motor</th><th>Sıra</th><th>Durum</th><th></th></tr></thead><tbody>
              @for (item of exerciseTypes().items; track item.id) { <tr><td><strong>{{ item.displayName }}</strong><div class="muted">{{ item.name }}</div></td><td>{{ item.engineType }}</td><td>{{ item.sortOrder }}</td><td>{{ item.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions"><button type="button" (click)="editExerciseType(item)">Düzenle</button><button type="button" class="danger" (click)="deleteExerciseType(item)">Sil</button></td></tr> } @empty { <tr><td colspan="5" class="empty">Egzersiz türü bulunamadı.</td></tr> }
            </tbody></table><div class="pager"><span>Toplam {{ exerciseTypes().totalCount }}</span><button class="secondary" type="button" (click)="changeContentPage(-1)" [disabled]="contentPage <= 1">Önceki</button><button class="secondary" type="button" (click)="changeContentPage(1)" [disabled]="contentPage >= totalPages(exerciseTypes())">Sonraki</button></div></div>
          }

          @if (contentTab() === 'exercises') {
            @if (exerciseEditing()) {
              <div class="dialog-backdrop" role="presentation" (click)="exerciseEditing.set(false)">
                <form class="dialog-panel dialog-panel-wide" role="dialog" aria-modal="true" aria-labelledby="exercise-dialog-title" (ngSubmit)="saveExercise()" (click)="$event.stopPropagation()">
                  <div class="dialog-header"><div><p class="dialog-eyebrow">Egzersiz</p><h3 id="exercise-dialog-title">{{ exerciseEditingId ? 'Egzersizi düzenle' : 'Yeni egzersiz' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="exerciseEditing.set(false)"><mat-icon>close</mat-icon></button></div>
                  <div class="dialog-body space-y-4">
                    <div class="form-grid"><label>Başlık<input [(ngModel)]="exerciseDraft.title" name="exerciseTitle" required maxlength="200" /></label><label>Egzersiz türü<select [(ngModel)]="exerciseDraft.exerciseTypeId" name="exerciseTypeId" required><option value="">Tür seçin</option>@for (type of availableExerciseTypes(); track type.id) {<option [value]="type.id">{{ type.displayName || type.name }}</option>}</select></label><label>Zorluk (0-10)<input type="number" [(ngModel)]="exerciseDraft.difficultyLevel" name="exerciseDifficulty" min="0" max="10" /></label><label>Yaş grubu ID<input [(ngModel)]="exerciseDraft.targetAgeGroupConfigurationId" name="exerciseAgeGroup" /></label><label class="wide">Açıklama<textarea [(ngModel)]="exerciseDraft.description" name="exerciseDescription" maxlength="5000"></textarea></label><label class="wide">Yapılandırma JSON<textarea class="code-editor" [(ngModel)]="exerciseDraft.configurationJson" name="exerciseConfig" required maxlength="100000"></textarea><span class="muted">Egzersiz motorunun çalışma ayarlarıdır. Geçerli JSON biçiminde olmalıdır.</span></label></div>
                    <div class="rounded-lg border border-indigo-200 bg-indigo-50 p-3 dark:border-indigo-900 dark:bg-indigo-950/30"><div class="flex flex-wrap items-center justify-between gap-2"><div><strong>Anlam korumalı adaptif akıcılık</strong><p class="muted">Dört aşamalı tekrar ve görülmemiş transfer metni şablonunu uygular.</p></div><button type="button" class="secondary" (click)="applyAdaptiveFluencyTemplate()">Şablonu uygula</button></div></div>
                    <label class="check"><input type="checkbox" [(ngModel)]="exerciseDraft.isActive" name="exerciseActive" /> Aktif</label>
                  </div>
                  <div class="dialog-footer"><button type="button" class="secondary" (click)="exerciseEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">{{ saving() ? 'Kaydediliyor…' : 'Kaydet' }}</button></div>
                </form>
              </div>
            }
            <div class="data-card"><table class="data-table"><thead><tr><th>Egzersiz</th><th>Tür</th><th>Zorluk</th><th>Durum</th><th></th></tr></thead><tbody>
              @for (item of exercises().items; track item.id) { <tr><td><strong>{{ item.title }}</strong><div class="muted">{{ item.description }}</div></td><td>{{ item.exerciseTypeName }}</td><td>{{ item.difficultyLevel }}</td><td>{{ item.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions"><button type="button" (click)="editExercise(item)">Düzenle</button><button type="button" class="danger" (click)="deleteExercise(item)">Sil</button></td></tr> } @empty { <tr><td colspan="5" class="empty">Egzersiz bulunamadı.</td></tr> }
            </tbody></table><div class="pager"><span>Toplam {{ exercises().totalCount }}</span><button class="secondary" type="button" (click)="changeContentPage(-1)" [disabled]="contentPage <= 1">Önceki</button><button class="secondary" type="button" (click)="changeContentPage(1)" [disabled]="contentPage >= totalPages(exercises())">Sonraki</button></div></div>
          }

          @if (contentTab() === 'reading-texts') {
            <div class="actions"><label class="secondary upload">CSV içe aktar<input type="file" accept=".csv,text/csv" (change)="onReadingTextImport($event, 'csv')" /></label><label class="secondary upload">Excel içe aktar<input type="file" accept=".xlsx,.xls" (change)="onReadingTextImport($event, 'excel')" /></label></div>
            @if (readingTextEditing()) {
              <div class="dialog-backdrop" role="presentation" (click)="closeReadingTextDialog()">
                <div class="dialog-panel dialog-panel-reading" role="dialog" aria-modal="true" aria-labelledby="reading-dialog-title" (click)="$event.stopPropagation()">
                  <div class="dialog-header"><div><p class="dialog-eyebrow">Okuma içeriği</p><h3 id="reading-dialog-title">{{ readingTextEditingId ? 'Okuma metnini ve sorularını düzenle' : 'Yeni okuma metni' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="closeReadingTextDialog()"><mat-icon>close</mat-icon></button></div>
                  <div class="reading-workspace">
                    <form class="reading-editor-pane" (ngSubmit)="saveReadingText()">
                      <div class="section-heading"><div><h4>Metin bilgileri</h4><p class="muted">Seviye, hedef kitle ve katalog bağlantıları</p></div><span class="status-chip" [class.active]="readingTextDraft.isActive">{{ readingTextDraft.isActive ? 'Aktif' : 'Pasif' }}</span></div>
                      <div class="form-grid"><label class="wide">Başlık<input [(ngModel)]="readingTextDraft.title" name="readingTitle" required maxlength="250" /></label><label>Kategori<input [(ngModel)]="readingTextDraft.category" name="readingCategory" required maxlength="100" placeholder="Bilim, tarih, doğa…" /></label><label>Dil<select [(ngModel)]="readingTextDraft.language" name="readingLanguage" required><option value="tr">Türkçe</option><option value="en">İngilizce</option><option value="de">Almanca</option><option value="fr">Fransızca</option></select></label><label>Önerilen min. seviye<input type="number" [(ngModel)]="readingTextDraft.recommendedMinLevel" name="readingMinLevel" min="0" /></label><label>Önerilen maks. seviye<input type="number" [(ngModel)]="readingTextDraft.recommendedMaxLevel" name="readingMaxLevel" min="0" /></label><label>Zorluk (0-10)<input type="number" [(ngModel)]="readingTextDraft.difficultyLevel" name="readingDifficulty" min="0" max="10" /></label><label>Bağlı egzersiz<select [(ngModel)]="readingTextDraft.exerciseId" name="readingExerciseId"><option [ngValue]="null">Ortak metin</option>@for (exercise of availableExercises(); track exercise.id) {<option [ngValue]="exercise.id">{{ exercise.title }}</option>}</select></label><label>Yaş grubu ID<input [(ngModel)]="readingTextDraft.targetAgeGroupConfigurationId" name="readingAgeGroup" placeholder="İsteğe bağlı" /></label><label>Etiketler<input [(ngModel)]="readingTags" name="readingTags" placeholder="bilim, uzay, keşif" /><span class="muted">Etiketleri virgülle ayırın.</span></label></div>
                      <div class="editor-section"><div class="section-heading"><div><h4>Metin içeriği</h4><p class="muted">Paragrafları boş satırla ayırabilirsiniz.</p></div></div><textarea class="reading-editor" [(ngModel)]="readingTextContent" name="readingContent" required maxlength="500000" placeholder="Okuma metnini buraya yazın…"></textarea><div class="text-metrics"><span><strong>{{ calculatedWordCount() }}</strong> kelime</span><span><strong>{{ paragraphCount() }}</strong> paragraf</span><span><strong>{{ estimatedReadingMinutes(150) }}</strong> dk normal okuma</span><span><strong>{{ estimatedReadingMinutes(300) }}</strong> dk hızlı okuma</span></div></div>
                      <label class="check"><input type="checkbox" [(ngModel)]="readingTextDraft.isActive" name="readingActive" /> Metin öğrenci kullanımına açık</label>
                      <div class="form-actions"><button type="button" class="secondary" (click)="closeReadingTextDialog()">Kapat</button><button class="primary" type="submit" [disabled]="saving()">{{ saving() ? 'Kaydediliyor…' : (readingTextEditingId ? 'Metni kaydet' : 'Metni oluştur') }}</button></div>
                    </form>

                    <aside class="question-pane">
                      <div class="section-heading"><div><h4>Anlama soruları</h4><p class="muted">Metin ve sorular aynı çalışma alanında yönetilir.</p></div>@if (readingTextEditingId) {<button type="button" class="primary" (click)="startQuestionCreate()"><mat-icon>add</mat-icon> Yeni soru</button>}</div>
                      @if (!readingTextEditingId) {
                        <div class="empty-state"><mat-icon>quiz</mat-icon><strong>Önce metni oluşturun</strong><p>Metin ilk kez kaydedildiğinde soru ekleme alanı otomatik olarak açılır.</p></div>
                      } @else {
                        @if (questionEditing()) {
                          <form class="question-form" (ngSubmit)="saveQuestion()"><div class="section-heading"><h4>{{ questionEditingId ? 'Soruyu düzenle' : 'Yeni soru' }}</h4><button type="button" class="icon-button" aria-label="Soru formunu kapat" (click)="questionEditing.set(false)"><mat-icon>close</mat-icon></button></div><label>Soru<textarea [(ngModel)]="questionDraft.questionText" name="questionText" required maxlength="5000"></textarea></label><div class="form-grid"><label>Soru türü<select [(ngModel)]="questionDraft.type" name="questionType" disabled><option [ngValue]="1">Çoktan seçmeli</option></select><span class="muted">Mevcut ölçüm motoru çoktan seçmeli soruları destekler.</span></label><label>Bloom seviyesi<select [(ngModel)]="questionDraft.bloomLevel" name="questionBloom"><option [ngValue]="1">Hatırlama</option><option [ngValue]="2">Anlama</option><option [ngValue]="3">Uygulama</option><option [ngValue]="4">Analiz</option><option [ngValue]="5">Değerlendirme</option><option [ngValue]="6">Üretme</option></select></label><label>Zorluk<input type="number" [(ngModel)]="questionDraft.difficultyLevel" name="questionDifficulty" min="0" max="10" /></label><label>Sıra<input type="number" [(ngModel)]="questionDraft.orderIndex" name="questionOrder" min="0" /></label></div><div class="option-grid"><label>A seçeneği<input [(ngModel)]="questionDraft.optionA" name="questionOptionA" required /></label><label>B seçeneği<input [(ngModel)]="questionDraft.optionB" name="questionOptionB" required /></label><label>C seçeneği<input [(ngModel)]="questionDraft.optionC" name="questionOptionC" required /></label><label>D seçeneği<input [(ngModel)]="questionDraft.optionD" name="questionOptionD" required /></label></div><label>Doğru cevap<select [(ngModel)]="questionDraft.correctAnswer" name="questionCorrect" required><option value="A">A</option><option value="B">B</option><option value="C">C</option><option value="D">D</option></select></label><label>Açıklama<textarea [(ngModel)]="questionDraft.explanation" name="questionExplanation" maxlength="5000" placeholder="Doğru cevabın nedenini açıklayın."></textarea></label><div class="form-actions"><button type="button" class="secondary" (click)="questionEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Soruyu kaydet</button></div></form>
                        }
                        <div class="question-list">@for (question of selectedReadingText()?.questions ?? []; track question.id) {<article class="question-card"><div class="question-number">{{ question.orderIndex }}</div><div class="min-w-0 flex-1"><strong>{{ question.questionText }}</strong><p class="muted">Doğru cevap: {{ question.correctAnswer }} · Bloom {{ question.bloomLevel }} · Zorluk {{ question.difficultyLevel }}</p></div><div class="actions"><button type="button" class="secondary" (click)="editQuestion(question)">Düzenle</button><button type="button" class="danger" (click)="deleteQuestion(question)">Sil</button></div></article>} @empty {<div class="empty-state"><mat-icon>quiz</mat-icon><strong>Henüz soru yok</strong><p>Anlama başarısını ölçmek için bu metne soru ekleyin.</p></div>}</div>
                      }
                    </aside>
                  </div>
                </div>
              </div>
            }
            <div class="data-card"><table class="data-table"><thead><tr><th>Başlık</th><th>Kelime</th><th>Kategori</th><th>Zorluk</th><th></th></tr></thead><tbody>
              @for (item of readingTexts(); track item.id) { <tr><td>{{ item.title }}</td><td>{{ item.wordCount }}</td><td>{{ item.category }}</td><td>{{ item.difficultyLevel }}</td><td class="actions"><button type="button" (click)="editReadingText(item)">Düzenle / sorular</button><button type="button" (click)="exportReadingText(item, 'pdf')">PDF</button><button type="button" (click)="exportReadingText(item, 'docx')">DOCX</button><button type="button" class="danger" (click)="deleteReadingText(item)">Sil</button></td></tr> } @empty { <tr><td colspan="5" class="empty">Okuma metni bulunamadı.</td></tr> }
            </tbody></table></div>
          }
        </section>
      }

      @if (selectedCoreTab() === 'programs') {
        <section class="space-y-4">
          <div class="flex flex-wrap items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">Program ve öğrenme yolları</h2><p class="muted">Program şablonları ile düğüm tabanlı öğrenme yolları.</p></div><button type="button" class="primary" (click)="startProgramCreate()">{{ programTab() === 'programs' ? 'Yeni program' : 'Yeni öğrenme yolu' }}</button></div>
          <nav class="ui-tab-list flex flex-wrap gap-2" aria-label="Program sekmeleri">@for (tab of programTabs; track tab.value) { <button type="button" class="ui-tab secondary" [attr.aria-pressed]="programTab() === tab.value" [class.bg-gray-100]="programTab() === tab.value" (click)="selectProgramTab(tab.value)">{{ tab.label }}</button> }</nav>
          @if (programTab() === 'programs') {
            @if (programEditing()) { <form class="form-card" (ngSubmit)="saveProgram()"><h3>{{ programEditingId ? 'Programı düzenle' : 'Yeni program' }}</h3><div class="form-grid"><label>Ad<input [(ngModel)]="programDraft.name" name="programName" required maxlength="200" /></label><label>Yaş grubu ID<input [(ngModel)]="programDraft.targetAgeGroupConfigurationId" name="programAgeGroup" required /></label><label>Min. puan<input type="number" [(ngModel)]="programDraft.minAssessmentScore" name="programMinScore" /></label><label>Maks. puan<input type="number" [(ngModel)]="programDraft.maxAssessmentScore" name="programMaxScore" /></label><label>İlk zorluk<input type="number" [(ngModel)]="programDraft.initialDifficultyLevel" name="programInitialDifficulty" /></label><label>Maks. zorluk<input type="number" [(ngModel)]="programDraft.maxDifficultyLevel" name="programMaxDifficulty" /></label><label>Toplam hafta<input type="number" [(ngModel)]="programDraft.totalWeeks" name="programWeeks" min="1" /></label><label>Toplam gün<input type="number" [(ngModel)]="programDraft.totalDays" name="programDays" min="1" /></label><label class="wide">Açıklama<textarea [(ngModel)]="programDraft.description" name="programDescription" maxlength="5000"></textarea></label><label class="wide">Haftalık plan JSON<textarea [(ngModel)]="programDraft.weeklyPatternJson" name="programPattern" required maxlength="100000"></textarea></label><label class="check"><input type="checkbox" [(ngModel)]="programDraft.isActive" name="programActive" /> Aktif</label><label class="check"><input type="checkbox" [(ngModel)]="programDraft.isAssessment" name="programAssessment" /> Seviye tespit programı</label></div><div class="form-actions"><button type="button" class="secondary" (click)="programEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form> }
            <div class="data-card"><table class="data-table"><thead><tr><th>Program</th><th>Hafta/gün</th><th>Zorluk</th><th>Durum</th><th></th></tr></thead><tbody>@for (item of programs(); track item.id) {<tr><td>{{ item.name }}<div class="muted">{{ item.description }}</div></td><td>{{ item.totalWeeks }} / {{ item.totalDays }}</td><td>{{ item.initialDifficultyLevel }}-{{ item.maxDifficultyLevel }}</td><td>{{ item.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions"><button type="button" (click)="editProgram(item)">Düzenle</button><button type="button" (click)="cloneProgram(item)">Kopyala</button><button type="button" class="danger" (click)="deleteProgram(item)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Program bulunamadı.</td></tr>}</tbody></table></div>
          }
          @if (programTab() === 'learning-paths') {
            @if (learningPathEditing()) { <form class="form-card" (ngSubmit)="saveLearningPath()"><h3>{{ learningPathEditingId ? 'Öğrenme yolunu düzenle' : 'Yeni öğrenme yolu' }}</h3><div class="form-grid"><label>Ad<input [(ngModel)]="learningPathDraft.name" name="pathName" required maxlength="200" /></label><label>Yaş grubu ID<input [(ngModel)]="learningPathDraft.targetAgeGroupConfigurationId" name="pathAgeGroup" /></label><label>Tahmini gün<input type="number" [(ngModel)]="learningPathDraft.estimatedDays" name="pathDays" min="1" /></label><label class="wide">Açıklama<textarea [(ngModel)]="learningPathDraft.description" name="pathDescription" maxlength="5000"></textarea></label><label class="check"><input type="checkbox" [(ngModel)]="learningPathDraft.isActive" name="pathActive" /> Aktif</label></div><div class="form-actions"><button type="button" class="secondary" (click)="learningPathEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form> }
            <div class="data-card"><table class="data-table"><thead><tr><th>Yol</th><th>Düğüm</th><th>Tahmini gün</th><th></th></tr></thead><tbody>@for (item of learningPaths(); track item.id) {<tr><td>{{ item.name }}</td><td>{{ item.totalNodes }}</td><td>{{ item.estimatedDays }}</td><td class="actions"><button type="button" (click)="selectLearningPath(item)">Düğümleri gör</button><button type="button" (click)="editLearningPath(item)">Düzenle</button><button type="button" class="danger" (click)="deleteLearningPath(item)">Sil</button></td></tr>} @empty {<tr><td colspan="4" class="empty">Öğrenme yolu bulunamadı.</td></tr>}</tbody></table></div>
            @if (selectedPath(); as details) {
              <div class="data-card">
                <div class="flex items-center justify-between"><strong>{{ details.template.name }} - düğümler</strong><button type="button" class="primary" (click)="startNodeCreate()">Yeni düğüm</button></div>
                @if (nodeEditing()) {<form class="form-card" (ngSubmit)="saveNode()"><h3>{{ nodeEditingId ? 'Düğümü düzenle' : 'Yeni düğüm' }}</h3><div class="form-grid"><label>Başlık<input [(ngModel)]="nodeDraft.title" name="nodeTitle" required maxlength="200" /></label><label>Tür<input [(ngModel)]="nodeDraft.nodeType" name="nodeType" required maxlength="100" /></label><label>Üst düğüm ID<input [(ngModel)]="nodeDraft.parentNodeId" name="nodeParent" /></label><label>Sıra<input type="number" [(ngModel)]="nodeDraft.order" name="nodeOrder" /></label><label>İçerik türü<input [(ngModel)]="nodeDraft.contentType" name="nodeContentType" /></label><label>İçerik ID<input [(ngModel)]="nodeDraft.contentId" name="nodeContentId" /></label></div><div class="form-actions"><button type="button" class="secondary" (click)="nodeEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form>}
                <table class="data-table"><thead><tr><th>Başlık</th><th>Tür</th><th>Sıra</th><th>İçerik</th><th>Önkoşul</th><th></th></tr></thead><tbody>@for (node of details.nodes; track node.id) {<tr><td>{{ node.title }}</td><td>{{ node.nodeType }}</td><td>{{ node.order }}</td><td>{{ node.contents.length }}</td><td>{{ node.prerequisiteNodeIds.length }}</td><td class="actions"><button type="button" (click)="manageNode(node)">İçerik/önkoşul</button><button type="button" (click)="editNode(node)">Düzenle</button><button type="button" class="danger" (click)="deleteNode(node)">Sil</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Düğüm bulunamadı.</td></tr>}</tbody></table>
                @if (activeNode(); as node) {
                  <div class="form-card"><div class="flex items-center justify-between"><h3>{{ node.title }} - içerik ve önkoşullar</h3><button type="button" class="secondary" (click)="activeNode.set(null)">Kapat</button></div>
                    <div class="data-card"><h4>Node içerikleri</h4><div class="actions"><button type="button" class="primary" (click)="startNodeContentCreate()">Yeni içerik</button></div>@if (nodeContentEditing()) {<form class="form-card" (ngSubmit)="saveNodeContent()"><div class="form-grid"><label>Egzersiz ID<input [(ngModel)]="nodeContentDraft.exerciseId" name="nodeContentExercise" /></label><label>Okuma metni ID<input [(ngModel)]="nodeContentDraft.readingTextId" name="nodeContentReading" /></label><label class="wide">Açıklama<textarea [(ngModel)]="nodeContentDraft.description" name="nodeContentDescription" maxlength="2000"></textarea></label></div><div class="form-actions"><button type="button" class="secondary" (click)="nodeContentEditing.set(false)">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div></form>}<table class="data-table"><thead><tr><th>Egzersiz</th><th>Okuma metni</th><th>Açıklama</th><th></th></tr></thead><tbody>@for (content of node.contents; track content.id) {<tr><td>{{ content.exerciseId || '-' }}</td><td>{{ content.readingTextId || '-' }}</td><td>{{ content.description }}</td><td class="actions"><button type="button" (click)="editNodeContent(content)">Düzenle</button><button type="button" class="danger" (click)="deleteNodeContent(content)">Sil</button></td></tr>} @empty {<tr><td colspan="4" class="empty">İçerik bulunamadı.</td></tr>}</tbody></table></div>
                    <div class="data-card"><h4>Önkoşullar</h4><div class="form-actions"><select [(ngModel)]="prerequisiteNodeId" name="prerequisiteNode"><option value="">Önkoşul düğümü seçin</option>@for (candidate of details.nodes; track candidate.id) {@if (candidate.id !== node.id) {<option [value]="candidate.id">{{ candidate.title }}</option>}}</select><button type="button" class="primary" (click)="addPrerequisite()" [disabled]="!prerequisiteNodeId || saving()">Ekle</button></div>@for (prerequisiteId of node.prerequisiteNodeIds; track prerequisiteId) {<div class="question-row flex items-center justify-between"><span>{{ nodeTitle(details.nodes, prerequisiteId) }}</span><button type="button" class="danger" (click)="deletePrerequisite(prerequisiteId)">Kaldır</button></div>} @empty {<p class="empty">Önkoşul bulunamadı.</p>}</div>
                  </div>
                }
              </div>
            }
          }
        </section>
      }

      @if (selectedCoreTab() === 'achievements') {
        <section class="space-y-4"><div class="flex items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">Başarı tanımları</h2><p class="muted">Rozet, kriter, tekrar davranışı ve XP ödülü.</p></div><button type="button" class="primary" (click)="startAchievementCreate()">Yeni başarı</button></div>
          @if (achievementStats(); as stats) { <div class="grid grid-cols-2 gap-3 md:grid-cols-4"><div class="metric"><span>Toplam</span><strong>{{ stats.totalCount }}</strong></div><div class="metric"><span>Aktif</span><strong>{{ stats.activeCount }}</strong></div><div class="metric"><span>Bronz/Gümüş</span><strong>{{ stats.bronzeCount }} / {{ stats.silverCount }}</strong></div><div class="metric"><span>Altın/Diamond</span><strong>{{ stats.goldCount }} / {{ stats.diamondCount }}</strong></div></div> }
          @if (achievementEditing()) { <form class="form-card" (ngSubmit)="saveAchievement()"><h3>{{ achievementEditingId ? 'Başarıyı düzenle' : 'Yeni başarı' }}</h3><div class="form-grid"><label>Ad<input [(ngModel)]="achievementDraft.name" name="achievementName" required maxlength="200" /></label><label>Kategori<input [(ngModel)]="achievementDraft.category" name="achievementCategory" required maxlength="100" /></label><label>Seviye<input [(ngModel)]="achievementDraft.tier" name="achievementTier" required maxlength="100" /></label><label>Emoji<input [(ngModel)]="achievementDraft.iconEmoji" name="achievementEmoji" maxlength="20" /></label><label>Kriter türü<input [(ngModel)]="achievementDraft.criteriaType" name="achievementCriteriaType" required /></label><label>Kriter değeri<input [(ngModel)]="achievementDraft.criteriaValue" name="achievementCriteriaValue" required /></label><label>Tetikleyici<input [(ngModel)]="achievementDraft.triggerType" name="achievementTriggerType" /></label><label>XP ödülü<input type="number" [(ngModel)]="achievementDraft.xpReward" name="achievementXp" min="0" /></label><label class="wide">Açıklama<textarea [(ngModel)]="achievementDraft.description" name="achievementDescription" required maxlength="5000"></textarea></label><label class="check"><input type="checkbox" [(ngModel)]="achievementDraft.isRepeatable" name="achievementRepeatable" /> Tekrarlanabilir</label><label class="check"><input type="checkbox" [(ngModel)]="achievementDraft.isActive" name="achievementActive" /> Aktif</label></div><div class="form-actions"><button type="button" class="secondary" (click)="achievementEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form> }
          <div class="data-card"><table class="data-table"><thead><tr><th>Başarı</th><th>Kriter</th><th>XP</th><th>Kullanıcı</th><th>Durum</th><th></th></tr></thead><tbody>@for (item of achievements().items; track item.id) {<tr><td><strong>{{ item.name }}</strong><div class="muted">{{ item.category }} · {{ item.tier }}</div></td><td>{{ item.criteriaType }}: {{ item.criteriaValue }}</td><td>{{ item.xpReward }}</td><td>{{ item.unlockedByUsersCount }}</td><td>{{ item.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions"><button type="button" (click)="editAchievement(item)">Düzenle</button><button type="button" class="danger" (click)="deleteAchievement(item)">Sil</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Başarı bulunamadı.</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ achievements().totalCount }}</span><button class="secondary" type="button" (click)="changeAchievementPage(-1)" [disabled]="achievementPage <= 1">Önceki</button><button class="secondary" type="button" (click)="changeAchievementPage(1)" [disabled]="achievementPage >= totalPages(achievements())">Sonraki</button></div></div>
        </section>
      }
      @if (saving()) { <div role="status" class="text-center text-sm text-gray-500">Kaydediliyor…</div> }
    </main>
  `,
  styles: [`
    :host { display: block; }
    .muted { color: var(--ui-text-muted); font-size: .85rem; }
    .data-card, .form-card { border: 1px solid var(--ui-border); border-radius: .75rem; padding: 1rem; background: var(--ui-surface); }
    .form-card { display: grid; gap: 1rem; }
    .form-grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr)); }
    label, fieldset { display: grid; gap: .35rem; font-size: .875rem; font-weight: 500; color: var(--ui-text); }
    fieldset { border: 0; padding: 0; }
    legend { margin-bottom: .65rem; font-weight: 700; }
    input, textarea, select { width: 100%; border: 1px solid var(--ui-border-strong); border-radius: .5rem; padding: .65rem .75rem; background: var(--ui-surface); font: inherit; color: inherit; }
    textarea { min-height: 5rem; resize: vertical; }
    input:focus, textarea:focus, select:focus { border-color: var(--ui-brand); outline: 3px solid color-mix(in srgb, var(--ui-brand) 16%, transparent); }
    .wide { grid-column: 1 / -1; }
    .check { display: flex; align-items: center; gap: .5rem; }
    .check input { width: auto; }
    .primary, .secondary, .danger { display: inline-flex; align-items: center; justify-content: center; gap: .35rem; border-radius: .5rem; padding: .55rem .8rem; font-size: .875rem; font-weight: 600; }
    .primary { background: var(--ui-brand); color: var(--ui-brand-contrast); }
    .secondary { border: 1px solid var(--ui-border-strong); }
    .danger { color: var(--ui-danger); }
    .actions, .form-actions, .pager { display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; }
    .form-actions { justify-content: flex-end; }
    .actions { white-space: normal; }
    .upload { cursor: pointer; }
    .upload input { display: none; }
    .metric { display: grid; gap: .25rem; border: 1px solid var(--ui-border); border-radius: .5rem; padding: .75rem; }
    .metric span { color: var(--ui-text-muted); font-size: .75rem; }
    .metric strong { font-size: 1.25rem; }
    .data-table { width: 100%; text-align: left; font-size: .875rem; }
    .data-table th { border-bottom: 1px solid var(--ui-border); padding: .625rem .75rem; font-size: .7rem; text-transform: uppercase; color: var(--ui-text-muted); }
    .data-table td { border-bottom: 1px solid var(--ui-border); padding: .625rem .75rem; color: var(--ui-text); vertical-align: top; }
    .empty { padding: 2rem; text-align: center; color: var(--ui-text-muted); }
    .pager { justify-content: space-between; margin-top: .75rem; font-size: .8rem; color: var(--ui-text-muted); }
    .question-row { border-top: 1px solid var(--ui-border); padding: .5rem 0; }
    .dialog-backdrop { position: fixed; inset: 0; z-index: 1000; display: grid; place-items: center; padding: 1rem; background: rgb(15 23 42 / .68); backdrop-filter: blur(4px); }
    .dialog-panel { display: flex; width: min(46rem, 100%); max-height: calc(100vh - 2rem); flex-direction: column; overflow: hidden; border: 1px solid var(--ui-border); border-radius: 1rem; background: var(--ui-surface); color: var(--ui-text); box-shadow: 0 24px 70px rgb(15 23 42 / .28); }
    .dialog-panel-wide { width: min(60rem, 100%); }
    .dialog-panel-reading { width: min(92rem, 100%); height: min(52rem, calc(100vh - 2rem)); }
    .dialog-header, .dialog-footer { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 1rem 1.25rem; border-color: var(--ui-border); }
    .dialog-header { border-bottom-width: 1px; }
    .dialog-footer { justify-content: flex-end; border-top-width: 1px; }
    .dialog-header h3 { font-size: 1.15rem; font-weight: 700; }
    .dialog-eyebrow { color: var(--ui-brand); font-size: .7rem; font-weight: 800; letter-spacing: .09em; text-transform: uppercase; }
    .dialog-body { overflow: auto; padding: 1.25rem; }
    .icon-button { display: inline-grid; flex: none; height: 2.25rem; width: 2.25rem; place-items: center; border-radius: 999px; color: var(--ui-text-muted); }
    .icon-button:hover { background: color-mix(in srgb, var(--ui-text) 8%, transparent); color: var(--ui-text); }
    .palette-grid { display: flex; flex-wrap: wrap; align-items: center; gap: .65rem; }
    .color-swatch { display: grid; height: 2.5rem; width: 2.5rem; place-items: center; border: 3px solid transparent; border-radius: 999px; color: white; box-shadow: 0 1px 4px rgb(15 23 42 / .2); }
    .color-swatch.selected { border-color: var(--ui-text); transform: scale(1.08); }
    .custom-color { display: flex; height: 2.5rem; align-items: center; gap: .5rem; border: 1px solid var(--ui-border-strong); border-radius: .6rem; padding: .35rem .55rem; }
    .custom-color input { height: 1.7rem; width: 2rem; cursor: pointer; border: 0; padding: 0; }
    .icon-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(6rem, 1fr)); gap: .5rem; max-height: 18rem; overflow: auto; padding: .15rem; }
    .icon-grid button { display: grid; min-height: 4.5rem; place-items: center; gap: .2rem; border: 1px solid var(--ui-border); border-radius: .65rem; padding: .5rem; color: var(--ui-text-muted); }
    .icon-grid button span { font-size: .7rem; }
    .icon-grid button.selected { border-color: var(--ui-brand); background: color-mix(in srgb, var(--ui-brand) 9%, transparent); color: var(--ui-text); }
    .code-editor { min-height: 13rem; font-family: ui-monospace, SFMono-Regular, Consolas, monospace; font-size: .78rem; line-height: 1.5; }
    .reading-workspace { display: grid; min-height: 0; flex: 1; grid-template-columns: minmax(0, 1.35fr) minmax(24rem, .85fr); }
    .reading-editor-pane, .question-pane { min-height: 0; overflow: auto; padding: 1.25rem; }
    .reading-editor-pane { display: grid; align-content: start; gap: 1.25rem; border-right: 1px solid var(--ui-border); }
    .question-pane { background: color-mix(in srgb, var(--ui-surface) 94%, var(--ui-brand)); }
    .section-heading { display: flex; align-items: start; justify-content: space-between; gap: 1rem; }
    .section-heading h4 { font-weight: 700; color: var(--ui-text); }
    .editor-section { display: grid; gap: .65rem; }
    .reading-editor { min-height: 22rem; line-height: 1.75; }
    .text-metrics { display: flex; flex-wrap: wrap; gap: .45rem; }
    .text-metrics span, .status-chip { border-radius: 999px; background: color-mix(in srgb, var(--ui-text) 7%, transparent); padding: .3rem .6rem; font-size: .75rem; color: var(--ui-text-muted); }
    .status-chip.active { background: rgb(16 185 129 / .14); color: rgb(5 150 105); }
    .question-form { display: grid; gap: .8rem; margin-top: 1rem; border: 1px solid color-mix(in srgb, var(--ui-brand) 28%, var(--ui-border)); border-radius: .8rem; padding: 1rem; background: var(--ui-surface); }
    .option-grid { display: grid; grid-template-columns: 1fr 1fr; gap: .75rem; }
    .question-list { display: grid; gap: .65rem; margin-top: 1rem; }
    .question-card { display: flex; align-items: flex-start; gap: .65rem; border: 1px solid var(--ui-border); border-radius: .75rem; padding: .8rem; background: var(--ui-surface); }
    .question-number { display: grid; flex: none; height: 1.8rem; width: 1.8rem; place-items: center; border-radius: 999px; background: color-mix(in srgb, var(--ui-brand) 13%, transparent); color: var(--ui-brand); font-size: .75rem; font-weight: 800; }
    .empty-state { display: grid; place-items: center; gap: .5rem; min-height: 14rem; padding: 2rem; text-align: center; color: var(--ui-text-muted); }
    .empty-state mat-icon { height: 3rem; width: 3rem; font-size: 3rem; opacity: .55; }
    @media (max-width: 900px) { .dialog-panel-reading { height: calc(100vh - 1rem); } .reading-workspace { grid-template-columns: 1fr; overflow: auto; } .reading-editor-pane, .question-pane { overflow: visible; } .reading-editor-pane { border-right: 0; border-bottom: 1px solid var(--ui-border); } }
    @media (max-width: 640px) { .form-grid, .option-grid { grid-template-columns: 1fr; } .wide { grid-column: auto; } .dialog-backdrop { padding: .5rem; } .dialog-panel { max-height: calc(100vh - 1rem); } .dialog-panel-reading { height: calc(100vh - 1rem); } .question-card { flex-wrap: wrap; } }
  `]
})
export class SpeedReadingCatalogComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService); private readonly route = inject(ActivatedRoute); private readonly authService = inject(AuthService); private readonly toaster = inject(ToasterService);
  readonly coreTabs: { value: CoreTab; label: string }[] = [{ value: 'content', label: 'İçerik katalogları' }, { value: 'programs', label: 'Programlar' }, { value: 'achievements', label: 'Başarılar' }];
  readonly visibleCoreTabs = computed(() => this.coreTabs.filter(tab => tab.value === 'content' ? this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingContentManage) : tab.value === 'programs' ? this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingProgramManage) : this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingGamificationManage)));
  readonly contentTabs: { value: ContentTab; label: string }[] = [{ value: 'exercise-types', label: 'Egzersiz türleri' }, { value: 'exercises', label: 'Egzersizler' }, { value: 'reading-texts', label: 'Okuma metinleri' }];
  readonly programTabs: { value: ProgramTab; label: string }[] = [{ value: 'programs', label: 'Program şablonları' }, { value: 'learning-paths', label: 'Öğrenme yolları' }];
  readonly selectedCoreTab = signal<CoreTab>('content'); readonly contentTab = signal<ContentTab>('exercise-types'); readonly programTab = signal<ProgramTab>('programs'); readonly loading = signal(false); readonly saving = signal(false); readonly error = signal('');
  readonly exerciseTypes = signal<{ items: SpeedReadingExerciseType[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 }); readonly availableExerciseTypes = signal<SpeedReadingExerciseType[]>([]); readonly exercises = signal<{ items: SpeedReadingExercise[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }); readonly availableExercises = signal<SpeedReadingExercise[]>([]); readonly readingTexts = signal<SpeedReadingReadingText[]>([]); readonly selectedReadingText = signal<SpeedReadingReadingTextDetails | null>(null); readonly programs = signal<SpeedReadingProgramTemplate[]>([]); readonly learningPaths = signal<SpeedReadingLearningPathTemplate[]>([]); readonly selectedPath = signal<{ template: SpeedReadingLearningPathTemplate; nodes: SpeedReadingLearningPathNode[] } | null>(null); readonly activeNode = signal<SpeedReadingLearningPathNode | null>(null); readonly achievements = signal<{ items: SpeedReadingAchievement[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 50 });
  readonly typeEditing = signal(false); readonly exerciseEditing = signal(false); readonly readingTextEditing = signal(false); readonly questionEditing = signal(false); readonly programEditing = signal(false); readonly learningPathEditing = signal(false); readonly nodeEditing = signal(false); readonly nodeContentEditing = signal(false); readonly achievementEditing = signal(false); readonly achievementStats = signal<SpeedReadingAchievementStats | null>(null);
  typeEditingId: string | null = null; exerciseEditingId: string | null = null; readingTextEditingId: string | null = null; questionEditingId: string | null = null; programEditingId: string | null = null; learningPathEditingId: string | null = null; nodeEditingId: string | null = null; nodeContentEditingId: string | null = null; achievementEditingId: string | null = null; prerequisiteNodeId = ''; contentPage = 1; achievementPage = 1;
  typeDraft: SpeedReadingExerciseTypeRequest = this.emptyType(); exerciseDraft: SpeedReadingExerciseRequest = this.emptyExercise(); readingTextDraft: SpeedReadingReadingTextRequest = this.emptyReadingText(); readingTextContent = ''; readingTags = ''; questionDraft: SpeedReadingReadingQuestionRequest = this.emptyQuestion(''); programDraft: SpeedReadingProgramTemplateRequest = this.emptyProgram(); learningPathDraft: SpeedReadingLearningPathTemplateRequest = this.emptyLearningPath(); nodeDraft: SpeedReadingLearningPathNodeRequest = this.emptyNode(''); nodeContentDraft: SpeedReadingLearningPathNodeContentRequest = this.emptyNodeContent(''); achievementDraft: SpeedReadingAchievementRequest = this.emptyAchievement();
  readonly exerciseTypeColors = [
    { value: '#2563eb', label: 'Mavi' }, { value: '#4f46e5', label: 'Çivit mavisi' }, { value: '#7c3aed', label: 'Mor' },
    { value: '#db2777', label: 'Pembe' }, { value: '#dc2626', label: 'Kırmızı' }, { value: '#ea580c', label: 'Turuncu' },
    { value: '#ca8a04', label: 'Sarı' }, { value: '#16a34a', label: 'Yeşil' }, { value: '#0d9488', label: 'Turkuaz' },
    { value: '#0891b2', label: 'Camgöbeği' }, { value: '#475569', label: 'Füme' }, { value: '#111827', label: 'Siyah' }
  ] as const;
  readonly exerciseTypeIcons = [
    { value: 'visibility', label: 'Göz takibi' }, { value: 'center_focus_strong', label: 'Odak' }, { value: 'speed', label: 'Hız' },
    { value: 'timer', label: 'Süre' }, { value: 'menu_book', label: 'Okuma' }, { value: 'auto_stories', label: 'Metin' },
    { value: 'psychology', label: 'Zihin' }, { value: 'memory', label: 'Hafıza' }, { value: 'travel_explore', label: 'Tarama' },
    { value: 'swap_horiz', label: 'Yatay takip' }, { value: 'swap_vert', label: 'Dikey takip' }, { value: 'zoom_out_map', label: 'Görüş alanı' },
    { value: 'filter_center_focus', label: 'Merkezleme' }, { value: 'format_line_spacing', label: 'Satır takibi' }, { value: 'text_fields', label: 'Kelime' },
    { value: 'grid', label: 'Izgara' }, { value: 'grid_view', label: 'Izgara görünümü' }, { value: 'flash_on', label: 'Hızlı gösterim' },
    { value: 'track_changes', label: 'Hedef' }, { value: 'trending_up', label: 'Gelişim' }, { value: 'school', label: 'Öğrenme' },
    { value: 'lightbulb', label: 'Anlama' }, { value: 'fitness_center', label: 'Egzersiz' }, { value: 'extension', label: 'Eşleştirme' },
    { value: 'category', label: 'Genel' }
  ] as const;

  ngOnInit(): void { const defaultTab = this.route.snapshot.data['defaultTab'] as CoreTab | undefined; if (defaultTab) this.selectedCoreTab.set(defaultTab); this.loadCurrentTab(); }
  selectCoreTab(tab: CoreTab): void { this.selectedCoreTab.set(tab); this.error.set(''); this.loadCurrentTab(); }
  selectContentTab(tab: ContentTab): void { this.contentTab.set(tab); this.cancelContentEdit(); this.loadCurrentTab(); }
  selectProgramTab(tab: ProgramTab): void { this.programTab.set(tab); this.cancelProgramEdit(); this.loadCurrentTab(); }
  loadCurrentTab(): void { if (this.selectedCoreTab() === 'content') { if (this.contentTab() === 'exercise-types') this.loadExerciseTypes(); if (this.contentTab() === 'exercises') this.loadExercises(); if (this.contentTab() === 'reading-texts') this.loadReadingTexts(); } else if (this.selectedCoreTab() === 'programs') { if (this.programTab() === 'programs') this.loadPrograms(); else this.loadLearningPaths(); } else this.loadAchievements(); }
  startContentCreate(): void { if (this.contentTab() === 'exercise-types') { this.typeEditingId = null; this.typeDraft = this.emptyType(); this.typeEditing.set(true); } else if (this.contentTab() === 'exercises') { this.exerciseEditingId = null; this.exerciseDraft = this.emptyExercise(); this.exerciseEditing.set(true); } else { this.readingTextEditingId = null; this.readingTextDraft = this.emptyReadingText(); this.readingTextContent = ''; this.readingTags = ''; this.selectedReadingText.set(null); this.questionEditing.set(false); this.readingTextEditing.set(true); } }
  loadExerciseTypes(): void { this.service.getExerciseTypes(this.contentPage).subscribe({ next: value => this.exerciseTypes.set(value), error: () => this.error.set('Egzersiz türleri yüklenemedi.') }); }
  editExerciseType(item: SpeedReadingExerciseType): void { this.typeEditingId = item.id; this.typeDraft = { name: item.name, displayName: item.displayName, description: item.description, iconName: item.iconName, colorCode: item.colorCode, sortOrder: item.sortOrder, isActive: item.isActive, engineType: item.engineType, categoryId: item.categoryId }; this.typeEditing.set(true); }
  saveExerciseType(): void { const action = this.typeEditingId ? this.service.updateExerciseType(this.typeEditingId, this.typeDraft) : this.service.createExerciseType(this.typeDraft); this.run(action, () => { this.typeEditing.set(false); this.loadExerciseTypes(); }, 'Egzersiz türü kaydedilemedi.'); }
  async deleteExerciseType(item: SpeedReadingExerciseType): Promise<void> { if (!await this.toaster.confirm('Bu egzersiz türü silinsin mi?', { title: 'Egzersiz türünü sil' })) return; this.run(this.service.deleteExerciseType(item.id), () => this.loadExerciseTypes(), 'Egzersiz türü silinemedi.'); }
  loadExercises(): void { this.service.getExercises(this.contentPage).subscribe({ next: value => this.exercises.set(value), error: () => this.error.set('Egzersizler yüklenemedi.') }); this.service.getAllExerciseTypes().subscribe({ next: value => this.availableExerciseTypes.set(value), error: () => this.error.set('Egzersiz türleri yüklenemedi.') }); }
  editExercise(item: SpeedReadingExercise): void { this.exerciseEditingId = item.id; this.exerciseDraft = { title: item.title, description: item.description, difficultyLevel: item.difficultyLevel, exerciseTypeId: item.exerciseTypeId, configurationJson: item.configurationJson, targetAgeGroupConfigurationId: item.targetAgeGroupConfigurationId, isActive: item.isActive }; this.exerciseEditing.set(true); }
  applyAdaptiveFluencyTemplate(): void { this.exerciseDraft.configurationJson = JSON.stringify({ engineType: 'adaptive_fluency', engineConfig: { engineType: 'adaptive_fluency', transferReadingTextId: 'TRANSFER_METIN_ID', repeatPurposes: ['Ana fikri belirleyin.', 'Neden-sonuç ilişkilerine ve önemli ayrıntılara odaklanın.'], increaseThreshold: 85, maintainThreshold: 75, supportThreshold: 65, minimumComprehension: 75, increasePercent: 8, decreasePercent: 5, supportDecreasePercent: 10 }, pilotOnly: true }, null, 2); }
  saveExercise(): void { const action = this.exerciseEditingId ? this.service.updateExercise(this.exerciseEditingId, this.exerciseDraft) : this.service.createExercise(this.exerciseDraft); this.run(action, () => { this.exerciseEditing.set(false); this.loadExercises(); }, 'Egzersiz kaydedilemedi.'); }
  async deleteExercise(item: SpeedReadingExercise): Promise<void> { if (!await this.toaster.confirm('Bu egzersiz silinsin mi?', { title: 'Egzersizi sil' })) return; this.run(this.service.deleteExercise(item.id), () => this.loadExercises(), 'Egzersiz silinemedi.'); }
  loadReadingTexts(): void { this.service.getReadingTexts().subscribe({ next: value => this.readingTexts.set(value), error: () => this.error.set('Okuma metinleri yüklenemedi.') }); this.service.getAllExercises().subscribe({ next: value => this.availableExercises.set(value), error: () => this.availableExercises.set([]) }); }
  editReadingText(item: SpeedReadingReadingText): void { this.service.getReadingText(item.id).subscribe({ next: detail => { this.readingTextEditingId = item.id; this.readingTextDraft = { title: detail.title, content: detail.content, wordCount: detail.wordCount, category: detail.category, difficultyLevel: detail.difficultyLevel, targetAgeGroupConfigurationId: detail.targetAgeGroupConfigurationId, language: detail.language, isActive: detail.isActive, tags: detail.tags.join(', '), recommendedMinLevel: detail.recommendedMinLevel, recommendedMaxLevel: detail.recommendedMaxLevel, exerciseId: detail.exerciseId }; this.readingTextContent = detail.content; this.readingTags = detail.tags.join(', '); this.selectedReadingText.set(detail); this.readingTextEditing.set(true); }, error: () => this.error.set('Okuma metni ayrıntısı yüklenemedi.') }); }
  saveReadingText(): void { const request = { ...this.readingTextDraft, content: this.readingTextContent, wordCount: this.calculatedWordCount(), tags: this.readingTags }; const action = this.readingTextEditingId ? this.service.updateReadingText(this.readingTextEditingId, request) : this.service.createReadingText(request); this.saving.set(true); this.error.set(''); action.pipe(finalize(() => this.saving.set(false))).subscribe({ next: saved => { this.readingTextEditingId = saved.id; this.loadReadingTexts(); this.service.getReadingText(saved.id).subscribe({ next: detail => this.selectedReadingText.set(detail), error: () => this.error.set('Metin kaydedildi ancak soru bilgileri yenilenemedi.') }); }, error: () => this.error.set('Okuma metni kaydedilemedi.') }); }
  async deleteReadingText(item: SpeedReadingReadingText): Promise<void> { if (!await this.toaster.confirm('Bu okuma metni silinsin mi?', { title: 'Okuma metnini sil' })) return; this.run(this.service.deleteReadingText(item.id), () => this.loadReadingTexts(), 'Okuma metni silinemedi.'); }
  onReadingTextImport(event: Event, format: 'csv' | 'excel'): void { const input = event.target as HTMLInputElement; const file = input.files?.[0]; if (!file) return; this.run(this.service.importReadingTexts(file, format), () => this.loadReadingTexts(), 'Okuma metni içe aktarılamadı.'); input.value = ''; }
  exportReadingText(item: SpeedReadingReadingText, format: 'pdf' | 'docx'): void { this.service.exportReadingText(item.id, format).subscribe({ next: blob => this.download(blob, `${item.title.replace(/[^a-z0-9-_]+/gi, '-').replace(/^-|-$/g, '') || 'okuma-metni'}.${format}`), error: () => this.error.set('Okuma metni dışa aktarılamadı.') }); }
  calculatedWordCount(): number { return this.readingTextContent.trim() ? this.readingTextContent.trim().split(/\s+/).length : 0; }
  paragraphCount(): number { return this.readingTextContent.trim() ? this.readingTextContent.trim().split(/\n\s*\n/).filter(Boolean).length : 0; }
  estimatedReadingMinutes(wordsPerMinute: number): string { const minutes = this.calculatedWordCount() / wordsPerMinute; return minutes < 0.1 ? '<0,1' : minutes.toLocaleString('tr-TR', { minimumFractionDigits: 1, maximumFractionDigits: 1 }); }
  closeReadingTextDialog(): void { this.readingTextEditing.set(false); this.questionEditing.set(false); this.selectedReadingText.set(null); }
  startQuestionCreate(): void { const readingTextId = this.selectedReadingText()?.id; if (!readingTextId) return; this.questionEditingId = null; this.questionDraft = this.emptyQuestion(readingTextId); this.questionEditing.set(true); }
  editQuestion(item: SpeedReadingReadingQuestion): void { this.questionEditingId = item.id; this.questionDraft = { readingTextId: this.selectedReadingText()?.id ?? '', questionText: item.questionText, type: item.type, bloomLevel: item.bloomLevel, difficultyLevel: item.difficultyLevel, explanation: item.explanation, optionA: item.optionA, optionB: item.optionB, optionC: item.optionC, optionD: item.optionD, correctAnswer: item.correctAnswer, orderIndex: item.orderIndex }; this.questionEditing.set(true); }
  saveQuestion(): void { const readingTextId = this.selectedReadingText()?.id; if (!readingTextId) return; const updateRequest: SpeedReadingReadingQuestionUpdateRequest = { questionText: this.questionDraft.questionText, type: this.questionDraft.type, bloomLevel: this.questionDraft.bloomLevel, difficultyLevel: this.questionDraft.difficultyLevel, explanation: this.questionDraft.explanation, optionA: this.questionDraft.optionA, optionB: this.questionDraft.optionB, optionC: this.questionDraft.optionC, optionD: this.questionDraft.optionD, correctAnswer: this.questionDraft.correctAnswer, orderIndex: this.questionDraft.orderIndex }; const action = this.questionEditingId ? this.service.updateReadingQuestion(this.questionEditingId, updateRequest) : this.service.createReadingQuestion({ ...this.questionDraft, readingTextId }); this.run(action, () => this.refreshReadingText(readingTextId), 'Okuma sorusu kaydedilemedi.'); }
  async deleteQuestion(item: SpeedReadingReadingQuestion): Promise<void> { if (!await this.toaster.confirm('Bu soru silinsin mi?', { title: 'Soruyu sil' })) return; this.run(this.service.deleteReadingQuestion(item.id), () => { const readingTextId = this.selectedReadingText()?.id; if (readingTextId) this.refreshReadingText(readingTextId); }, 'Okuma sorusu silinemedi.'); }
  private refreshReadingText(id: string): void { this.questionEditing.set(false); this.service.getReadingText(id).subscribe({ next: value => this.selectedReadingText.set(value), error: () => this.error.set('Okuma metni ayrıntısı yüklenemedi.') }); }
  changeContentPage(delta: number): void { this.contentPage = Math.max(1, this.contentPage + delta); if (this.contentTab() === 'exercise-types') this.loadExerciseTypes(); else if (this.contentTab() === 'exercises') this.loadExercises(); }

  loadPrograms(): void { this.service.getProgramTemplates().subscribe({ next: value => this.programs.set(value), error: () => this.error.set('Programlar yüklenemedi.') }); }
  startProgramCreate(): void { if (this.programTab() === 'programs') { this.programEditingId = null; this.programDraft = this.emptyProgram(); this.programEditing.set(true); } else { this.learningPathEditingId = null; this.learningPathDraft = this.emptyLearningPath(); this.learningPathEditing.set(true); } }
  editProgram(item: SpeedReadingProgramTemplate): void { this.programEditingId = item.id; this.programDraft = { name: item.name, description: item.description, targetAgeGroupConfigurationId: item.targetAgeGroupConfigurationId, minAssessmentScore: item.minAssessmentScore, maxAssessmentScore: item.maxAssessmentScore, weeklyPatternJson: item.weeklyPatternJson, initialDifficultyLevel: item.initialDifficultyLevel, weeksPerDifficultyIncrease: item.weeksPerDifficultyIncrease, maxDifficultyLevel: item.maxDifficultyLevel, totalWeeks: item.totalWeeks, totalDays: item.totalDays, isActive: item.isActive, displayOrder: item.displayOrder, programType: item.programType, examType: item.examType, isAssessment: item.isAssessment }; this.programEditing.set(true); }
  saveProgram(): void { const action = this.programEditingId ? this.service.updateProgramTemplate(this.programEditingId, this.programDraft) : this.service.createProgramTemplate(this.programDraft); this.run(action, () => { this.programEditing.set(false); this.loadPrograms(); }, 'Program kaydedilemedi.'); }
  async deleteProgram(item: SpeedReadingProgramTemplate): Promise<void> { if (!await this.toaster.confirm('Bu program silinsin mi?', { title: 'Programı sil' })) return; this.run(this.service.deleteProgramTemplate(item.id), () => this.loadPrograms(), 'Program silinemedi.'); }
  async cloneProgram(item: SpeedReadingProgramTemplate): Promise<void> { if (!await this.toaster.confirm(`"${item.name}" programı kopyalansın mı?`, { title: 'Programı kopyala' })) return; this.run(this.service.cloneProgramTemplate(item.id), () => this.loadPrograms(), 'Program kopyalanamadı.'); }
  loadLearningPaths(): void { this.service.getLearningPathTemplates().subscribe({ next: value => this.learningPaths.set(value), error: () => this.error.set('Öğrenme yolları yüklenemedi.') }); }
  selectLearningPath(item: SpeedReadingLearningPathTemplate): void { this.service.getLearningPathTemplateDetails(item.id).subscribe({ next: value => this.selectedPath.set(value), error: () => this.error.set('Öğrenme yolu ayrıntısı yüklenemedi.') }); }
  editLearningPath(item: SpeedReadingLearningPathTemplate): void { this.learningPathEditingId = item.id; this.learningPathDraft = { name: item.name, targetAgeGroupConfigurationId: item.targetAgeGroupConfigurationId, description: item.description, estimatedDays: item.estimatedDays, isActive: item.isActive }; this.learningPathEditing.set(true); }
  saveLearningPath(): void { const action = this.learningPathEditingId ? this.service.updateLearningPathTemplate(this.learningPathEditingId, this.learningPathDraft) : this.service.createLearningPathTemplate(this.learningPathDraft); this.run(action, () => { this.learningPathEditing.set(false); this.loadLearningPaths(); }, 'Öğrenme yolu kaydedilemedi.'); }
  async deleteLearningPath(item: SpeedReadingLearningPathTemplate): Promise<void> { if (!await this.toaster.confirm('Bu öğrenme yolu silinsin mi?', { title: 'Öğrenme yolunu sil' })) return; this.run(this.service.deleteLearningPathTemplate(item.id), () => this.loadLearningPaths(), 'Öğrenme yolu silinemedi.'); }
  startNodeCreate(): void { const templateId = this.selectedPath()?.template.id ?? ''; this.nodeEditingId = null; this.nodeDraft = this.emptyNode(templateId); this.nodeEditing.set(true); }
  editNode(node: SpeedReadingLearningPathNode): void { const templateId = this.selectedPath()?.template.id ?? ''; this.nodeEditingId = node.id; this.nodeDraft = { templateId, parentNodeId: node.parentNodeId, nodeType: node.nodeType, title: node.title, contentType: node.contentType, contentId: node.contentId, order: node.order }; this.nodeEditing.set(true); }
  saveNode(): void { const action = this.nodeEditingId ? this.service.updateLearningPathNode(this.nodeEditingId, { parentNodeId: this.nodeDraft.parentNodeId, nodeType: this.nodeDraft.nodeType, title: this.nodeDraft.title, contentType: this.nodeDraft.contentType, contentId: this.nodeDraft.contentId, order: this.nodeDraft.order }) : this.service.createLearningPathNode(this.nodeDraft); this.run(action, () => { this.nodeEditing.set(false); if (this.selectedPath()) this.selectLearningPath(this.selectedPath()!.template); }, 'Öğrenme yolu düğümü kaydedilemedi.'); }
  async deleteNode(node: SpeedReadingLearningPathNode): Promise<void> { if (!await this.toaster.confirm('Bu düğüm silinsin mi?', { title: 'Düğümü sil' })) return; this.run(this.service.deleteLearningPathNode(node.id), () => { if (this.selectedPath()) this.selectLearningPath(this.selectedPath()!.template); }, 'Öğrenme yolu düğümü silinemedi.'); }
  manageNode(node: SpeedReadingLearningPathNode): void { this.activeNode.set(node); this.nodeContentEditing.set(false); this.prerequisiteNodeId = ''; }
  startNodeContentCreate(): void { const nodeId = this.activeNode()?.id; if (!nodeId) return; this.nodeContentEditingId = null; this.nodeContentDraft = this.emptyNodeContent(nodeId); this.nodeContentEditing.set(true); }
  editNodeContent(content: SpeedReadingLearningPathNodeContent): void { const nodeId = this.activeNode()?.id; if (!nodeId) return; this.nodeContentEditingId = content.id; this.nodeContentDraft = { nodeId, exerciseId: content.exerciseId, readingTextId: content.readingTextId, description: content.description }; this.nodeContentEditing.set(true); }
  saveNodeContent(): void { const nodeId = this.activeNode()?.id; if (!nodeId) return; const updateRequest: SpeedReadingLearningPathNodeContentUpdateRequest = { exerciseId: this.nodeContentDraft.exerciseId, readingTextId: this.nodeContentDraft.readingTextId, description: this.nodeContentDraft.description }; const action = this.nodeContentEditingId ? this.service.updateLearningPathNodeContent(this.nodeContentEditingId, updateRequest) : this.service.createLearningPathNodeContent({ ...this.nodeContentDraft, nodeId }); this.run(action, () => this.reloadSelectedPath(nodeId), 'Düğüm içeriği kaydedilemedi.'); }
  async deleteNodeContent(content: SpeedReadingLearningPathNodeContent): Promise<void> { if (!await this.toaster.confirm('Bu düğüm içeriği silinsin mi?', { title: 'Düğüm içeriğini sil' })) return; this.run(this.service.deleteLearningPathNodeContent(content.id), () => { const nodeId = this.activeNode()?.id; if (nodeId) this.reloadSelectedPath(nodeId); }, 'Düğüm içeriği silinemedi.'); }
  addPrerequisite(): void { const nodeId = this.activeNode()?.id; if (!nodeId || !this.prerequisiteNodeId || nodeId === this.prerequisiteNodeId) return; const request: SpeedReadingLearningPathPrerequisiteRequest = { nodeId, prerequisiteNodeId: this.prerequisiteNodeId }; this.run(this.service.createLearningPathPrerequisite(request), () => { this.prerequisiteNodeId = ''; this.reloadSelectedPath(nodeId); }, 'Önkoşul eklenemedi.'); }
  async deletePrerequisite(prerequisiteNodeId: string): Promise<void> { const nodeId = this.activeNode()?.id; if (!nodeId) return; if (!await this.toaster.confirm('Bu önkoşul kaldırılsın mı?', { title: 'Önkoşulu kaldır' })) return; this.run(this.service.deleteLearningPathPrerequisite(nodeId, prerequisiteNodeId), () => this.reloadSelectedPath(nodeId), 'Önkoşul kaldırılamadı.'); }
  nodeTitle(nodes: SpeedReadingLearningPathNode[], id: string): string { return nodes.find(node => node.id === id)?.title ?? id; }
  private reloadSelectedPath(activeNodeId?: string): void { const template = this.selectedPath()?.template; if (!template) return; this.service.getLearningPathTemplateDetails(template.id).subscribe({ next: value => { this.selectedPath.set(value); this.activeNode.set(activeNodeId ? value.nodes.find(node => node.id === activeNodeId) ?? null : null); this.nodeContentEditing.set(false); }, error: () => this.error.set('Öğrenme yolu ayrıntısı yenilenemedi.') }); }

  loadAchievements(): void { this.service.getAchievementsForAdmin(this.achievementPage).subscribe({ next: value => this.achievements.set(value), error: () => this.error.set('Başarılar yüklenemedi.') }); this.loadAchievementStats(); }
  loadAchievementStats(): void { this.service.getAchievementStats().subscribe({ next: value => this.achievementStats.set(value), error: () => this.achievementStats.set(null) }); }
  startAchievementCreate(): void { this.achievementEditingId = null; this.achievementDraft = this.emptyAchievement(); this.achievementEditing.set(true); }
  editAchievement(item: SpeedReadingAchievement): void { this.achievementEditingId = item.id; this.achievementDraft = { name: item.name, description: item.description, category: item.category, tier: item.tier, iconUrl: item.iconUrl, iconEmoji: item.iconEmoji, criteriaType: item.criteriaType, criteriaValue: item.criteriaValue, triggerType: item.triggerType, triggerValue: item.triggerValue, isRepeatable: item.isRepeatable, xpReward: item.xpReward, isActive: item.isActive, sortOrder: item.sortOrder }; this.achievementEditing.set(true); }
  saveAchievement(): void { const action = this.achievementEditingId ? this.service.updateAchievement(this.achievementEditingId, this.achievementDraft) : this.service.createAchievement(this.achievementDraft); this.run(action, () => { this.achievementEditing.set(false); this.loadAchievements(); }, 'Başarı kaydedilemedi.'); }
  async deleteAchievement(item: SpeedReadingAchievement): Promise<void> { if (!await this.toaster.confirm('Bu başarı silinsin mi?', { title: 'Başarıyı sil' })) return; this.run(this.service.deleteAchievement(item.id), () => this.loadAchievements(), 'Başarı silinemedi.'); }
  changeAchievementPage(delta: number): void { this.achievementPage = Math.max(1, this.achievementPage + delta); this.loadAchievements(); }
  totalPages(page: { totalCount: number; pageSize: number }): number { return Math.max(1, Math.ceil(page.totalCount / page.pageSize)); }
  cancelContentEdit(): void { this.typeEditing.set(false); this.exerciseEditing.set(false); this.readingTextEditing.set(false); this.questionEditing.set(false); }
  cancelProgramEdit(): void { this.programEditing.set(false); this.learningPathEditing.set(false); this.nodeEditing.set(false); this.nodeContentEditing.set(false); this.activeNode.set(null); }
  private run(request: Observable<unknown>, success: () => void, message: string): void { this.saving.set(true); this.error.set(''); request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: success, error: () => this.error.set(message) }); }
  private download(blob: Blob, fileName: string): void { const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = fileName; anchor.click(); URL.revokeObjectURL(url); }
  private emptyType(): SpeedReadingExerciseTypeRequest { return { name: '', displayName: '', description: '', iconName: 'category', colorCode: '#2563eb', sortOrder: 0, isActive: true, engineType: '', categoryId: null }; }
  private emptyExercise(): SpeedReadingExerciseRequest { return { title: '', description: '', difficultyLevel: 1, exerciseTypeId: '', configurationJson: '{}', targetAgeGroupConfigurationId: null, isActive: true }; }
  private emptyReadingText(): SpeedReadingReadingTextRequest { return { title: '', content: '', wordCount: 0, category: '', difficultyLevel: 1, targetAgeGroupConfigurationId: null, language: 'tr', isActive: true, tags: '', recommendedMinLevel: 0, recommendedMaxLevel: 10, exerciseId: null }; }
  private emptyQuestion(readingTextId: string): SpeedReadingReadingQuestionRequest { return { readingTextId, questionText: '', type: 1, bloomLevel: 1, difficultyLevel: 1, explanation: '', optionA: '', optionB: '', optionC: '', optionD: '', correctAnswer: 'A', orderIndex: 0 }; }
  private emptyProgram(): SpeedReadingProgramTemplateRequest { return { name: '', description: '', targetAgeGroupConfigurationId: '', minAssessmentScore: 0, maxAssessmentScore: 100, weeklyPatternJson: '{}', initialDifficultyLevel: 1, weeksPerDifficultyIncrease: 1, maxDifficultyLevel: 5, totalWeeks: 4, totalDays: 28, isActive: true, displayOrder: 0, programType: 0, examType: null, isAssessment: false }; }
  private emptyLearningPath(): SpeedReadingLearningPathTemplateRequest { return { name: '', targetAgeGroupConfigurationId: null, description: '', estimatedDays: 1, isActive: true }; }
  private emptyNode(templateId: string): SpeedReadingLearningPathNodeRequest { return { templateId, parentNodeId: null, nodeType: 'Exercise', title: '', contentType: null, contentId: null, order: 0 }; }
  private emptyNodeContent(nodeId: string): SpeedReadingLearningPathNodeContentRequest { return { nodeId, exerciseId: null, readingTextId: null, description: '' }; }
  private emptyAchievement(): SpeedReadingAchievementRequest { return { name: '', description: '', category: 'Reading', tier: 'Bronze', iconUrl: null, iconEmoji: '🏅', criteriaType: 'count', criteriaValue: '1', triggerType: null, triggerValue: null, isRepeatable: false, xpReward: 10, isActive: true, sortOrder: 0 }; }
}
