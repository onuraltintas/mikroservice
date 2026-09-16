import { CommonModule } from '@angular/common';
import { Component, HostListener, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { A11yModule } from '@angular/cdk/a11y';
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
  SpeedReadingReadingTextImportResult,
  SpeedReadingReadingTextQualityMetrics,
  SpeedReadingAgeGroup,
  SpeedReadingAchievementStats
} from '../../../core/services/speed-reading-admin.service';

type CoreTab = 'content' | 'programs' | 'achievements';
type ContentTab = 'exercise-types' | 'exercises' | 'reading-texts';
type ProgramTab = 'programs' | 'learning-paths';
type AdaptivePolicyDraft = {
  minimumMeasuredSessions: number;
  advanceComprehensionThreshold: number;
  maintainComprehensionThreshold: number;
  minimumWpmTrendPercent: number;
  supportTrendPercent: number;
};

type ExerciseSettingsDraft = {
  mode: string;
  targetWpm: number | null;
  timeLimitSeconds: number | null;
  gridSize: number | null;
  itemCount: number | null;
  chunkSize: number | null;
  vocabularyCategory: string;
  vocabularyDifficulty: number | null;
  vocabularyCount: number | null;
  transferReadingTextId: string;
  increaseThreshold: number;
  maintainThreshold: number;
  supportThreshold: number;
  minimumComprehension: number;
  increasePercent: number;
  decreasePercent: number;
  supportDecreasePercent: number;
  repeatPurposeOne: string;
  repeatPurposeTwo: string;
};

type ProgramPlanEntryDraft = {
  id: number;
  exerciseTypeId: string;
  exerciseTypeName: string;
  exerciseId: string;
  count: number;
  difficulty: number | null;
};

type ProgramPlanDayDraft = { id: number; dayNumber: number; entries: ProgramPlanEntryDraft[] };
type ProgramPlanWeekDraft = { id: number; weekNumber: number; days: ProgramPlanDayDraft[] };

type AchievementCriteriaDefinition = {
  value: string;
  label: string;
  property: string;
  input: 'number' | 'text';
  min?: number;
  max?: number;
  allowDecimal?: boolean;
  help: string;
};

@Component({
  selector: 'app-speed-reading-catalog',
  standalone: true,
  imports: [CommonModule, FormsModule, A11yModule, MatIconModule],
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
                <form class="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="type-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveExerciseType()" (click)="$event.stopPropagation()">
                  <div class="dialog-header"><div><p class="dialog-eyebrow">Egzersiz türü</p><h3 id="type-dialog-title">{{ typeEditingId ? 'Egzersiz türünü düzenle' : 'Yeni egzersiz türü' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="typeEditing.set(false)"><mat-icon>close</mat-icon></button></div>
                  <div class="dialog-body space-y-5">
                    <div class="form-grid"><label>Teknik ad<input [(ngModel)]="typeDraft.name" name="typeName" required maxlength="100" /></label><label>Öğrencide görünen ad<input [(ngModel)]="typeDraft.displayName" name="typeDisplayName" required maxlength="150" /></label><label>Motor tipi<select [(ngModel)]="typeDraft.engineType" name="typeEngine" required><option value="">Öğrenci oynatıcısı seçin</option>@for (engine of exerciseEngines; track engine.value) {<option [value]="engine.value">{{ engine.label }}</option>}</select><span class="muted">Seçilen motor öğrenci ekranındaki egzersiz deneyimini belirler.</span></label><label>Sıra<input type="number" [(ngModel)]="typeDraft.sortOrder" name="typeOrder" min="0" /></label><label class="wide">Açıklama<textarea [(ngModel)]="typeDraft.description" name="typeDescription" maxlength="1000"></textarea></label></div>
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
                <form class="dialog-panel dialog-panel-wide" role="dialog" aria-modal="true" aria-labelledby="exercise-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveExercise()" (click)="$event.stopPropagation()">
                  <div class="dialog-header"><div><p class="dialog-eyebrow">Egzersiz</p><h3 id="exercise-dialog-title">{{ exerciseEditingId ? 'Egzersizi düzenle' : 'Yeni egzersiz' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="exerciseEditing.set(false)"><mat-icon>close</mat-icon></button></div>
                  <div class="dialog-body space-y-4">
                    <div class="form-grid"><label>Başlık<input [(ngModel)]="exerciseDraft.title" name="exerciseTitle" required maxlength="200" /></label><label>Egzersiz türü<select [(ngModel)]="exerciseDraft.exerciseTypeId" (ngModelChange)="onExerciseTypeChanged()" name="exerciseTypeId" required><option value="">Tür seçin</option>@for (type of availableExerciseTypes(); track type.id) {<option [value]="type.id">{{ type.displayName || type.name }}</option>}</select></label><label>Zorluk (0-10)<input type="number" [(ngModel)]="exerciseDraft.difficultyLevel" name="exerciseDifficulty" min="0" max="10" /></label><label>Yaş grubu<select [(ngModel)]="exerciseDraft.targetAgeGroupConfigurationId" name="exerciseAgeGroup"><option [ngValue]="null">Tüm yaş grupları</option>@for (ageGroup of ageGroups(); track ageGroup.id) {<option [ngValue]="ageGroup.id">{{ ageGroup.displayName }} ({{ ageGroup.minAge }}–{{ ageGroup.maxAge }} yaş)</option>}</select></label><label class="wide">Açıklama<textarea [(ngModel)]="exerciseDraft.description" name="exerciseDescription" maxlength="5000"></textarea></label></div>
                    <fieldset class="form-card"><legend>Öğrenci deneyimi ayarları</legend><p class="muted">Seçilen egzersiz motoru bu ayarlarla çalışır. Kaydederken sistem geçerli teknik yapılandırmayı otomatik oluşturur.</p><div class="form-grid"><label>Oynatıcı motoru<input [value]="selectedExerciseEngineLabel()" readonly /></label><label>Hedef hız (kelime/dk.)<input type="number" [(ngModel)]="exerciseSettings.targetWpm" name="exerciseTargetWpm" min="1" /></label><label>Oturum üst sınırı (sn.)<input type="number" [(ngModel)]="exerciseSettings.timeLimitSeconds" name="exerciseTimeLimit" min="1" /></label><label>Uyarıcı / adım sayısı<input type="number" [(ngModel)]="exerciseSettings.itemCount" name="exerciseItemCount" min="1" /></label>@if (selectedExerciseEngine() === 'grid_interaction') {<label>Izgara boyutu<input type="number" [(ngModel)]="exerciseSettings.gridSize" name="exerciseGridSize" min="2" max="10" /></label>}@if (usesChunkSize()) {<label>Kelime grubu boyutu<input type="number" [(ngModel)]="exerciseSettings.chunkSize" name="exerciseChunkSize" min="1" max="20" /></label>}@if (selectedExerciseEngine() === 'text_stream') {<label>Akış modu<select [(ngModel)]="exerciseSettings.mode" name="exerciseStreamMode"><option value="rsvp">RSVP</option><option value="tachistoscope">Taşistoskop</option><option value="sequence">Sıralı akış</option></select></label>}@if (selectedExerciseEngine() === 'motion_path') {<label>Göz hareketi modu<select [(ngModel)]="exerciseSettings.mode" name="exerciseMotionMode"><option value="fixation">Sabitleme</option><option value="saccade">Sakkad</option><option value="tracking">Takip</option></select></label>}@if (selectedExerciseEngine() === 'focus' || selectedExerciseEngine() === 'attention_training') {<label>Odak modu<select [(ngModel)]="exerciseSettings.mode" name="exerciseFocusMode"><option value="position">Konum</option><option value="word">Kelime</option><option value="dual">Çift uyarıcı</option></select></label>}@if (selectedExerciseEngine() === 'visualization') {<label>Görselleştirme modu<select [(ngModel)]="exerciseSettings.mode" name="exerciseVisualizationMode"><option value="static">Tek sahne</option><option value="sequence">Sahne dizisi</option></select></label>}</div>
                      @if (selectedExerciseEngine() === 'vocabulary_builder') {<div class="form-grid mt-4"><label>Kelime kategorisi<input [(ngModel)]="exerciseSettings.vocabularyCategory" name="exerciseVocabularyCategory" maxlength="100" placeholder="Boş bırakılırsa tüm kategoriler" /></label><label>Kelime zorluğu<select [(ngModel)]="exerciseSettings.vocabularyDifficulty" name="exerciseVocabularyDifficulty"><option [ngValue]="null">Egzersiz zorluğunu kullan</option><option [ngValue]="1">1</option><option [ngValue]="2">2</option><option [ngValue]="3">3</option><option [ngValue]="4">4</option><option [ngValue]="5">5</option></select></label><label>Kelime sayısı<input type="number" [(ngModel)]="exerciseSettings.vocabularyCount" name="exerciseVocabularyCount" min="1" max="50" /></label></div>}
                      @if (selectedExerciseEngine() === 'adaptive_fluency') {<div class="editor-section mt-4"><div><strong>Anlam korumalı adaptif akıcılık</strong><p class="muted">Sistem ana metinden farklı, aynı zorlukta ve sorulu bir transfer metni ile dört aşamalı tekrar uygular.</p></div><div class="form-grid"><label class="wide">Transfer metni<select [(ngModel)]="exerciseSettings.transferReadingTextId" name="exerciseTransferText" required><option value="">Transfer metni seçin</option>@for (text of eligibleTransferTexts(); track text.id) {<option [value]="text.id">{{ text.title }} · seviye {{ text.difficultyLevel }}</option>}</select><span class="muted">Yalnızca aktif ve cevap anahtarlı soruları olan, egzersiz zorluğuna uygun metinler gösterilir.</span></label><label>İlerleme eşiği<input type="number" [(ngModel)]="exerciseSettings.increaseThreshold" name="adaptiveIncreaseThreshold" min="0" max="100" /></label><label>Pekiştirme eşiği<input type="number" [(ngModel)]="exerciseSettings.maintainThreshold" name="adaptiveMaintainThreshold" min="0" max="100" /></label><label>Destek eşiği<input type="number" [(ngModel)]="exerciseSettings.supportThreshold" name="adaptiveSupportThreshold" min="0" max="100" /></label><label>Asgari anlama<input type="number" [(ngModel)]="exerciseSettings.minimumComprehension" name="adaptiveMinimumComprehension" min="0" max="100" /></label><label>İlerleme artışı (%)<input type="number" [(ngModel)]="exerciseSettings.increasePercent" name="adaptiveIncreasePercent" min="0" max="100" /></label><label>Normal düşüş (%)<input type="number" [(ngModel)]="exerciseSettings.decreasePercent" name="adaptiveDecreasePercent" min="0" max="100" /></label><label>Destek düşüşü (%)<input type="number" [(ngModel)]="exerciseSettings.supportDecreasePercent" name="adaptiveSupportDecreasePercent" min="0" max="100" /></label><label>1. tekrar amacı<input [(ngModel)]="exerciseSettings.repeatPurposeOne" name="adaptivePurposeOne" maxlength="500" /></label><label>2. tekrar amacı<input [(ngModel)]="exerciseSettings.repeatPurposeTwo" name="adaptivePurposeTwo" maxlength="500" /></label></div></div>}
                    </fieldset>
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
            @if (readingTextImportResult(); as result) {<section class="rounded-lg border p-3 text-sm" [class.border-emerald-200]="result.errorCount === 0" [class.bg-emerald-50]="result.errorCount === 0" [class.border-amber-200]="result.errorCount > 0" [class.bg-amber-50]="result.errorCount > 0"><strong>{{ result.successCount }} metin içe aktarıldı</strong><span> · {{ result.errorCount }} satır alınamadı</span>@if (importErrors(result).length) {<ul class="mt-2 list-disc space-y-1 pl-5">@for (item of importErrors(result); track $index) {<li>{{ item }}</li>}</ul>}</section>}
            <form class="form-grid data-card" (ngSubmit)="applyReadingTextFilters()"><label class="wide">Metin ara<input [(ngModel)]="readingTextSearch" name="readingTextSearch" maxlength="100" placeholder="Başlık veya içerik içinde arayın" /></label><label>Kategori<select [(ngModel)]="readingTextCategory" name="readingTextCategory"><option value="">Tüm kategoriler</option>@for (category of readingTextCategories(); track category) {<option [value]="category">{{ category }}</option>}</select></label><label>Zorluk<select [(ngModel)]="readingTextDifficulty" name="readingTextDifficulty"><option value="">Tüm zorluklar</option>@for (difficulty of readingTextDifficultyLevels(); track difficulty) {<option [value]="difficulty">Seviye {{ difficulty }}</option>}</select></label><label>Yaş grubu<select [(ngModel)]="readingTextAgeGroupId" name="readingTextAgeGroup"><option value="">Tüm yaş grupları</option>@for (ageGroup of ageGroups(); track ageGroup.id) {<option [value]="ageGroup.id">{{ ageGroup.displayName }}</option>}</select></label><label>Durum<select [(ngModel)]="readingTextStatus" name="readingTextStatus"><option value="all">Tümü</option><option value="active">Aktif</option><option value="passive">Taslak / pasif</option></select></label><label class="check"><input type="checkbox" [(ngModel)]="onlyTextsWithQuestions" name="onlyTextsWithQuestions" /> Sadece sorulu metinler</label><div class="actions"><button class="primary" type="submit">Filtrele</button><button class="secondary" type="button" (click)="clearReadingTextFilters()">Temizle</button></div></form>
            @if (readingTextEditing()) {
              <div class="dialog-backdrop" role="presentation" (click)="closeReadingTextDialog()">
                <div class="dialog-panel dialog-panel-reading" role="dialog" aria-modal="true" aria-labelledby="reading-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (click)="$event.stopPropagation()">
                  <div class="dialog-header"><div><p class="dialog-eyebrow">Okuma içeriği</p><h3 id="reading-dialog-title">{{ readingTextEditingId ? 'Okuma metnini ve sorularını düzenle' : 'Yeni okuma metni' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="closeReadingTextDialog()"><mat-icon>close</mat-icon></button></div>
                  <div class="reading-workspace">
                    <form class="reading-editor-pane" (ngSubmit)="saveReadingText()">
                      <div class="section-heading"><div><h4>Metin bilgileri</h4><p class="muted">Seviye, hedef kitle ve katalog bağlantıları</p></div><span class="status-chip" [class.active]="readingTextDraft.isActive">{{ readingTextDraft.isActive ? 'Aktif' : 'Pasif' }}</span></div>
                      <div class="form-grid"><label class="wide">Başlık<input [(ngModel)]="readingTextDraft.title" name="readingTitle" required maxlength="250" /></label><label>Kategori<input [(ngModel)]="readingTextDraft.category" name="readingCategory" required maxlength="100" placeholder="Bilim, tarih, doğa…" /></label><label>Dil<select [(ngModel)]="readingTextDraft.language" (ngModelChange)="queueQualityPreview()" name="readingLanguage" required><option value="tr">Türkçe</option><option value="en">İngilizce</option><option value="de">Almanca</option><option value="fr">Fransızca</option></select></label><label>Önerilen min. seviye<input type="number" [(ngModel)]="readingTextDraft.recommendedMinLevel" name="readingMinLevel" min="0" /></label><label>Önerilen maks. seviye<input type="number" [(ngModel)]="readingTextDraft.recommendedMaxLevel" name="readingMaxLevel" min="0" /></label><label>Zorluk (0-10)<input type="number" [(ngModel)]="readingTextDraft.difficultyLevel" name="readingDifficulty" min="0" max="10" /></label><label>Bağlı egzersiz<select [(ngModel)]="readingTextDraft.exerciseId" name="readingExerciseId"><option [ngValue]="null">Ortak metin</option>@for (exercise of availableExercises(); track exercise.id) {<option [ngValue]="exercise.id">{{ exercise.title }}</option>}</select></label><label>Yaş grubu<select [(ngModel)]="readingTextDraft.targetAgeGroupConfigurationId" name="readingAgeGroup"><option [ngValue]="null">Tüm yaş grupları</option>@for (ageGroup of ageGroups(); track ageGroup.id) {<option [ngValue]="ageGroup.id">{{ ageGroup.displayName }} · {{ ageGroup.minAge }}-{{ ageGroup.maxAge }} yaş</option>}</select></label><label>Etiketler<input [(ngModel)]="readingTags" name="readingTags" placeholder="bilim, uzay, keşif" /><span class="muted">Etiketleri virgülle ayırın.</span></label></div>
                      <div class="editor-section"><div class="section-heading"><div><h4>Metin içeriği</h4><p class="muted">Paragrafları boş satırla ayırabilirsiniz.</p></div></div><textarea class="reading-editor" [(ngModel)]="readingTextContent" (ngModelChange)="queueQualityPreview()" name="readingContent" required maxlength="500000" placeholder="Okuma metnini buraya yazın…"></textarea><div class="text-metrics"><span><strong>{{ calculatedWordCount() }}</strong> kelime</span><span><strong>{{ paragraphCount() }}</strong> paragraf</span><span><strong>{{ estimatedReadingMinutes(150) }}</strong> dk normal okuma</span><span><strong>{{ estimatedReadingMinutes(300) }}</strong> dk hızlı okuma</span></div></div>
                      <label class="check"><input type="checkbox" [(ngModel)]="readingTextDraft.isActive" name="readingActive" /> Metin öğrenci kullanımına açık</label>
                      @if (publicationBlockers().length) {<div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300"><strong>Öğrenci kullanımına açmak için düzeltin:</strong><ul class="mt-1 list-disc space-y-1 pl-5">@for (blocker of publicationBlockers(); track blocker) {<li>{{ blocker }}</li>}</ul></div>}
                      <div class="form-actions"><button type="button" class="secondary" (click)="closeReadingTextDialog()">Kapat</button><button class="primary" type="submit" [disabled]="saving() || (readingTextDraft.isActive && publicationBlockers().length > 0)">{{ saving() ? 'Kaydediliyor…' : (readingTextEditingId ? 'Metni kaydet' : (readingTextDraft.isActive ? 'Metni oluştur' : 'Taslağı oluştur')) }}</button></div>
                    </form>

                    <aside class="question-pane">
                      <div class="section-heading"><div><h4>Anlama soruları</h4><p class="muted">Metin ve sorular aynı çalışma alanında yönetilir.</p></div>@if (readingTextEditingId) {<button type="button" class="primary" (click)="startQuestionCreate()"><mat-icon>add</mat-icon> Yeni soru</button>}</div>
                      @if (currentQualityMetrics(); as quality) {
                        <section class="editor-section" aria-label="İçerik kalite ön kontrolü"><div class="section-heading"><div><h4>İçerik kalite ön kontrolü</h4><p class="muted">Taslak metin ve sorular için canlı ön kontrol. Uzman incelemesinin yerine geçmez.</p></div>@if (qualityPreviewLoading()) {<span class="status-chip">Kontrol ediliyor…</span>}</div><div class="text-metrics"><span><strong>{{ quality.wordCount }}</strong> kelime</span><span><strong>{{ quality.sentenceCount }}</strong> cümle</span><span><strong>{{ quality.averageWordsPerSentence }}</strong> kelime/cümle</span><span><strong>{{ quality.averageCharactersPerWord }}</strong> harf/kelime</span>@if (quality.estimatedAtesmanReadability !== null) {<span><strong>{{ quality.estimatedAtesmanReadability }}</strong> okunabilirlik · {{ quality.readabilityBand }}</span>}</div><p class="muted">Bloom: @for (item of quality.bloomDistribution; track item.level) { {{ item.level }}. seviye: {{ item.count }} } @empty { soru yok }</p><p class="muted">Zorluk: @for (item of quality.difficultyDistribution; track item.level) { {{ item.level }}: {{ item.count }} } @empty { soru yok }</p><p class="muted">Cevap anahtarı: @for (item of quality.correctAnswerDistribution; track item.level) { {{ answerChoiceLabel(item.level) }}: {{ item.count }} } @empty { anahtar yok }</p>@if (quality.correctAnswerUniqueLongestCount > 0 || quality.correctAnswerUniqueShortestCount > 0) {<p class="muted">Biçimsel ipucu: {{ quality.correctAnswerUniqueLongestCount }} doğru cevap tek başına en uzun, {{ quality.correctAnswerUniqueShortestCount }} doğru cevap tek başına en kısa seçenek.</p>}@if (quality.warnings.length) {<ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-amber-700 dark:text-amber-300">@for (warning of quality.warnings; track warning) {<li>{{ warning }}</li>}</ul>}@if (qualityPreviewError()) {<p role="alert" class="text-sm text-amber-700 dark:text-amber-300">{{ qualityPreviewError() }}</p>}</section>
                      }
                      @if (!readingTextEditingId) {
                        <div class="empty-state"><mat-icon>quiz</mat-icon><strong>Önce metni oluşturun</strong><p>Metin ilk kez kaydedildiğinde soru ekleme alanı otomatik olarak açılır.</p></div>
                      } @else {
                        @if (questionEditing()) {
                          <form class="question-form" (ngSubmit)="saveQuestion()"><div class="section-heading"><h4>{{ questionEditingId ? 'Soruyu düzenle' : 'Yeni soru' }}</h4><button type="button" class="icon-button" aria-label="Soru formunu kapat" (click)="questionEditing.set(false)"><mat-icon>close</mat-icon></button></div><label>Soru<textarea [(ngModel)]="questionDraft.questionText" (ngModelChange)="queueQualityPreview()" name="questionText" required maxlength="5000"></textarea></label><div class="form-grid"><label>Soru türü<select [(ngModel)]="questionDraft.type" (ngModelChange)="queueQualityPreview()" name="questionType" required><option [ngValue]="1">Gerçek anlam</option><option [ngValue]="2">Çıkarım</option><option [ngValue]="3">Değerlendirme</option></select><span class="muted">Tür, sorunun metinden hangi bilişsel işlemi ölçtüğünü belirtir. Seçenekli sunum biçimi tüm türlerde aynıdır.</span></label><label>Bloom seviyesi<select [(ngModel)]="questionDraft.bloomLevel" (ngModelChange)="queueQualityPreview()" name="questionBloom"><option [ngValue]="1">Hatırlama</option><option [ngValue]="2">Anlama</option><option [ngValue]="3">Uygulama</option><option [ngValue]="4">Analiz</option><option [ngValue]="5">Değerlendirme</option><option [ngValue]="6">Üretme</option></select></label><label>Zorluk<input type="number" [(ngModel)]="questionDraft.difficultyLevel" (ngModelChange)="queueQualityPreview()" name="questionDifficulty" min="0" max="10" /></label><label>Sıra<input type="number" [(ngModel)]="questionDraft.orderIndex" (ngModelChange)="queueQualityPreview()" name="questionOrder" min="0" /></label></div><div class="option-grid"><label>A seçeneği<input [(ngModel)]="questionDraft.optionA" (ngModelChange)="queueQualityPreview()" name="questionOptionA" required /></label><label>B seçeneği<input [(ngModel)]="questionDraft.optionB" (ngModelChange)="queueQualityPreview()" name="questionOptionB" required /></label><label>C seçeneği<input [(ngModel)]="questionDraft.optionC" (ngModelChange)="queueQualityPreview()" name="questionOptionC" required /></label><label>D seçeneği<input [(ngModel)]="questionDraft.optionD" (ngModelChange)="queueQualityPreview()" name="questionOptionD" required /></label></div><label>Doğru cevap<select [(ngModel)]="questionDraft.correctAnswer" (ngModelChange)="queueQualityPreview()" name="questionCorrect" required><option value="A">A</option><option value="B">B</option><option value="C">C</option><option value="D">D</option></select></label><label>Açıklama<textarea [(ngModel)]="questionDraft.explanation" (ngModelChange)="queueQualityPreview()" name="questionExplanation" maxlength="5000" placeholder="Doğru cevabın nedenini açıklayın."></textarea></label><div class="form-actions"><button type="button" class="secondary" (click)="questionEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Soruyu kaydet</button></div></form>
                        }
                        <div class="question-list">@for (question of selectedReadingText()?.questions ?? []; track question.id) {<article class="question-card"><div class="question-number">{{ question.orderIndex }}</div><div class="min-w-0 flex-1"><strong>{{ question.questionText }}</strong><p class="muted">{{ questionTypeLabel(question.type) }} · Doğru cevap: {{ question.correctAnswer }} · Bloom {{ question.bloomLevel }} · Zorluk {{ question.difficultyLevel }}</p></div><div class="actions"><button type="button" class="secondary" (click)="editQuestion(question)">Düzenle</button><button type="button" class="danger" (click)="deleteQuestion(question)">Sil</button></div></article>} @empty {<div class="empty-state"><mat-icon>quiz</mat-icon><strong>Henüz soru yok</strong><p>Anlama başarısını ölçmek için bu metne soru ekleyin.</p></div>}</div>
                      }
                    </aside>
                  </div>
                </div>
              </div>
            }
            <div class="data-card"><table class="data-table"><thead><tr><th>Başlık</th><th>Kelime</th><th>Kategori</th><th>Zorluk</th><th>Soru</th><th>Durum</th><th></th></tr></thead><tbody>
              @for (item of visibleReadingTexts(); track item.id) { <tr><td>{{ item.title }}</td><td>{{ item.wordCount }}</td><td>{{ item.category }}</td><td>{{ item.difficultyLevel }}</td><td>{{ item.questionCount ?? 0 }}</td><td>{{ item.isActive ? 'Aktif' : 'Taslak' }}</td><td class="actions"><button type="button" (click)="editReadingText(item)">Düzenle / sorular</button><button type="button" (click)="exportReadingText(item, 'pdf')">PDF</button><button type="button" (click)="exportReadingText(item, 'docx')">DOCX</button><button type="button" class="danger" (click)="deleteReadingText(item)">Sil</button></td></tr> } @empty { <tr><td colspan="7" class="empty">Okuma metni bulunamadı.</td></tr> }
            </tbody></table><div class="pager"><span>Toplam {{ readingTexts().length }} metin · Sayfa {{ readingTextPage }} / {{ readingTextTotalPages() }}</span><div class="actions"><button class="secondary" type="button" (click)="changeReadingTextPage(-1)" [disabled]="readingTextPage <= 1">Önceki</button><button class="secondary" type="button" (click)="changeReadingTextPage(1)" [disabled]="readingTextPage >= readingTextTotalPages()">Sonraki</button></div></div></div>
          }
        </section>
      }

      @if (selectedCoreTab() === 'programs') {
        <section class="space-y-4">
          <div class="flex flex-wrap items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">Program ve öğrenme yolları</h2><p class="muted">Program şablonları ile düğüm tabanlı öğrenme yolları.</p></div><button type="button" class="primary" (click)="startProgramCreate()">{{ programTab() === 'programs' ? 'Yeni program' : 'Yeni öğrenme yolu' }}</button></div>
          <nav class="ui-tab-list flex flex-wrap gap-2" aria-label="Program sekmeleri">@for (tab of programTabs; track tab.value) { <button type="button" class="ui-tab secondary" [attr.aria-pressed]="programTab() === tab.value" [class.bg-gray-100]="programTab() === tab.value" (click)="selectProgramTab(tab.value)">{{ tab.label }}</button> }</nav>
          @if (programTab() === 'programs') {
            @if (programEditing()) {
              <div class="dialog-backdrop" role="presentation" (click)="programEditing.set(false)">
                <form class="dialog-panel dialog-panel-wide" role="dialog" aria-modal="true" aria-labelledby="program-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveProgram()" (click)="$event.stopPropagation()">
                  <div class="dialog-header"><div><p class="dialog-eyebrow">Program şablonu</p><h3 id="program-dialog-title">{{ programEditingId ? 'Programı düzenle' : 'Yeni program' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="programEditing.set(false)"><mat-icon>close</mat-icon></button></div>
                  <div class="dialog-body"><div class="form-grid"><label>Ad<input [(ngModel)]="programDraft.name" name="programName" required maxlength="200" /></label><label>Yaş grubu<select [(ngModel)]="programDraft.targetAgeGroupConfigurationId" name="programAgeGroup" required><option value="">Yaş grubu seçin</option>@for (ageGroup of ageGroups(); track ageGroup.id) {<option [value]="ageGroup.id">{{ ageGroup.displayName }} ({{ ageGroup.minAge }}–{{ ageGroup.maxAge }} yaş)</option>}</select></label><label>Min. puan<input type="number" [(ngModel)]="programDraft.minAssessmentScore" name="programMinScore" /></label><label>Maks. puan<input type="number" [(ngModel)]="programDraft.maxAssessmentScore" name="programMaxScore" /></label><label>İlk zorluk<input type="number" [(ngModel)]="programDraft.initialDifficultyLevel" name="programInitialDifficulty" /></label><label>Maks. zorluk<input type="number" [(ngModel)]="programDraft.maxDifficultyLevel" name="programMaxDifficulty" /></label><label>Toplam hafta<input type="number" [(ngModel)]="programDraft.totalWeeks" name="programWeeks" min="1" /></label><label>Toplam gün<input type="number" [(ngModel)]="programDraft.totalDays" name="programDays" min="1" /></label><label class="wide">Açıklama<textarea [(ngModel)]="programDraft.description" name="programDescription" maxlength="5000"></textarea></label></div><fieldset class="form-card mt-4"><legend>Haftalık çalışma planı</legend><p class="muted">Öğrencinin günlük temel planını oluşturun. Adaptif motor bu planın üzerine öğrencinin ölçümlerine göre destek veya zorluk ayarı ekler.</p><div class="actions"><button type="button" class="secondary" (click)="addProgramWeek()"><mat-icon>add</mat-icon> Hafta ekle</button></div>@for (week of programPlan; track week.id) {<section class="plan-card"><div class="section-heading"><div><h4>{{ week.weekNumber }}. hafta</h4><p class="muted">Bu haftanın çalışma günleri.</p></div><div class="actions"><button type="button" class="secondary" (click)="addProgramDay(week)"><mat-icon>add</mat-icon> Gün ekle</button><button type="button" class="danger" (click)="removeProgramWeek(week)">Haftayı sil</button></div></div>@for (day of week.days; track day.id) {<div class="plan-day"><div class="section-heading"><strong>{{ day.dayNumber }}. gün</strong><div class="actions"><button type="button" class="secondary" (click)="addProgramPlanEntry(day)"><mat-icon>add</mat-icon> {{ programDraft.isAssessment ? 'Ölçüm egzersizi' : 'Egzersiz' }} ekle</button><button type="button" class="danger" (click)="removeProgramDay(week, day)">Günü sil</button></div></div>@for (entry of day.entries; track entry.id) {<div class="plan-entry form-grid">@if (programDraft.isAssessment) {<label class="wide">Ölçüm egzersizi<select [(ngModel)]="entry.exerciseId" [name]="'assessmentExercise' + entry.id" required><option value="">Egzersiz seçin</option>@for (exercise of availableExercises(); track exercise.id) {<option [value]="exercise.id">{{ exercise.title }} · {{ exercise.exerciseTypeName }}</option>}</select></label>} @else {<label>Egzersiz türü<select [(ngModel)]="entry.exerciseTypeId" [name]="'planType' + entry.id" required><option value="">Tür seçin</option>@for (type of availableExerciseTypes(); track type.id) {<option [value]="type.id">{{ type.displayName || type.name }}</option>}</select></label><label>Tekrar sayısı<input type="number" [(ngModel)]="entry.count" [name]="'planCount' + entry.id" min="1" max="20" required /></label><label>Zorluk (isteğe bağlı)<input type="number" [(ngModel)]="entry.difficulty" [name]="'planDifficulty' + entry.id" min="0" max="10" /></label>}<div class="actions"><button type="button" class="danger" (click)="removeProgramPlanEntry(day, entry)">Kaldır</button></div></div>} @empty {<p class="muted">Henüz çalışma eklenmedi.</p>}</div>} @empty {<p class="muted">Henüz gün eklenmedi.</p>}</section>} @empty {<div class="empty-state"><mat-icon>calendar_month</mat-icon><strong>Plan boş</strong><p>Hafta ekleyerek günlük egzersiz akışını oluşturmaya başlayın.</p></div>}</fieldset><fieldset class="form-card mt-4"><legend>Otomatik uyarlama kuralları</legend><p class="muted">Sistem, yeterli ölçümden sonra anlama ve WPM eğilimine göre öğrenciyi ilerletir, aynı seviyede tutar veya destek paketi oluşturur.</p><div class="form-grid"><label>Karar için ölçüm<input type="number" [(ngModel)]="adaptivePolicyDraft.minimumMeasuredSessions" name="adaptiveSessions" min="2" max="10" /></label><label>İlerleme anlama eşiği<input type="number" [(ngModel)]="adaptivePolicyDraft.advanceComprehensionThreshold" name="adaptiveAdvance" min="0" max="100" /></label><label>Pekiştirme alt eşiği<input type="number" [(ngModel)]="adaptivePolicyDraft.maintainComprehensionThreshold" name="adaptiveMaintain" min="0" max="100" /></label><label>İlerleme için WPM eğilimi<input type="number" [(ngModel)]="adaptivePolicyDraft.minimumWpmTrendPercent" name="adaptiveWpmTrend" min="-100" max="100" /></label><label>Destek WPM gerileme eşiği<input type="number" [(ngModel)]="adaptivePolicyDraft.supportTrendPercent" name="adaptiveSupportTrend" min="-100" max="0" /></label></div></fieldset><div class="form-grid"><label class="check"><input type="checkbox" [(ngModel)]="programDraft.isActive" name="programActive" /> Aktif</label><label class="check"><input type="checkbox" [(ngModel)]="programDraft.isAssessment" name="programAssessment" /> Seviye tespit programı</label></div></div>
                  <div class="dialog-footer"><button type="button" class="secondary" (click)="programEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div>
                </form>
              </div>
            }
            <div class="data-card"><table class="data-table"><thead><tr><th>Program</th><th>Hafta/gün</th><th>Zorluk</th><th>Durum</th><th></th></tr></thead><tbody>@for (item of programs(); track item.id) {<tr><td>{{ item.name }}<div class="muted">{{ item.description }}</div></td><td>{{ item.totalWeeks }} / {{ item.totalDays }}</td><td>{{ item.initialDifficultyLevel }}-{{ item.maxDifficultyLevel }}</td><td>{{ item.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions"><button type="button" (click)="editProgram(item)">Düzenle</button><button type="button" (click)="cloneProgram(item)">Kopyala</button><button type="button" class="danger" (click)="deleteProgram(item)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Program bulunamadı.</td></tr>}</tbody></table></div>
          }
          @if (programTab() === 'learning-paths') {
            @if (learningPathEditing()) {
              <div class="dialog-backdrop" role="presentation" (click)="learningPathEditing.set(false)"><form class="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="path-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveLearningPath()" (click)="$event.stopPropagation()"><div class="dialog-header"><div><p class="dialog-eyebrow">Öğrenme yolu</p><h3 id="path-dialog-title">{{ learningPathEditingId ? 'Öğrenme yolunu düzenle' : 'Yeni öğrenme yolu' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="learningPathEditing.set(false)"><mat-icon>close</mat-icon></button></div><div class="dialog-body"><div class="form-grid"><label>Ad<input [(ngModel)]="learningPathDraft.name" name="pathName" required maxlength="200" /></label><label>Yaş grubu<select [(ngModel)]="learningPathDraft.targetAgeGroupConfigurationId" name="pathAgeGroup"><option [ngValue]="null">Tüm yaş grupları</option>@for (ageGroup of ageGroups(); track ageGroup.id) {<option [ngValue]="ageGroup.id">{{ ageGroup.displayName }} ({{ ageGroup.minAge }}–{{ ageGroup.maxAge }} yaş)</option>}</select></label><label>Tahmini gün<input type="number" [(ngModel)]="learningPathDraft.estimatedDays" name="pathDays" min="1" /></label><label class="wide">Açıklama<textarea [(ngModel)]="learningPathDraft.description" name="pathDescription" maxlength="5000"></textarea></label><label class="check"><input type="checkbox" [(ngModel)]="learningPathDraft.isActive" name="pathActive" /> Aktif</label></div></div><div class="dialog-footer"><button type="button" class="secondary" (click)="learningPathEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form></div>
            }
            <div class="data-card"><table class="data-table"><thead><tr><th>Yol</th><th>Düğüm</th><th>Tahmini gün</th><th></th></tr></thead><tbody>@for (item of learningPaths(); track item.id) {<tr><td>{{ item.name }}</td><td>{{ item.totalNodes }}</td><td>{{ item.estimatedDays }}</td><td class="actions"><button type="button" (click)="selectLearningPath(item)">Düğümleri gör</button><button type="button" (click)="editLearningPath(item)">Düzenle</button><button type="button" class="danger" (click)="deleteLearningPath(item)">Sil</button></td></tr>} @empty {<tr><td colspan="4" class="empty">Öğrenme yolu bulunamadı.</td></tr>}</tbody></table></div>
            @if (selectedPath(); as details) {
              <div class="data-card">
                <div class="flex items-center justify-between"><strong>{{ details.template.name }} - düğümler</strong><button type="button" class="primary" (click)="startNodeCreate()">Yeni düğüm</button></div>
                @if (nodeEditing()) {<div class="dialog-backdrop" role="presentation" (click)="nodeEditing.set(false)"><form class="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="node-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveNode()" (click)="$event.stopPropagation()"><div class="dialog-header"><div><p class="dialog-eyebrow">Öğrenme yolu düğümü</p><h3 id="node-dialog-title">{{ nodeEditingId ? 'Düğümü düzenle' : 'Yeni düğüm' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="nodeEditing.set(false)"><mat-icon>close</mat-icon></button></div><div class="dialog-body"><div class="form-grid"><label>Başlık<input [(ngModel)]="nodeDraft.title" name="nodeTitle" required maxlength="200" /></label><label>Tür<input [(ngModel)]="nodeDraft.nodeType" name="nodeType" required maxlength="100" /></label><label>Üst düğüm ID<input [(ngModel)]="nodeDraft.parentNodeId" name="nodeParent" /></label><label>Sıra<input type="number" [(ngModel)]="nodeDraft.order" name="nodeOrder" /></label><label>İçerik türü<input [(ngModel)]="nodeDraft.contentType" name="nodeContentType" /></label><label>İçerik ID<input [(ngModel)]="nodeDraft.contentId" name="nodeContentId" /></label></div></div><div class="dialog-footer"><button type="button" class="secondary" (click)="nodeEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form></div>}
                <table class="data-table"><thead><tr><th>Başlık</th><th>Tür</th><th>Sıra</th><th>İçerik</th><th>Önkoşul</th><th></th></tr></thead><tbody>@for (node of details.nodes; track node.id) {<tr><td>{{ node.title }}</td><td>{{ node.nodeType }}</td><td>{{ node.order }}</td><td>{{ node.contents.length }}</td><td>{{ node.prerequisiteNodeIds.length }}</td><td class="actions"><button type="button" (click)="manageNode(node)">İçerik/önkoşul</button><button type="button" (click)="editNode(node)">Düzenle</button><button type="button" class="danger" (click)="deleteNode(node)">Sil</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Düğüm bulunamadı.</td></tr>}</tbody></table>
                @if (activeNode(); as node) {
                  <div class="dialog-backdrop" role="presentation" (click)="activeNode.set(null)"><div class="dialog-panel dialog-panel-wide" role="dialog" aria-modal="true" aria-labelledby="node-manage-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (click)="$event.stopPropagation()"><div class="dialog-header"><div><p class="dialog-eyebrow">Düğüm yönetimi</p><h3 id="node-manage-dialog-title">{{ node.title }} - içerik ve önkoşullar</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="activeNode.set(null)"><mat-icon>close</mat-icon></button></div><div class="dialog-body space-y-4">
                    <div class="data-card"><h4>Düğüm içerikleri</h4><div class="actions"><button type="button" class="primary" (click)="startNodeContentCreate()">Yeni içerik</button></div>@if (nodeContentEditing()) {<div class="dialog-backdrop dialog-backdrop-nested" role="presentation" (click)="nodeContentEditing.set(false)"><form class="dialog-panel" role="dialog" aria-modal="false" aria-labelledby="node-content-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveNodeContent()" (click)="$event.stopPropagation()"><div class="dialog-header"><div><p class="dialog-eyebrow">Düğüm içeriği</p><h3 id="node-content-dialog-title">{{ nodeContentEditingId ? 'İçeriği düzenle' : 'Yeni içerik' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="nodeContentEditing.set(false)"><mat-icon>close</mat-icon></button></div><div class="dialog-body"><div class="form-grid"><label>Egzersiz ID<input [(ngModel)]="nodeContentDraft.exerciseId" name="nodeContentExercise" /></label><label>Okuma metni ID<input [(ngModel)]="nodeContentDraft.readingTextId" name="nodeContentReading" /></label><label class="wide">Açıklama<textarea [(ngModel)]="nodeContentDraft.description" name="nodeContentDescription" maxlength="2000"></textarea></label></div></div><div class="dialog-footer"><button type="button" class="secondary" (click)="nodeContentEditing.set(false)">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div></form></div>}<table class="data-table"><thead><tr><th>Egzersiz</th><th>Okuma metni</th><th>Açıklama</th><th></th></tr></thead><tbody>@for (content of node.contents; track content.id) {<tr><td>{{ content.exerciseId || '-' }}</td><td>{{ content.readingTextId || '-' }}</td><td>{{ content.description }}</td><td class="actions"><button type="button" (click)="editNodeContent(content)">Düzenle</button><button type="button" class="danger" (click)="deleteNodeContent(content)">Sil</button></td></tr>} @empty {<tr><td colspan="4" class="empty">İçerik bulunamadı.</td></tr>}</tbody></table></div>
                    <div class="data-card"><h4>Önkoşullar</h4><div class="form-actions"><select [(ngModel)]="prerequisiteNodeId" name="prerequisiteNode"><option value="">Önkoşul düğümü seçin</option>@for (candidate of details.nodes; track candidate.id) {@if (candidate.id !== node.id) {<option [value]="candidate.id">{{ candidate.title }}</option>}}</select><button type="button" class="primary" (click)="addPrerequisite()" [disabled]="!prerequisiteNodeId || saving()">Ekle</button></div>@for (prerequisiteId of node.prerequisiteNodeIds; track prerequisiteId) {<div class="question-row flex items-center justify-between"><span>{{ nodeTitle(details.nodes, prerequisiteId) }}</span><button type="button" class="danger" (click)="deletePrerequisite(prerequisiteId)">Kaldır</button></div>} @empty {<p class="empty">Önkoşul bulunamadı.</p>}</div>
                  </div></div></div>
                }
              </div>
            }
          }
        </section>
      }

      @if (selectedCoreTab() === 'achievements') {
        <section class="space-y-4"><div class="flex items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">Başarı tanımları</h2><p class="muted">Rozet, kriter, tekrar davranışı ve XP ödülü.</p></div><button type="button" class="primary" (click)="startAchievementCreate()">Yeni başarı</button></div>
          @if (achievementStats(); as stats) { <div class="grid grid-cols-2 gap-3 md:grid-cols-4"><div class="metric"><span>Toplam</span><strong>{{ stats.totalCount }}</strong></div><div class="metric"><span>Aktif</span><strong>{{ stats.activeCount }}</strong></div><div class="metric"><span>Bronz/Gümüş</span><strong>{{ stats.bronzeCount }} / {{ stats.silverCount }}</strong></div><div class="metric"><span>Altın/Diamond</span><strong>{{ stats.goldCount }} / {{ stats.diamondCount }}</strong></div></div> }
          @if (achievementEditing()) { <div class="dialog-backdrop" role="presentation" (click)="achievementEditing.set(false)"><form class="dialog-panel dialog-panel-wide" role="dialog" aria-modal="true" aria-labelledby="achievement-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveAchievement()" (click)="$event.stopPropagation()"><div class="dialog-header"><div><p class="dialog-eyebrow">Başarı tanımı</p><h3 id="achievement-dialog-title">{{ achievementEditingId ? 'Başarıyı düzenle' : 'Yeni başarı' }}</h3></div><button type="button" class="icon-button" aria-label="Dialogu kapat" (click)="achievementEditing.set(false)"><mat-icon>close</mat-icon></button></div><div class="dialog-body"><div class="form-grid"><label>Ad<input [(ngModel)]="achievementDraft.name" name="achievementName" required maxlength="200" /></label><label>Kategori<input [(ngModel)]="achievementDraft.category" name="achievementCategory" required maxlength="100" /></label><label>Seviye<select [(ngModel)]="achievementDraft.tier" name="achievementTier" required><option value="Bronze">Bronz</option><option value="Silver">Gümüş</option><option value="Gold">Altın</option><option value="Diamond">Elmas</option><option value="Special">Özel</option></select></label><label>Rozet sembolü<input [(ngModel)]="achievementDraft.iconEmoji" name="achievementEmoji" maxlength="20" /></label><label>Kriter türü<select [(ngModel)]="achievementDraft.criteriaType" name="achievementCriteriaType" required (ngModelChange)="resetAchievementCriteriaTarget()">@for (criterion of achievementCriteria; track criterion.value) { <option [value]="criterion.value">{{ criterion.label }}</option> }</select></label><label>{{ achievementCriteriaValueLabel() }}<input [type]="achievementCriteriaInputType()" [(ngModel)]="achievementCriteriaTarget" name="achievementCriteriaTarget" required [attr.min]="achievementCriteriaMin()" [attr.max]="achievementCriteriaMax()" /></label><p class="wide muted">{{ achievementCriteriaHelp() }}</p><label>Tetikleyici<input [(ngModel)]="achievementDraft.triggerType" name="achievementTriggerType" /></label><label>XP ödülü<input type="number" [(ngModel)]="achievementDraft.xpReward" name="achievementXp" min="0" /></label><label class="wide">Açıklama<textarea [(ngModel)]="achievementDraft.description" name="achievementDescription" required maxlength="5000"></textarea></label><p class="wide muted">Her rozet öğrenciye yalnızca bir kez verilir; tekrarlanabilir rozetler şu anda desteklenmez.</p><label class="check"><input type="checkbox" [(ngModel)]="achievementDraft.isActive" name="achievementActive" /> Aktif</label></div></div><div class="dialog-footer"><button type="button" class="secondary" (click)="achievementEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form></div> }
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
    .plan-card, .plan-day, .plan-entry { display: grid; gap: .75rem; }
    .plan-card { margin-top: 1rem; border: 1px solid color-mix(in srgb, var(--ui-brand) 24%, var(--ui-border)); border-radius: .8rem; padding: 1rem; background: color-mix(in srgb, var(--ui-surface) 96%, var(--ui-brand)); }
    .plan-day { border-top: 1px solid var(--ui-border); padding-top: .85rem; }
    .plan-entry { grid-template-columns: minmax(0, 1fr) auto; align-items: end; border: 1px solid var(--ui-border); border-radius: .65rem; padding: .75rem; background: var(--ui-surface); }
    .plan-entry .form-grid { min-width: 0; }
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
export class SpeedReadingCatalogComponent implements OnInit, OnDestroy {
  private readonly service = inject(SpeedReadingAdminService); private readonly route = inject(ActivatedRoute); private readonly authService = inject(AuthService); private readonly toaster = inject(ToasterService);
  readonly coreTabs: { value: CoreTab; label: string }[] = [{ value: 'content', label: 'İçerik katalogları' }, { value: 'programs', label: 'Programlar' }, { value: 'achievements', label: 'Başarılar' }];
  readonly visibleCoreTabs = computed(() => this.coreTabs.filter(tab => tab.value === 'content' ? this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingContentManage) : tab.value === 'programs' ? this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingProgramManage) : this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingGamificationManage)));
  readonly contentTabs: { value: ContentTab; label: string }[] = [{ value: 'exercise-types', label: 'Egzersiz türleri' }, { value: 'exercises', label: 'Egzersizler' }, { value: 'reading-texts', label: 'Okuma metinleri' }];
  readonly programTabs: { value: ProgramTab; label: string }[] = [{ value: 'programs', label: 'Program şablonları' }, { value: 'learning-paths', label: 'Öğrenme yolları' }];
  readonly selectedCoreTab = signal<CoreTab>('content'); readonly contentTab = signal<ContentTab>('exercise-types'); readonly programTab = signal<ProgramTab>('programs'); readonly loading = signal(false); readonly saving = signal(false); readonly error = signal('');
  readonly exerciseTypes = signal<{ items: SpeedReadingExerciseType[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 }); readonly availableExerciseTypes = signal<SpeedReadingExerciseType[]>([]); readonly exercises = signal<{ items: SpeedReadingExercise[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }); readonly availableExercises = signal<SpeedReadingExercise[]>([]); readonly adaptiveTransferTexts = signal<SpeedReadingReadingText[]>([]); readonly readingTexts = signal<SpeedReadingReadingText[]>([]); readonly readingTextCategories = signal<string[]>([]); readonly readingTextDifficultyLevels = signal<number[]>([]); readonly ageGroups = signal<SpeedReadingAgeGroup[]>([]); readonly selectedReadingText = signal<SpeedReadingReadingTextDetails | null>(null); readonly draftQualityMetrics = signal<SpeedReadingReadingTextQualityMetrics | null>(null); readonly qualityPreviewLoading = signal(false); readonly qualityPreviewError = signal(''); readonly readingTextImportResult = signal<SpeedReadingReadingTextImportResult | null>(null); readonly programs = signal<SpeedReadingProgramTemplate[]>([]); readonly learningPaths = signal<SpeedReadingLearningPathTemplate[]>([]); readonly selectedPath = signal<{ template: SpeedReadingLearningPathTemplate; nodes: SpeedReadingLearningPathNode[] } | null>(null); readonly activeNode = signal<SpeedReadingLearningPathNode | null>(null); readonly achievements = signal<{ items: SpeedReadingAchievement[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 50 });
  readonly typeEditing = signal(false); readonly exerciseEditing = signal(false); readonly readingTextEditing = signal(false); readonly questionEditing = signal(false); readonly programEditing = signal(false); readonly learningPathEditing = signal(false); readonly nodeEditing = signal(false); readonly nodeContentEditing = signal(false); readonly achievementEditing = signal(false); readonly achievementStats = signal<SpeedReadingAchievementStats | null>(null);
  typeEditingId: string | null = null; exerciseEditingId: string | null = null; readingTextEditingId: string | null = null; questionEditingId: string | null = null; programEditingId: string | null = null; learningPathEditingId: string | null = null; nodeEditingId: string | null = null; nodeContentEditingId: string | null = null; achievementEditingId: string | null = null; prerequisiteNodeId = ''; contentPage = 1; achievementPage = 1; readingTextPage = 1; readonly readingTextPageSize = 25; readingTextSearch = ''; readingTextCategory = ''; readingTextDifficulty = ''; readingTextAgeGroupId = ''; readingTextStatus = 'all'; onlyTextsWithQuestions = false; private qualityPreviewTimer?: ReturnType<typeof setTimeout>; private qualityPreviewSequence = 0;
  typeDraft: SpeedReadingExerciseTypeRequest = this.emptyType(); exerciseDraft: SpeedReadingExerciseRequest = this.emptyExercise(); exerciseSettings: ExerciseSettingsDraft = this.emptyExerciseSettings(); readingTextDraft: SpeedReadingReadingTextRequest = this.emptyReadingText(); readingTextContent = ''; readingTags = ''; questionDraft: SpeedReadingReadingQuestionRequest = this.emptyQuestion(''); programDraft: SpeedReadingProgramTemplateRequest = this.emptyProgram(); programPlan: ProgramPlanWeekDraft[] = []; adaptivePolicyDraft: AdaptivePolicyDraft = this.emptyAdaptivePolicy(); learningPathDraft: SpeedReadingLearningPathTemplateRequest = this.emptyLearningPath(); nodeDraft: SpeedReadingLearningPathNodeRequest = this.emptyNode(''); nodeContentDraft: SpeedReadingLearningPathNodeContentRequest = this.emptyNodeContent(''); achievementDraft: SpeedReadingAchievementRequest = this.emptyAchievement();
  private exerciseConfigExtras: Record<string, unknown> = {}; private exerciseEngineConfigExtras: Record<string, unknown> = {}; private exerciseTimingExtras: Record<string, unknown> = {}; private exerciseGridExtras: Record<string, unknown> = {}; private exerciseContentExtras: Record<string, unknown> = {}; private exerciseVocabularyExtras: Record<string, unknown> = {}; private programPatternExtras: Record<string, unknown> = {}; private nextPlanItemId = 1;
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
  readonly exerciseEngines = [
    { value: 'grid_interaction', label: 'Izgara etkileşimi' }, { value: 'motion_path', label: 'Göz hareketi' },
    { value: 'text_stream', label: 'Hızlı metin akışı (RSVP)' }, { value: 'text_fade', label: 'Metin solma' },
    { value: 'word_highlight', label: 'Kelime vurgulama' }, { value: 'visual_expansion', label: 'Görsel genişletme' },
    { value: 'scan_find', label: 'Tarama ve bulma' }, { value: 'reading_comprehension', label: 'Okuma ve anlama' },
    { value: 'exam_simulation', label: 'Sınav simülasyonu' }, { value: 'free_reading', label: 'Serbest okuma' },
    { value: 'regression_reduction', label: 'Geri dönüş azaltma' }, { value: 'subvocalization_reduction', label: 'İç ses azaltma' },
    { value: 'visualization', label: 'Görselleştirme' }, { value: 'attention_training', label: 'Dikkat eğitimi' },
    { value: 'focus', label: 'Odak eğitimi' }, { value: 'vocabulary_builder', label: 'Kelime geliştirme' },
    { value: 'error_analysis', label: 'Hata analizi' }, { value: 'adaptive_fluency', label: 'Adaptif akıcılık' },
    { value: 'scanning', label: 'Hızlı tarama' }, { value: 'skimming', label: 'Göz gezdirme' }
  ] as const;
  readonly achievementCriteria: AchievementCriteriaDefinition[] = [
    { value: 'activity_count', label: 'Tamamlanan çalışma sayısı', property: 'count', input: 'number', min: 1, help: 'Öğrencinin tamamladığı doğrulanmış çalışma sayısı.' },
    { value: 'exercise_count', label: 'Tamamlanan egzersiz sayısı', property: 'count', input: 'number', min: 1, help: 'Doğrulanmış egzersiz oturumlarının toplamı.' },
    { value: 'streak', label: 'Günlük seri', property: 'days', input: 'number', min: 1, help: 'Aralıksız çalışma günlerinin sayısı.' },
    { value: 'level_reached', label: 'Seviye', property: 'level', input: 'number', min: 1, help: 'Öğrencinin ulaştığı oyunlaştırma seviyesi.' },
    { value: 'total_xp', label: 'Toplam XP', property: 'xp', input: 'number', min: 1, help: 'Doğrulanmış çalışmalardan kazanılan toplam XP.' },
    { value: 'reading_minutes', label: 'Okuma süresi', property: 'minutes', input: 'number', min: 1, help: 'Doğrulanmış çalışmalardaki toplam dakika.' },
    { value: 'wpm_reached', label: 'En yüksek okuma hızı', property: 'wpm', input: 'number', min: 1, help: 'Ölçülen en yüksek kelime/dakika değeri.' },
    { value: 'comprehension_score', label: 'En yüksek anlama puanı', property: 'score', input: 'number', min: 0, max: 100, allowDecimal: true, help: 'Ölçülen en yüksek anlama yüzdesi.' },
    { value: 'reading_count', label: 'Okuma oturumu sayısı', property: 'count', input: 'number', min: 1, help: 'Ölçülen okuma oturumlarının toplamı.' },
    { value: 'rsvp_count', label: 'RSVP oturumu sayısı', property: 'count', input: 'number', min: 1, help: 'Ölçülen RSVP oturumlarının toplamı.' },
    { value: 'rsvp_wpm', label: 'RSVP hızı', property: 'wpm', input: 'number', min: 1, help: 'Ölçülen en yüksek RSVP kelime/dakika değeri.' },
    { value: 'rsvp_comprehension', label: 'RSVP anlama puanı', property: 'score', input: 'number', min: 0, max: 100, allowDecimal: true, help: 'Ölçülen en yüksek RSVP anlama yüzdesi.' },
    { value: 'exercise_type_first', label: 'Egzersiz türünü ilk kez tamamlama', property: 'type', input: 'text', help: 'Egzersiz türünün sistemdeki adı; örneğin RSVP veya SchulteTable.' },
    { value: 'exercise_variety', label: 'Farklı egzersiz türü sayısı', property: 'types', input: 'number', min: 1, help: 'En az bir kez tamamlanmış farklı egzersiz türü sayısı.' }
  ];
  achievementCriteriaTarget = '1';

  ngOnInit(): void { const defaultTab = this.route.snapshot.data['defaultTab'] as CoreTab | undefined; if (defaultTab) this.selectedCoreTab.set(defaultTab); this.loadAgeGroups(); this.loadCurrentTab(); }
  ngOnDestroy(): void { if (this.qualityPreviewTimer) clearTimeout(this.qualityPreviewTimer); }
  @HostListener('document:keydown.escape')
  closeTopDialog(): void {
    if (this.nodeContentEditing()) this.nodeContentEditing.set(false);
    else if (this.activeNode()) this.activeNode.set(null);
    else if (this.nodeEditing()) this.nodeEditing.set(false);
    else if (this.learningPathEditing()) this.learningPathEditing.set(false);
    else if (this.programEditing()) this.programEditing.set(false);
    else if (this.achievementEditing()) this.achievementEditing.set(false);
    else if (this.questionEditing()) this.questionEditing.set(false);
    else if (this.readingTextEditing()) this.closeReadingTextDialog();
    else if (this.exerciseEditing()) this.exerciseEditing.set(false);
    else if (this.typeEditing()) this.typeEditing.set(false);
  }
  selectCoreTab(tab: CoreTab): void { this.selectedCoreTab.set(tab); this.error.set(''); this.loadCurrentTab(); }
  selectContentTab(tab: ContentTab): void { this.contentTab.set(tab); this.cancelContentEdit(); this.loadCurrentTab(); }
  selectProgramTab(tab: ProgramTab): void { this.programTab.set(tab); this.cancelProgramEdit(); this.loadCurrentTab(); }
  loadCurrentTab(): void { if (this.selectedCoreTab() === 'content') { if (this.contentTab() === 'exercise-types') this.loadExerciseTypes(); if (this.contentTab() === 'exercises') this.loadExercises(); if (this.contentTab() === 'reading-texts') this.loadReadingTexts(); } else if (this.selectedCoreTab() === 'programs') { if (this.programTab() === 'programs') this.loadPrograms(); else this.loadLearningPaths(); } else this.loadAchievements(); }
  startContentCreate(): void { if (this.contentTab() === 'exercise-types') { this.typeEditingId = null; this.typeDraft = this.emptyType(); this.typeEditing.set(true); } else if (this.contentTab() === 'exercises') { this.exerciseEditingId = null; this.exerciseDraft = this.emptyExercise(); this.exerciseSettings = this.emptyExerciseSettings(); this.exerciseConfigExtras = {}; this.exerciseEngineConfigExtras = {}; this.exerciseTimingExtras = {}; this.exerciseGridExtras = {}; this.exerciseContentExtras = {}; this.exerciseVocabularyExtras = {}; this.exerciseEditing.set(true); } else { this.readingTextEditingId = null; this.readingTextDraft = this.emptyReadingText(); this.readingTextContent = ''; this.readingTags = ''; this.selectedReadingText.set(null); this.draftQualityMetrics.set(null); this.questionEditing.set(false); this.qualityPreviewError.set(''); this.readingTextEditing.set(true); } }
  loadExerciseTypes(): void { this.service.getExerciseTypes(this.contentPage).subscribe({ next: value => this.exerciseTypes.set(value), error: () => this.error.set('Egzersiz türleri yüklenemedi.') }); }
  editExerciseType(item: SpeedReadingExerciseType): void { this.typeEditingId = item.id; this.typeDraft = { name: item.name, displayName: item.displayName, description: item.description, iconName: item.iconName, colorCode: item.colorCode, sortOrder: item.sortOrder, isActive: item.isActive, engineType: this.normalizeEngineType(item.engineType), categoryId: item.categoryId }; this.typeEditing.set(true); }
  saveExerciseType(): void { const action = this.typeEditingId ? this.service.updateExerciseType(this.typeEditingId, this.typeDraft) : this.service.createExerciseType(this.typeDraft); this.run(action, () => { this.typeEditing.set(false); this.loadExerciseTypes(); }, 'Egzersiz türü kaydedilemedi.'); }
  async deleteExerciseType(item: SpeedReadingExerciseType): Promise<void> { if (!await this.toaster.confirm('Bu egzersiz türü silinsin mi?', { title: 'Egzersiz türünü sil' })) return; this.run(this.service.deleteExerciseType(item.id), () => this.loadExerciseTypes(), 'Egzersiz türü silinemedi.'); }
  loadExercises(): void { this.service.getExercises(this.contentPage).subscribe({ next: value => this.exercises.set(value), error: () => this.error.set('Egzersizler yüklenemedi.') }); this.service.getAllExerciseTypes().subscribe({ next: value => this.availableExerciseTypes.set(value), error: () => this.error.set('Egzersiz türleri yüklenemedi.') }); this.service.getReadingTexts({ isActive: true, onlyWithQuestions: true }).subscribe({ next: value => this.adaptiveTransferTexts.set(value), error: () => this.adaptiveTransferTexts.set([]) }); }
  private loadAgeGroups(): void { this.service.getActiveAgeGroups().subscribe({ next: value => this.ageGroups.set(value), error: () => this.ageGroups.set([]) }); }
  editExercise(item: SpeedReadingExercise): void { this.exerciseEditingId = item.id; this.exerciseDraft = { title: item.title, description: item.description, difficultyLevel: item.difficultyLevel, exerciseTypeId: item.exerciseTypeId, configurationJson: item.configurationJson, targetAgeGroupConfigurationId: item.targetAgeGroupConfigurationId, isActive: item.isActive }; this.readExerciseConfiguration(item.configurationJson); this.exerciseEditing.set(true); }
  onExerciseTypeChanged(): void { const engine = this.selectedExerciseEngine(); if (engine === 'adaptive_fluency' && !this.exerciseSettings.repeatPurposeOne) this.exerciseSettings = this.emptyExerciseSettings(); }
  selectedExerciseEngine(): string { const selected = this.availableExerciseTypes().find(item => item.id === this.exerciseDraft.exerciseTypeId); return selected ? this.normalizeEngineType(selected.engineType) : ''; }
  selectedExerciseEngineLabel(): string { const engine = this.selectedExerciseEngine(); return this.exerciseEngines.find(item => item.value === engine)?.label ?? (engine || 'Önce egzersiz türü seçin'); }
  usesChunkSize(): boolean { return ['text_stream', 'word_highlight', 'free_reading', 'regression_reduction', 'subvocalization_reduction', 'scanning', 'skimming', 'adaptive_fluency'].includes(this.selectedExerciseEngine()); }
  eligibleTransferTexts(): SpeedReadingReadingText[] { return this.adaptiveTransferTexts().filter(item => item.difficultyLevel === this.exerciseDraft.difficultyLevel && (item.targetAgeGroupConfigurationId === null || item.targetAgeGroupConfigurationId === this.exerciseDraft.targetAgeGroupConfigurationId)); }
  saveExercise(): void { const configurationJson = this.buildExerciseConfiguration(); if (!configurationJson) return; const request = { ...this.exerciseDraft, configurationJson }; const action = this.exerciseEditingId ? this.service.updateExercise(this.exerciseEditingId, request) : this.service.createExercise(request); this.run(action, () => { this.exerciseEditing.set(false); this.loadExercises(); }, 'Egzersiz kaydedilemedi.'); }
  async deleteExercise(item: SpeedReadingExercise): Promise<void> { if (!await this.toaster.confirm('Bu egzersiz silinsin mi?', { title: 'Egzersizi sil' })) return; this.run(this.service.deleteExercise(item.id), () => this.loadExercises(), 'Egzersiz silinemedi.'); }
  loadReadingTexts(): void { const isActive = this.readingTextStatus === 'active' ? true : this.readingTextStatus === 'passive' ? false : undefined; this.service.getReadingTexts({ searchTerm: this.readingTextSearch.trim() || undefined, category: this.readingTextCategory || undefined, difficultyLevel: this.readingTextDifficulty ? Number(this.readingTextDifficulty) : undefined, targetAgeGroupId: this.readingTextAgeGroupId || undefined, isActive, onlyWithQuestions: this.onlyTextsWithQuestions || undefined }).subscribe({ next: value => { this.readingTexts.set(value); this.readingTextPage = Math.min(this.readingTextPage, this.readingTextTotalPages()); }, error: () => this.error.set('Okuma metinleri yüklenemedi.') }); this.service.getAllExercises().subscribe({ next: value => this.availableExercises.set(value), error: () => this.availableExercises.set([]) }); this.service.getReadingTextCategories().subscribe({ next: value => this.readingTextCategories.set(value), error: () => this.readingTextCategories.set([]) }); this.service.getReadingTextDifficultyLevels().subscribe({ next: value => this.readingTextDifficultyLevels.set(value), error: () => this.readingTextDifficultyLevels.set([]) }); }
  applyReadingTextFilters(): void { this.readingTextPage = 1; this.loadReadingTexts(); }
  clearReadingTextFilters(): void { this.readingTextSearch = ''; this.readingTextCategory = ''; this.readingTextDifficulty = ''; this.readingTextAgeGroupId = ''; this.readingTextStatus = 'all'; this.onlyTextsWithQuestions = false; this.readingTextPage = 1; this.loadReadingTexts(); }
  importErrors(result: SpeedReadingReadingTextImportResult): string[] { return result.errors.filter((error): error is string => Boolean(error)); }
  visibleReadingTexts(): SpeedReadingReadingText[] { const start = (this.readingTextPage - 1) * this.readingTextPageSize; return this.readingTexts().slice(start, start + this.readingTextPageSize); }
  readingTextTotalPages(): number { return Math.max(1, Math.ceil(this.readingTexts().length / this.readingTextPageSize)); }
  changeReadingTextPage(delta: number): void { this.readingTextPage = Math.min(this.readingTextTotalPages(), Math.max(1, this.readingTextPage + delta)); }
  editReadingText(item: SpeedReadingReadingText): void { this.service.getReadingText(item.id).subscribe({ next: detail => { this.readingTextEditingId = item.id; this.readingTextDraft = { title: detail.title, content: detail.content, wordCount: detail.wordCount, category: detail.category, difficultyLevel: detail.difficultyLevel, targetAgeGroupConfigurationId: detail.targetAgeGroupConfigurationId, language: detail.language, isActive: detail.isActive, tags: detail.tags.join(', '), recommendedMinLevel: detail.recommendedMinLevel, recommendedMaxLevel: detail.recommendedMaxLevel, exerciseId: detail.exerciseId }; this.readingTextContent = detail.content; this.readingTags = detail.tags.join(', '); this.selectedReadingText.set(detail); this.draftQualityMetrics.set(detail.qualityMetrics ?? null); this.qualityPreviewError.set(''); this.readingTextEditing.set(true); this.queueQualityPreview(); }, error: () => this.error.set('Okuma metni ayrıntısı yüklenemedi.') }); }
  saveReadingText(): void { if (this.readingTextDraft.isActive) { this.previewQualityThenSave(); return; } this.persistReadingText(); }
  async deleteReadingText(item: SpeedReadingReadingText): Promise<void> { if (!await this.toaster.confirm('Bu okuma metni silinsin mi?', { title: 'Okuma metnini sil' })) return; this.run(this.service.deleteReadingText(item.id), () => this.loadReadingTexts(), 'Okuma metni silinemedi.'); }
  onReadingTextImport(event: Event, format: 'csv' | 'excel'): void { const input = event.target as HTMLInputElement; const file = input.files?.[0]; if (!file) return; this.saving.set(true); this.error.set(''); this.readingTextImportResult.set(null); this.service.importReadingTexts(file, format).pipe(finalize(() => this.saving.set(false))).subscribe({ next: result => { this.readingTextImportResult.set(result); this.loadReadingTexts(); }, error: () => this.error.set('Okuma metni içe aktarılamadı.') }); input.value = ''; }
  exportReadingText(item: SpeedReadingReadingText, format: 'pdf' | 'docx'): void { this.service.exportReadingText(item.id, format).subscribe({ next: blob => this.download(blob, `${item.title.replace(/[^a-z0-9-_]+/gi, '-').replace(/^-|-$/g, '') || 'okuma-metni'}.${format}`), error: () => this.error.set('Okuma metni dışa aktarılamadı.') }); }
  calculatedWordCount(): number { return this.readingTextContent.trim() ? this.readingTextContent.trim().split(/\s+/).length : 0; }
  paragraphCount(): number { return this.readingTextContent.trim() ? this.readingTextContent.trim().split(/\n\s*\n/).filter(Boolean).length : 0; }
  estimatedReadingMinutes(wordsPerMinute: number): string { const minutes = this.calculatedWordCount() / wordsPerMinute; return minutes < 0.1 ? '<0,1' : minutes.toLocaleString('tr-TR', { minimumFractionDigits: 1, maximumFractionDigits: 1 }); }
  closeReadingTextDialog(): void { this.readingTextEditing.set(false); this.questionEditing.set(false); this.selectedReadingText.set(null); this.draftQualityMetrics.set(null); this.qualityPreviewError.set(''); if (this.qualityPreviewTimer) clearTimeout(this.qualityPreviewTimer); }
  startQuestionCreate(): void { const readingTextId = this.selectedReadingText()?.id; if (!readingTextId) return; this.questionEditingId = null; this.questionDraft = this.emptyQuestion(readingTextId); this.questionEditing.set(true); this.queueQualityPreview(); }
  editQuestion(item: SpeedReadingReadingQuestion): void { this.questionEditingId = item.id; this.questionDraft = { readingTextId: this.selectedReadingText()?.id ?? '', questionText: item.questionText, type: item.type, bloomLevel: item.bloomLevel, difficultyLevel: item.difficultyLevel, explanation: item.explanation, optionA: item.optionA, optionB: item.optionB, optionC: item.optionC, optionD: item.optionD, correctAnswer: item.correctAnswer, orderIndex: item.orderIndex }; this.questionEditing.set(true); this.queueQualityPreview(); }
  saveQuestion(): void { const readingTextId = this.selectedReadingText()?.id; if (!readingTextId) return; const updateRequest: SpeedReadingReadingQuestionUpdateRequest = { questionText: this.questionDraft.questionText, type: this.questionDraft.type, bloomLevel: this.questionDraft.bloomLevel, difficultyLevel: this.questionDraft.difficultyLevel, explanation: this.questionDraft.explanation, optionA: this.questionDraft.optionA, optionB: this.questionDraft.optionB, optionC: this.questionDraft.optionC, optionD: this.questionDraft.optionD, correctAnswer: this.questionDraft.correctAnswer, orderIndex: this.questionDraft.orderIndex }; const action = this.questionEditingId ? this.service.updateReadingQuestion(this.questionEditingId, updateRequest) : this.service.createReadingQuestion({ ...this.questionDraft, readingTextId }); this.run(action, () => this.refreshReadingText(readingTextId), 'Okuma sorusu kaydedilemedi.'); }
  async deleteQuestion(item: SpeedReadingReadingQuestion): Promise<void> { if (!await this.toaster.confirm('Bu soru silinsin mi?', { title: 'Soruyu sil' })) return; this.run(this.service.deleteReadingQuestion(item.id), () => { const readingTextId = this.selectedReadingText()?.id; if (readingTextId) this.refreshReadingText(readingTextId); }, 'Okuma sorusu silinemedi.'); }
  private refreshReadingText(id: string): void { this.questionEditing.set(false); this.service.getReadingText(id).subscribe({ next: value => { this.selectedReadingText.set(value); this.draftQualityMetrics.set(value.qualityMetrics ?? null); this.queueQualityPreview(); }, error: () => this.error.set('Okuma metni ayrıntısı yüklenemedi.') }); }
  currentQualityMetrics(): SpeedReadingReadingTextQualityMetrics | null { return this.draftQualityMetrics() ?? this.selectedReadingText()?.qualityMetrics ?? null; }
  publicationBlockers(): string[] { return this.currentQualityMetrics()?.publicationBlockers ?? []; }
  answerChoiceLabel(level: number): string { return ['A', 'B', 'C', 'D'][level - 1] ?? '?'; }
  questionTypeLabel(type: number): string { return ({ 1: 'Gerçek anlam', 2: 'Çıkarım', 3: 'Değerlendirme' } as Record<number, string>)[type] ?? 'Bilinmeyen'; }
  private previewQualityThenSave(): void {
    this.qualityPreviewLoading.set(true);
    this.qualityPreviewError.set('');
    this.service.previewReadingTextQuality(this.qualityPreviewRequest()).subscribe({
      next: quality => {
        this.draftQualityMetrics.set(quality);
        this.qualityPreviewLoading.set(false);
        if (quality.publicationBlockers?.length) {
          this.error.set('Metin öğrenci kullanımına açılamaz; kalite engellerini düzeltin veya taslak olarak kaydedin.');
          return;
        }
        this.persistReadingText();
      },
      error: () => {
        this.qualityPreviewLoading.set(false);
        this.error.set('Kalite kontrolü tamamlanamadığı için aktif metin kaydedilemedi.');
      }
    });
  }
  private persistReadingText(): void {
    const request = { ...this.readingTextDraft, content: this.readingTextContent, wordCount: this.calculatedWordCount(), tags: this.readingTags };
    const action = this.readingTextEditingId ? this.service.updateReadingText(this.readingTextEditingId, request) : this.service.createReadingText(request);
    this.saving.set(true); this.error.set('');
    action.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: saved => {
        this.readingTextEditingId = saved.id;
        this.loadReadingTexts();
        this.service.getReadingText(saved.id).subscribe({
          next: detail => { this.selectedReadingText.set(detail); this.draftQualityMetrics.set(detail.qualityMetrics ?? null); this.queueQualityPreview(); },
          error: () => this.error.set('Metin kaydedildi ancak soru bilgileri yenilenemedi.')
        });
      },
      error: () => this.error.set('Okuma metni kaydedilemedi. Aktif metin için kalite engellerini gözden geçirin.')
    });
  }
  queueQualityPreview(): void {
    if (this.qualityPreviewTimer) clearTimeout(this.qualityPreviewTimer);
    if (!this.readingTextContent.trim()) {
      this.draftQualityMetrics.set(null);
      this.qualityPreviewError.set('');
      this.qualityPreviewLoading.set(false);
      return;
    }
    this.qualityPreviewTimer = setTimeout(() => {
      const sequence = ++this.qualityPreviewSequence;
      this.qualityPreviewLoading.set(true);
      this.qualityPreviewError.set('');
      this.service.previewReadingTextQuality(this.qualityPreviewRequest()).subscribe({
        next: quality => {
          if (sequence !== this.qualityPreviewSequence) return;
          this.draftQualityMetrics.set(quality);
          this.qualityPreviewLoading.set(false);
        },
        error: () => {
          if (sequence !== this.qualityPreviewSequence) return;
          this.qualityPreviewLoading.set(false);
          this.qualityPreviewError.set('Taslak kalite kontrolü şu an alınamadı.');
        }
      });
    }, 350);
  }
  private qualityPreviewRequest() {
    const selectedQuestions = this.selectedReadingText()?.questions ?? [];
    const questions = selectedQuestions
      .filter(question => question.id !== this.questionEditingId)
      .map(question => ({ questionText: question.questionText, type: question.type, bloomLevel: question.bloomLevel, difficultyLevel: question.difficultyLevel, explanation: question.explanation, optionA: question.optionA, optionB: question.optionB, optionC: question.optionC, optionD: question.optionD, correctAnswer: question.correctAnswer, orderIndex: question.orderIndex }));
    if (this.questionEditing()) {
      questions.push({ questionText: this.questionDraft.questionText, type: this.questionDraft.type, bloomLevel: this.questionDraft.bloomLevel, difficultyLevel: this.questionDraft.difficultyLevel, explanation: this.questionDraft.explanation ?? null, optionA: this.questionDraft.optionA, optionB: this.questionDraft.optionB, optionC: this.questionDraft.optionC, optionD: this.questionDraft.optionD, correctAnswer: this.questionDraft.correctAnswer, orderIndex: this.questionDraft.orderIndex });
    }
    return { content: this.readingTextContent, language: this.readingTextDraft.language, questions };
  }
  changeContentPage(delta: number): void { this.contentPage = Math.max(1, this.contentPage + delta); if (this.contentTab() === 'exercise-types') this.loadExerciseTypes(); else if (this.contentTab() === 'exercises') this.loadExercises(); }

  loadPrograms(): void { this.service.getProgramTemplates().subscribe({ next: value => this.programs.set(value), error: () => this.error.set('Programlar yüklenemedi.') }); this.service.getAllExerciseTypes().subscribe({ next: value => this.availableExerciseTypes.set(value), error: () => this.availableExerciseTypes.set([]) }); this.service.getAllExercises().subscribe({ next: value => this.availableExercises.set(value), error: () => this.availableExercises.set([]) }); }
  startProgramCreate(): void { if (this.programTab() === 'programs') { this.programEditingId = null; this.programDraft = this.emptyProgram(); this.programPlan = []; this.programPatternExtras = {}; this.nextPlanItemId = 1; this.adaptivePolicyDraft = this.emptyAdaptivePolicy(); this.programEditing.set(true); } else { this.learningPathEditingId = null; this.learningPathDraft = this.emptyLearningPath(); this.learningPathEditing.set(true); } }
  editProgram(item: SpeedReadingProgramTemplate): void { this.programEditingId = item.id; this.programDraft = { name: item.name, description: item.description, targetAgeGroupConfigurationId: item.targetAgeGroupConfigurationId, minAssessmentScore: item.minAssessmentScore, maxAssessmentScore: item.maxAssessmentScore, weeklyPatternJson: item.weeklyPatternJson, initialDifficultyLevel: item.initialDifficultyLevel, weeksPerDifficultyIncrease: item.weeksPerDifficultyIncrease, maxDifficultyLevel: item.maxDifficultyLevel, totalWeeks: item.totalWeeks, totalDays: item.totalDays, isActive: item.isActive, displayOrder: item.displayOrder, programType: item.programType, examType: item.examType, isAssessment: item.isAssessment }; this.adaptivePolicyDraft = this.readAdaptivePolicy(item.weeklyPatternJson); this.readProgramPlan(item.weeklyPatternJson, item.isAssessment); this.programEditing.set(true); }
  addProgramWeek(): void { const weekNumber = Math.max(0, ...this.programPlan.map(item => item.weekNumber)) + 1; const week: ProgramPlanWeekDraft = { id: this.nextPlanId(), weekNumber, days: [] }; this.programPlan = [...this.programPlan, week]; this.addProgramDay(week); }
  removeProgramWeek(week: ProgramPlanWeekDraft): void { this.programPlan = this.programPlan.filter(item => item.id !== week.id); }
  addProgramDay(week: ProgramPlanWeekDraft): void { const dayNumber = Math.max(0, ...week.days.map(item => item.dayNumber)) + 1; week.days = [...week.days, { id: this.nextPlanId(), dayNumber, entries: [] }]; this.programPlan = [...this.programPlan]; }
  removeProgramDay(week: ProgramPlanWeekDraft, day: ProgramPlanDayDraft): void { week.days = week.days.filter(item => item.id !== day.id); this.programPlan = [...this.programPlan]; }
  addProgramPlanEntry(day: ProgramPlanDayDraft): void { day.entries = [...day.entries, { id: this.nextPlanId(), exerciseTypeId: '', exerciseTypeName: '', exerciseId: '', count: 1, difficulty: null }]; this.programPlan = [...this.programPlan]; }
  removeProgramPlanEntry(day: ProgramPlanDayDraft, entry: ProgramPlanEntryDraft): void { day.entries = day.entries.filter(item => item.id !== entry.id); this.programPlan = [...this.programPlan]; }
  saveProgram(): void { const weeklyPatternJson = this.buildProgramPattern(); if (!weeklyPatternJson) return; const request = { ...this.programDraft, weeklyPatternJson }; const action = this.programEditingId ? this.service.updateProgramTemplate(this.programEditingId, request) : this.service.createProgramTemplate(request); this.run(action, () => { this.programEditing.set(false); this.loadPrograms(); }, 'Program kaydedilemedi.'); }
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
  startAchievementCreate(): void { this.achievementEditingId = null; this.achievementDraft = this.emptyAchievement(); this.loadAchievementCriteriaTarget(); this.achievementEditing.set(true); }
  editAchievement(item: SpeedReadingAchievement): void { this.achievementEditingId = item.id; this.achievementDraft = { name: item.name, description: item.description, category: item.category, tier: item.tier, iconUrl: item.iconUrl, iconEmoji: item.iconEmoji, criteriaType: item.criteriaType, criteriaValue: item.criteriaValue, triggerType: item.triggerType, triggerValue: item.triggerValue, isRepeatable: false, xpReward: item.xpReward, isActive: item.isActive, sortOrder: item.sortOrder }; this.loadAchievementCriteriaTarget(); this.achievementEditing.set(true); }
  saveAchievement(): void { const criteriaValue = this.buildAchievementCriteriaValue(); if (!criteriaValue) return; this.achievementDraft = { ...this.achievementDraft, criteriaValue }; const action = this.achievementEditingId ? this.service.updateAchievement(this.achievementEditingId, this.achievementDraft) : this.service.createAchievement(this.achievementDraft); this.run(action, () => { this.achievementEditing.set(false); this.loadAchievements(); }, 'Başarı kaydedilemedi.'); }
  async deleteAchievement(item: SpeedReadingAchievement): Promise<void> { if (!await this.toaster.confirm('Bu başarı silinsin mi?', { title: 'Başarıyı sil' })) return; this.run(this.service.deleteAchievement(item.id), () => this.loadAchievements(), 'Başarı silinemedi.'); }
  changeAchievementPage(delta: number): void { this.achievementPage = Math.max(1, this.achievementPage + delta); this.loadAchievements(); }
  totalPages(page: { totalCount: number; pageSize: number }): number { return Math.max(1, Math.ceil(page.totalCount / page.pageSize)); }
  cancelContentEdit(): void { this.typeEditing.set(false); this.exerciseEditing.set(false); this.readingTextEditing.set(false); this.questionEditing.set(false); }
  cancelProgramEdit(): void { this.programEditing.set(false); this.learningPathEditing.set(false); this.nodeEditing.set(false); this.nodeContentEditing.set(false); this.activeNode.set(null); }
  private run(request: Observable<unknown>, success: () => void, message: string): void { this.saving.set(true); this.error.set(''); request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: success, error: () => this.error.set(message) }); }
  private download(blob: Blob, fileName: string): void { const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = fileName; anchor.click(); URL.revokeObjectURL(url); }
  private emptyType(): SpeedReadingExerciseTypeRequest { return { name: '', displayName: '', description: '', iconName: 'category', colorCode: '#2563eb', sortOrder: 0, isActive: true, engineType: '', categoryId: null }; }
  private emptyExercise(): SpeedReadingExerciseRequest { return { title: '', description: '', difficultyLevel: 1, exerciseTypeId: '', configurationJson: '{}', targetAgeGroupConfigurationId: null, isActive: true }; }
  private emptyExerciseSettings(): ExerciseSettingsDraft { return { mode: '', targetWpm: null, timeLimitSeconds: null, gridSize: null, itemCount: null, chunkSize: null, vocabularyCategory: '', vocabularyDifficulty: null, vocabularyCount: 10, transferReadingTextId: '', increaseThreshold: 85, maintainThreshold: 75, supportThreshold: 65, minimumComprehension: 75, increasePercent: 8, decreasePercent: 5, supportDecreasePercent: 10, repeatPurposeOne: 'Ana fikri belirleyin.', repeatPurposeTwo: 'Neden-sonuç ilişkilerine ve önemli ayrıntılara odaklanın.' }; }
  private readExerciseConfiguration(value: string): void {
    const root = this.parseObject(value);
    if (!root) {
      this.exerciseConfigExtras = {};
      this.exerciseEngineConfigExtras = {};
      this.exerciseTimingExtras = {};
      this.exerciseGridExtras = {};
      this.exerciseContentExtras = {};
      this.exerciseVocabularyExtras = {};
      this.exerciseSettings = this.emptyExerciseSettings();
      this.error.set('Eski egzersiz yapılandırması okunamadı. Formdaki ayarları gözden geçirip yeniden kaydedin.');
      return;
    }
    const engine = this.objectValue(root['engineConfig']);
    const timing = this.objectValue(engine['timing']);
    const grid = this.objectValue(engine['grid']);
    const content = this.objectValue(engine['content']);
    const vocabulary = this.objectValue(engine['vocabulary']);
    const purposes = this.stringArray(engine['repeatPurposes']);
    this.exerciseConfigExtras = this.withoutKeys(root, ['engineType', 'engineConfig', 'gridSize', 'timeLimitSeconds', 'itemCount', 'totalSteps']);
    this.exerciseEngineConfigExtras = this.withoutKeys(engine, ['engineType', 'mode', 'targetWpm', 'chunkSize', 'timing', 'grid', 'content', 'vocabulary', 'transferReadingTextId', 'increaseThreshold', 'maintainThreshold', 'supportThreshold', 'minimumComprehension', 'increasePercent', 'decreasePercent', 'supportDecreasePercent', 'repeatPurposes']);
    this.exerciseTimingExtras = this.withoutKeys(timing, ['durationMs']);
    this.exerciseGridExtras = this.withoutKeys(grid, ['rows', 'columns']);
    this.exerciseContentExtras = this.withoutKeys(content, ['count']);
    this.exerciseVocabularyExtras = this.withoutKeys(vocabulary, ['category', 'difficultyLevel', 'count']);
    const defaults = this.emptyExerciseSettings();
    this.exerciseSettings = {
      ...defaults,
      mode: this.stringValue(engine['mode']),
      targetWpm: this.numberValue(engine['targetWpm']),
      timeLimitSeconds: this.numberValue(root['timeLimitSeconds']) ?? this.secondsFromMilliseconds(timing['durationMs']),
      gridSize: this.numberValue(root['gridSize']) ?? this.numberValue(grid['rows']),
      itemCount: this.numberValue(root['itemCount']) ?? this.numberValue(root['totalSteps']) ?? this.numberValue(content['count']),
      chunkSize: this.numberValue(engine['chunkSize']),
      vocabularyCategory: this.stringValue(vocabulary['category']),
      vocabularyDifficulty: this.numberValue(vocabulary['difficultyLevel']),
      vocabularyCount: this.numberValue(vocabulary['count']) ?? defaults.vocabularyCount,
      transferReadingTextId: this.stringValue(engine['transferReadingTextId']),
      increaseThreshold: this.numberValue(engine['increaseThreshold']) ?? defaults.increaseThreshold,
      maintainThreshold: this.numberValue(engine['maintainThreshold']) ?? defaults.maintainThreshold,
      supportThreshold: this.numberValue(engine['supportThreshold']) ?? defaults.supportThreshold,
      minimumComprehension: this.numberValue(engine['minimumComprehension']) ?? defaults.minimumComprehension,
      increasePercent: this.numberValue(engine['increasePercent']) ?? defaults.increasePercent,
      decreasePercent: this.numberValue(engine['decreasePercent']) ?? defaults.decreasePercent,
      supportDecreasePercent: this.numberValue(engine['supportDecreasePercent']) ?? defaults.supportDecreasePercent,
      repeatPurposeOne: purposes[0] ?? defaults.repeatPurposeOne,
      repeatPurposeTwo: purposes[1] ?? defaults.repeatPurposeTwo
    };
  }
  private buildExerciseConfiguration(): string | null {
    const engineType = this.selectedExerciseEngine();
    if (!engineType) { this.error.set('Egzersiz türü seçin.'); return null; }
    if (engineType === 'adaptive_fluency') {
      if (!this.exerciseSettings.transferReadingTextId) { this.error.set('Adaptif akıcılık için transfer metni seçin.'); return null; }
      if (this.exerciseSettings.increaseThreshold < this.exerciseSettings.maintainThreshold || this.exerciseSettings.maintainThreshold < this.exerciseSettings.supportThreshold) { this.error.set('Adaptif akıcılık eşikleri ilerleme, pekiştirme ve destek sırasıyla azalmış olmalıdır.'); return null; }
    }
    const settings = this.exerciseSettings;
    const engineConfig: Record<string, unknown> = { ...this.exerciseEngineConfigExtras, engineType };
    const root: Record<string, unknown> = { ...this.exerciseConfigExtras, engineType, engineConfig };
    if (settings.mode) engineConfig['mode'] = settings.mode;
    if (this.positiveNumber(settings.targetWpm)) engineConfig['targetWpm'] = settings.targetWpm;
    if (this.positiveNumber(settings.timeLimitSeconds)) { root['timeLimitSeconds'] = settings.timeLimitSeconds; engineConfig['timing'] = { ...this.exerciseTimingExtras, durationMs: settings.timeLimitSeconds * 1000 }; } else if (Object.keys(this.exerciseTimingExtras).length) engineConfig['timing'] = this.exerciseTimingExtras;
    if (this.positiveNumber(settings.itemCount)) { root['itemCount'] = settings.itemCount; root['totalSteps'] = settings.itemCount; engineConfig['content'] = { ...this.exerciseContentExtras, count: settings.itemCount }; } else if (Object.keys(this.exerciseContentExtras).length) engineConfig['content'] = this.exerciseContentExtras;
    if (engineType === 'grid_interaction' && this.positiveNumber(settings.gridSize)) { root['gridSize'] = settings.gridSize; engineConfig['grid'] = { ...this.exerciseGridExtras, rows: settings.gridSize, columns: settings.gridSize }; } else if (engineType === 'grid_interaction' && Object.keys(this.exerciseGridExtras).length) engineConfig['grid'] = this.exerciseGridExtras;
    if (this.positiveNumber(settings.chunkSize)) engineConfig['chunkSize'] = settings.chunkSize;
    if (engineType === 'vocabulary_builder') engineConfig['vocabulary'] = { ...this.exerciseVocabularyExtras, category: settings.vocabularyCategory.trim() || undefined, difficultyLevel: this.positiveNumber(settings.vocabularyDifficulty) ? settings.vocabularyDifficulty : undefined, count: this.positiveNumber(settings.vocabularyCount) ? settings.vocabularyCount : 10 };
    if (engineType === 'adaptive_fluency') {
      engineConfig['transferReadingTextId'] = settings.transferReadingTextId;
      engineConfig['increaseThreshold'] = settings.increaseThreshold;
      engineConfig['maintainThreshold'] = settings.maintainThreshold;
      engineConfig['supportThreshold'] = settings.supportThreshold;
      engineConfig['minimumComprehension'] = settings.minimumComprehension;
      engineConfig['increasePercent'] = settings.increasePercent;
      engineConfig['decreasePercent'] = settings.decreasePercent;
      engineConfig['supportDecreasePercent'] = settings.supportDecreasePercent;
      engineConfig['repeatPurposes'] = [settings.repeatPurposeOne, settings.repeatPurposeTwo].map(item => item.trim()).filter(Boolean);
    }
    return JSON.stringify(root);
  }
  private parseObject(value: string): Record<string, unknown> | null { try { return this.objectValue(JSON.parse(value)); } catch { return null; } }
  private objectValue(value: unknown): Record<string, unknown> { return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {}; }
  private withoutKeys(value: Record<string, unknown>, keys: string[]): Record<string, unknown> { return Object.fromEntries(Object.entries(value).filter(([key]) => !keys.includes(key))); }
  private numberValue(value: unknown): number | null { return typeof value === 'number' && Number.isFinite(value) ? value : null; }
  private stringValue(value: unknown): string { return typeof value === 'string' ? value : ''; }
  private stringArray(value: unknown): string[] { return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : []; }
  private secondsFromMilliseconds(value: unknown): number | null { const milliseconds = this.numberValue(value); return milliseconds && milliseconds > 0 ? Math.round(milliseconds / 1000) : null; }
  private positiveNumber(value: number | null): value is number { return value !== null && Number.isFinite(value) && value > 0; }
  private emptyReadingText(): SpeedReadingReadingTextRequest { return { title: '', content: '', wordCount: 0, category: '', difficultyLevel: 1, targetAgeGroupConfigurationId: null, language: 'tr', isActive: false, tags: '', recommendedMinLevel: 0, recommendedMaxLevel: 10, exerciseId: null }; }
  private emptyQuestion(readingTextId: string): SpeedReadingReadingQuestionRequest { return { readingTextId, questionText: '', type: 1, bloomLevel: 1, difficultyLevel: 1, explanation: '', optionA: '', optionB: '', optionC: '', optionD: '', correctAnswer: 'A', orderIndex: 0 }; }
  private emptyProgram(): SpeedReadingProgramTemplateRequest { return { name: '', description: '', targetAgeGroupConfigurationId: '', minAssessmentScore: 0, maxAssessmentScore: 100, weeklyPatternJson: '{}', initialDifficultyLevel: 1, weeksPerDifficultyIncrease: 1, maxDifficultyLevel: 5, totalWeeks: 4, totalDays: 28, isActive: false, displayOrder: 0, programType: 0, examType: null, isAssessment: false }; }
  private emptyAdaptivePolicy(): AdaptivePolicyDraft { return { minimumMeasuredSessions: 3, advanceComprehensionThreshold: 80, maintainComprehensionThreshold: 65, minimumWpmTrendPercent: 0, supportTrendPercent: -10 }; }
  private readAdaptivePolicy(value: string): AdaptivePolicyDraft { try { const parsed = JSON.parse(value) as { adaptation?: Partial<AdaptivePolicyDraft> }; return { ...this.emptyAdaptivePolicy(), ...parsed.adaptation }; } catch { return this.emptyAdaptivePolicy(); } }
  private nextPlanId(): number { return this.nextPlanItemId++; }
  private readProgramPlan(value: string, isAssessment: boolean): void {
    const root = this.parseObject(value);
    this.nextPlanItemId = 1;
    if (!root) {
      this.programPatternExtras = {};
      this.programPlan = [];
      this.error.set('Eski haftalık plan okunamadı. Planı form üzerinden yeniden oluşturup kaydedin.');
      return;
    }
    this.programPatternExtras = Object.fromEntries(Object.entries(root).filter(([key]) => !/^week\d+$/i.test(key) && key !== 'adaptation'));
    this.programPlan = Object.entries(root)
      .map(([key, value]) => ({ weekNumber: Number(/^week(\d+)$/i.exec(key)?.[1]), value }))
      .filter(item => Number.isInteger(item.weekNumber) && item.weekNumber > 0)
      .sort((left, right) => left.weekNumber - right.weekNumber)
      .map(item => ({ id: this.nextPlanId(), weekNumber: item.weekNumber, days: this.readProgramDays(item.value, isAssessment) }));
  }
  private readProgramDays(value: unknown, isAssessment: boolean): ProgramPlanDayDraft[] {
    const candidateDays = Array.isArray(value) ? [['day1', value] as const] : Object.entries(this.objectValue(value));
    return candidateDays
      .map(([key, entries]) => ({ dayNumber: Number(/^day(\d+)$/i.exec(key)?.[1]), entries }))
      .filter(item => Number.isInteger(item.dayNumber) && item.dayNumber > 0 && Array.isArray(item.entries))
      .sort((left, right) => left.dayNumber - right.dayNumber)
      .map(item => ({ id: this.nextPlanId(), dayNumber: item.dayNumber, entries: (item.entries as unknown[]).map(entry => this.readProgramEntry(entry, isAssessment)).filter((entry): entry is ProgramPlanEntryDraft => entry !== null) }));
  }
  private readProgramEntry(value: unknown, isAssessment: boolean): ProgramPlanEntryDraft | null {
    const entry = this.objectValue(value);
    const exerciseId = this.stringValue(entry['exerciseId']);
    const exerciseTypeName = this.stringValue(entry['type']);
    if (isAssessment ? !exerciseId : !exerciseTypeName) return null;
    const exerciseTypeId = this.availableExerciseTypes().find(item => item.name === exerciseTypeName)?.id ?? '';
    return { id: this.nextPlanId(), exerciseTypeId, exerciseTypeName, exerciseId, count: this.numberValue(entry['count']) ?? 1, difficulty: this.numberValue(entry['difficulty']) };
  }
  private buildProgramPattern(): string | null {
    const plan: Record<string, unknown> = { ...this.programPatternExtras, adaptation: this.adaptivePolicyDraft };
    const activeDays = this.programPlan.flatMap(week => week.days.map(day => ({ week, day }))).filter(item => item.day.entries.length > 0);
    if (this.programDraft.isActive && activeDays.length === 0) { this.error.set('Aktif program için en az bir gün ve çalışma ekleyin.'); return null; }
    for (const { week, day } of activeDays) {
      const entries: Record<string, unknown>[] = [];
      for (const entry of day.entries) {
        if (this.programDraft.isAssessment) {
          if (!entry.exerciseId) { this.error.set(`${week.weekNumber}. hafta ${day.dayNumber}. gün için ölçüm egzersizi seçin.`); return null; }
          entries.push({ exerciseId: entry.exerciseId });
          continue;
        }
        const typeName = this.availableExerciseTypes().find(item => item.id === entry.exerciseTypeId)?.name ?? entry.exerciseTypeName;
        if (!typeName) { this.error.set(`${week.weekNumber}. hafta ${day.dayNumber}. gün için egzersiz türü seçin.`); return null; }
        if (!Number.isInteger(entry.count) || entry.count < 1 || entry.count > 20) { this.error.set(`${week.weekNumber}. hafta ${day.dayNumber}. gün için tekrar sayısı 1 ile 20 arasında olmalıdır.`); return null; }
        if (entry.difficulty !== null && (!Number.isInteger(entry.difficulty) || entry.difficulty < 0 || entry.difficulty > 10)) { this.error.set(`${week.weekNumber}. hafta ${day.dayNumber}. gün için zorluk 0 ile 10 arasında olmalıdır.`); return null; }
        entries.push({ type: typeName, count: entry.count, ...(entry.difficulty === null ? {} : { difficulty: entry.difficulty }) });
      }
      const currentWeek = this.objectValue(plan[`week${week.weekNumber}`]);
      plan[`week${week.weekNumber}`] = { ...currentWeek, [`day${day.dayNumber}`]: entries };
    }
    return JSON.stringify(plan);
  }
  private normalizeEngineType(value: string): string { const aliases: Record<string, string> = { schultetable: 'grid_interaction', eyetracking: 'motion_path', eye_tracking: 'motion_path', fixation: 'motion_path', saccade: 'motion_path', tachistoscope: 'text_stream', rsvp: 'text_stream', textfading: 'text_fade', text_fading: 'text_fade', speedreading: 'word_highlight', speed_reading: 'word_highlight', chunking: 'word_highlight', word_group: 'word_highlight', visualexpansion: 'visual_expansion', comprehension: 'reading_comprehension', examsimulation: 'exam_simulation', freereading: 'free_reading', regressionreduction: 'regression_reduction', subvocalizationreduction: 'subvocalization_reduction', attentiontraining: 'attention_training', vocabulary: 'vocabulary_builder', vocabularybuilder: 'vocabulary_builder', erroranalysis: 'error_analysis', adaptivefluency: 'adaptive_fluency' }; const normalized = value.trim().replace(/-/g, '_').replace(/ /g, '_').toLowerCase(); return aliases[normalized] ?? normalized; }
  private emptyLearningPath(): SpeedReadingLearningPathTemplateRequest { return { name: '', targetAgeGroupConfigurationId: null, description: '', estimatedDays: 1, isActive: true }; }
  private emptyNode(templateId: string): SpeedReadingLearningPathNodeRequest { return { templateId, parentNodeId: null, nodeType: 'Exercise', title: '', contentType: null, contentId: null, order: 0 }; }
  private emptyNodeContent(nodeId: string): SpeedReadingLearningPathNodeContentRequest { return { nodeId, exerciseId: null, readingTextId: null, description: '' }; }
  resetAchievementCriteriaTarget(): void { this.achievementCriteriaTarget = this.selectedAchievementCriterion()?.input === 'text' ? '' : '1'; }
  achievementCriteriaValueLabel(): string { const criterion = this.selectedAchievementCriterion(); return criterion?.input === 'text' ? 'Egzersiz türü adı' : 'Hedef değer'; }
  achievementCriteriaInputType(): 'number' | 'text' { return this.selectedAchievementCriterion()?.input ?? 'number'; }
  achievementCriteriaMin(): number | null { return this.selectedAchievementCriterion()?.min ?? null; }
  achievementCriteriaMax(): number | null { return this.selectedAchievementCriterion()?.max ?? null; }
  achievementCriteriaHelp(): string { return this.selectedAchievementCriterion()?.help ?? 'Önce geçerli bir kriter türü seçin.'; }
  private selectedAchievementCriterion(): AchievementCriteriaDefinition | undefined { return this.achievementCriteria.find(item => item.value === this.achievementDraft.criteriaType); }
  private loadAchievementCriteriaTarget(): void { const criterion = this.selectedAchievementCriterion(); if (!criterion) { this.achievementDraft = { ...this.achievementDraft, criteriaType: 'activity_count' }; this.achievementCriteriaTarget = '1'; return; } try { const value = JSON.parse(this.achievementDraft.criteriaValue)?.[criterion.property]; this.achievementCriteriaTarget = value === undefined || value === null ? (criterion.input === 'text' ? '' : '1') : String(value); } catch { this.achievementCriteriaTarget = criterion.input === 'text' ? '' : '1'; } }
  private buildAchievementCriteriaValue(): string | null { const criterion = this.selectedAchievementCriterion(); const value = this.achievementCriteriaTarget.trim(); if (!criterion || !value) { this.error.set('Başarı kriteri için geçerli bir hedef girin.'); return null; } if (criterion.input === 'text') return JSON.stringify({ [criterion.property]: value }); const numericValue = Number(value); if (!Number.isFinite(numericValue) || (!criterion.allowDecimal && !Number.isInteger(numericValue)) || numericValue < (criterion.min ?? 0) || numericValue > (criterion.max ?? Number.MAX_SAFE_INTEGER)) { this.error.set('Başarı kriteri hedefi geçerli aralıkta olmalıdır.'); return null; } return JSON.stringify({ [criterion.property]: numericValue }); }
  private emptyAchievement(): SpeedReadingAchievementRequest { return { name: '', description: '', category: 'Reading', tier: 'Bronze', iconUrl: null, iconEmoji: '🏅', criteriaType: 'activity_count', criteriaValue: '{"count":1}', triggerType: null, triggerValue: null, isRepeatable: false, xpReward: 10, isActive: true, sortOrder: 0 }; }
}
