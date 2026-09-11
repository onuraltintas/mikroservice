import { CommonModule } from '@angular/common';
import { A11yModule } from '@angular/cdk/a11y';
import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import {
  SpeedReadingAdminService,
  SpeedReadingAgeGroup,
  SpeedReadingAgeGroupRequest,
  SpeedReadingAssessmentExerciseInput,
  SpeedReadingAssessmentTemplate,
  SpeedReadingCalibrationReport,
  SpeedReadingExercise,
  SpeedReadingLevelCatalog,
  SpeedReadingLevelDefinition,
  SpeedReadingMeasurementCapability,
  SpeedReadingStudyDefinition,
  SpeedReadingStudyDefinitionRequest,
  SpeedReadingStudyEnrollment,
  SpeedReadingStudyStudentOption
} from '../../../core/services/speed-reading-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';

type ConfigurationTab = 'age-groups' | 'assessments' | 'levels';

interface AssessmentExerciseDraft extends SpeedReadingAssessmentExerciseInput {
  exerciseTitle: string;
  exerciseType: string;
  difficultyLevel: number;
}

@Component({
  selector: 'app-speed-reading-content-configuration',
  standalone: true,
  imports: [CommonModule, FormsModule, A11yModule],
  template: `
    <main class="space-y-6">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Hızlı Okuma servisi</p>
        <h1 class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">İçerik yapılandırması</h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Yaş grubu hedeflerini ve yaş grubuna göre seviye tespit egzersizlerini yönetin.</p>
      </header>

      <nav class="ui-tab-list flex flex-wrap gap-2" aria-label="İçerik yapılandırma sekmeleri">
        @for (tab of tabs; track tab.value) {
          <button type="button" (click)="selectTab(tab.value)" [attr.aria-pressed]="selectedTab() === tab.value" [class.bg-indigo-600]="selectedTab() === tab.value" [class.text-white]="selectedTab() === tab.value" class="ui-tab rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 dark:border-gray-600 dark:text-gray-200">{{ tab.label }}</button>
        }
      </nav>

      @if (error()) {
        <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div>
      }

      @if (selectedTab() === 'age-groups') {
        <section class="space-y-4" aria-labelledby="age-groups-title">
          <div class="flex items-center justify-between gap-3"><div><h2 id="age-groups-title" class="text-lg font-semibold text-gray-900 dark:text-white">Yaş grupları</h2><p class="muted">WPM, anlama ve günlük çalışma hedefleri burada tanımlanır.</p></div><button type="button" (click)="startAgeGroupCreate()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni yaş grubu</button></div>

          @if (ageEditing()) {
            <div class="dialog-backdrop" (click)="cancelAgeGroupEdit()" aria-hidden="true"></div>
            <form (ngSubmit)="saveAgeGroup()" (click)="$event.stopPropagation()" class="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="age-group-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true">
              <header class="dialog-header"><h3 id="age-group-dialog-title">{{ ageEditingId ? 'Yaş grubunu düzenle' : 'Yeni yaş grubu' }}</h3><button type="button" class="secondary" (click)="cancelAgeGroupEdit()">Kapat</button></header>
              <div class="form-grid dialog-body">
                <label>Teknik ad<input [(ngModel)]="ageDraft.name" name="ageName" required maxlength="80" /></label>
                <label>Görünen ad<input [(ngModel)]="ageDraft.displayName" name="ageDisplayName" required maxlength="150" /></label>
                <label>Min. yaş<input type="number" [(ngModel)]="ageDraft.minAge" name="ageMin" min="0" max="120" required /></label>
                <label>Max. yaş<input type="number" [(ngModel)]="ageDraft.maxAge" name="ageMax" min="0" max="120" /></label>
                <label>Min. WPM<input type="number" [(ngModel)]="ageDraft.minWpm" name="ageMinWpm" min="0" max="5000" required /></label>
                <label>Önerilen WPM<input type="number" [(ngModel)]="ageDraft.recommendedWpm" name="ageRecommendedWpm" min="0" max="5000" required /></label>
                <label>Max. WPM<input type="number" [(ngModel)]="ageDraft.maxWpm" name="ageMaxWpm" min="0" max="5000" required /></label>
                <label>Önerilen anlama (%)<input type="number" [(ngModel)]="ageDraft.recommendedComprehension" name="ageComprehension" min="0" max="100" required /></label>
                <label>Günlük dakika<input type="number" [(ngModel)]="ageDraft.recommendedDailyMinutes" name="ageMinutes" min="1" max="1440" required /></label>
                <label>Varsayılan zorluk<input type="number" [(ngModel)]="ageDraft.defaultDifficultyLevel" name="ageDifficulty" min="1" max="5" required /></label>
                <label>Sıra<input type="number" [(ngModel)]="ageDraft.orderIndex" name="ageOrder" min="0" max="10000" required /></label>
                <label class="wide">Açıklama<textarea [(ngModel)]="ageDraft.description" name="ageDescription" maxlength="1000"></textarea></label>
                <label class="check"><input type="checkbox" [(ngModel)]="ageDraft.isActive" name="ageActive" /> Aktif</label>
              </div>
              <div class="form-actions"><button type="button" (click)="cancelAgeGroupEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div>
            </form>
          }

          <div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Yaş grubu</th><th>Yaş</th><th>WPM hedefi</th><th>Anlama</th><th>Günlük süre</th><th>Durum</th><th></th></tr></thead><tbody>
            @for (ageGroup of ageGroups(); track ageGroup.id) {
              <tr><td><strong>{{ ageGroup.displayName }}</strong><div class="muted">{{ ageGroup.name }}</div></td><td>{{ ageGroup.minAge }}{{ ageGroup.maxAge === null ? '+' : '–' + ageGroup.maxAge }}</td><td>{{ ageGroup.minWpm }}–{{ ageGroup.maxWpm }} <span class="muted">({{ ageGroup.recommendedWpm }})</span></td><td>%{{ ageGroup.recommendedComprehension }}</td><td>{{ ageGroup.recommendedDailyMinutes }} dk</td><td>{{ ageGroup.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions"><button type="button" (click)="startAgeGroupEdit(ageGroup)">Düzenle</button><button type="button" (click)="deleteAgeGroup(ageGroup)">Sil</button></td></tr>
            } @empty { <tr><td colspan="7" class="empty">Yaş grubu bulunamadı.</td></tr> }
          </tbody></table></div></div>
        </section>
      }

        @if (selectedTab() === 'assessments') {
        <section class="space-y-4" aria-labelledby="assessments-title">
          <div class="flex items-center justify-between gap-3"><div><h2 id="assessments-title" class="text-lg font-semibold text-gray-900 dark:text-white">Seviye tespit şablonları</h2><p class="muted">Her yaş grubu için kullanılacak egzersizleri ve sıralamayı belirleyin.</p></div><button type="button" (click)="openAssessmentDialog()" class="primary" [disabled]="!assessmentAgeGroupId()">{{ currentTemplate() ? 'Şablonu düzenle' : 'Yeni şablon' }}</button></div>
          <div class="data-card"><label class="block max-w-xl">Yaş grubu<select [ngModel]="assessmentAgeGroupId()" name="assessmentAgeGroup" (ngModelChange)="assessmentAgeGroupId.set($event); loadAssessmentForAgeGroup()"><option value="">Seçin</option>@for (ageGroup of ageGroups(); track ageGroup.id) {<option [value]="ageGroup.id">{{ ageGroup.displayName }}</option>}</select></label></div>

          @if (assessmentEditing()) {
            <div class="dialog-backdrop" (click)="closeAssessmentDialog()" aria-hidden="true"></div>
            <form (ngSubmit)="saveAssessment()" (click)="$event.stopPropagation()" class="dialog-panel dialog-panel-wide" role="dialog" aria-modal="true" aria-labelledby="assessment-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true">
              <header class="dialog-header"><h3 id="assessment-dialog-title">{{ currentTemplate() ? 'Seviye tespit şablonunu düzenle' : 'Yeni seviye tespit şablonu' }}</h3><div class="actions">@if (currentTemplate()) {<button type="button" (click)="deleteAssessment()" class="danger">Şablonu sil</button>}<button type="button" (click)="closeAssessmentDialog()" class="secondary">Kapat</button></div></header>
              <div class="dialog-body"><label class="block">Şablon adı<input [(ngModel)]="assessmentName" name="assessmentName" required maxlength="150" /></label>
              <div class="mt-4 grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]"><div><h4 class="font-medium">Egzersiz ekle</h4><select [(ngModel)]="exerciseToAdd" name="exerciseToAdd" (ngModelChange)="addAssessmentExercise($event)"><option value="">Seçin</option>@for (exercise of exercises(); track exercise.id) {<option [value]="exercise.id">{{ exercise.title }} — Seviye {{ exercise.difficultyLevel }}</option>}</select></div><div><h4 class="font-medium">Seçilen egzersizler</h4>@if (selectedAssessmentExercises().length === 0) {<p class="empty">En az bir egzersiz ekleyin.</p>} @for (exercise of selectedAssessmentExercises(); track exercise.exerciseId; let index = $index) {<div class="exercise-row"><div class="min-w-0 flex-1"><strong>{{ index + 1 }}. {{ exercise.customTitle || exercise.exerciseTitle }}</strong><div class="muted">{{ exercise.exerciseType }} · Seviye {{ exercise.difficultyLevel }}</div><input [(ngModel)]="exercise.customTitle" [name]="'assessmentTitle' + index" placeholder="Özel başlık" maxlength="150" /><input [(ngModel)]="exercise.customDescription" [name]="'assessmentDescription' + index" placeholder="Özel açıklama" maxlength="500" /></div><div class="actions"><button type="button" (click)="moveAssessmentExercise(index, -1)" [disabled]="index === 0">↑</button><button type="button" (click)="moveAssessmentExercise(index, 1)" [disabled]="index === selectedAssessmentExercises().length - 1">↓</button><button type="button" (click)="removeAssessmentExercise(index)">Sil</button></div></div>}</div></div>
              </div><div class="form-actions"><button type="button" (click)="closeAssessmentDialog()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving() || selectedAssessmentExercises().length === 0">Kaydet</button></div>
            </form>
          }

          <div class="data-card"><h3 class="mb-3 font-medium text-gray-900 dark:text-white">Kayıtlı şablonlar</h3><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Şablon</th><th>Yaş grubu</th><th>Egzersiz</th><th>Durum</th><th>İşlemler</th></tr></thead><tbody>@for (template of assessmentTemplates(); track template.id) {<tr><td>{{ template.name }}</td><td>{{ template.ageGroupDisplayName }}</td><td>{{ template.exercises.length }}</td><td>{{ template.isActive ? 'Aktif' : 'Pasif' }}</td><td class="table-actions"><button type="button" class="action-button" (click)="selectAssessmentTemplate(template)">Düzenle</button><button type="button" class="action-button danger" (click)="deleteAssessment(template)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Şablon bulunamadı.</td></tr>}</tbody></table></div></div>
        </section>
      }

      @if (selectedTab() === 'levels') {
        <section class="space-y-4" aria-labelledby="levels-title">
          <div class="flex items-center justify-between gap-3"><div><h2 id="levels-title" class="text-lg font-semibold text-gray-900 dark:text-white">Seviye sözlüğü</h2><p class="muted">Değerlendirme eşiklerini sürümleyin. Yayınlanan sürümler geçmiş sonuçların tekrar üretilebilmesi için kilitlenir.</p></div><button type="button" (click)="startLevelCatalogCreate()" class="primary">Yeni sürüm</button></div>
          @if (levelEditing()) {
            <div class="dialog-backdrop" (click)="cancelLevelCatalogEdit()" aria-hidden="true"></div>
            <form (ngSubmit)="saveLevelCatalog()" (click)="$event.stopPropagation()" class="dialog-panel dialog-panel-wide" role="dialog" aria-modal="true" aria-labelledby="level-catalog-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true">
              <header class="dialog-header"><h3 id="level-catalog-dialog-title">{{ levelCatalogEditingId ? 'Seviye kataloğunu düzenle' : 'Yeni seviye kataloğu' }}</h3><button type="button" class="secondary" (click)="cancelLevelCatalogEdit()">Kapat</button></header>
              <div class="dialog-body"><div class="form-grid"><label>Sürüm kodu<input [(ngModel)]="levelCatalogVersion" name="levelCatalogVersion" required maxlength="100" [disabled]="!!levelCatalogEditingId" /></label><label>Katalog adı<input [(ngModel)]="levelCatalogName" name="levelCatalogName" required maxlength="200" /></label></div>
              <div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Seviye</th><th>Kod</th><th>Ad</th><th>Minimum WPM</th><th>Minimum anlama</th><th></th></tr></thead><tbody>
                @for (level of levelDraft; track $index; let index = $index) {<tr><td>{{ index + 1 }}</td><td><input [(ngModel)]="level.code" [name]="'levelCode' + index" required maxlength="50" /></td><td><input [(ngModel)]="level.displayName" [name]="'levelName' + index" required maxlength="100" /></td><td><input type="number" [(ngModel)]="level.minimumWpm" [name]="'levelWpm' + index" min="0" max="2000" required /></td><td><input type="number" [(ngModel)]="level.minimumComprehension" [name]="'levelComprehension' + index" min="0" max="100" required /></td><td><button type="button" (click)="removeLevel(index)" [disabled]="levelDraft.length <= 2" class="danger">Sil</button></td></tr>}
              </tbody></table></div>
              </div>
              <div class="form-actions"><button type="button" (click)="addLevel()" class="secondary" [disabled]="levelDraft.length >= 20">Seviye ekle</button><button type="button" (click)="cancelLevelCatalogEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Taslağı kaydet</button></div>
            </form>
          }
          <div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Sürüm</th><th>Ad</th><th>Durum</th><th>Seviye</th><th>Yayın tarihi</th><th></th></tr></thead><tbody>
            @for (catalog of levelCatalogs(); track catalog.version) {<tr><td><strong>{{ catalog.version }}</strong></td><td>{{ catalog.name }}</td><td>{{ levelCatalogStatus(catalog.status) }}</td><td>{{ catalog.definitions.length }}</td><td>{{ catalog.publishedAt ? (catalog.publishedAt | date:'short') : '—' }}</td><td class="actions">@if (catalog.status === 1 && catalog.id) {<button type="button" (click)="startLevelCatalogEdit(catalog)">Düzenle</button><button type="button" (click)="publishLevelCatalog(catalog)">Yayınla</button>}</td></tr>}
          </tbody></table></div></div>
          <div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Seviye</th><th>Kod</th><th>Ad</th><th>Minimum WPM</th><th>Minimum anlama</th></tr></thead><tbody>
            @for (level of levels(); track level.level) {
              <tr><td>{{ level.level }}</td><td>{{ level.code }}</td><td>{{ level.displayName }}</td><td>{{ level.minimumWpm }}</td><td>%{{ level.minimumComprehension }}</td></tr>
            } @empty { <tr><td colspan="5" class="empty">Seviye sözlüğü yüklenemedi.</td></tr> }
          </tbody></table></div></div>
          <div class="data-card"><h3 class="mb-3 font-medium text-gray-900 dark:text-white">Motor ölçüm yetenekleri</h3><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Motor</th><th>Ölçüm modu</th><th>Placement</th><th>Kanıt</th></tr></thead><tbody>
            @for (capability of measurementCapabilities(); track capability.code) {
              <tr><td><strong>{{ capability.displayName }}</strong><div class="muted">{{ capability.code }}</div></td><td>{{ capability.measurementMode }}</td><td>{{ capability.isAssessmentEligible ? 'Uygun' : 'NotMeasured' }}</td><td>{{ capability.evidence }}</td></tr>
            } @empty { <tr><td colspan="4" class="empty">Ölçüm yetenekleri yüklenemedi.</td></tr> }
          </tbody></table></div></div>
          <div class="data-card"><h3 class="mb-1 font-medium text-gray-900 dark:text-white">Kalibrasyon ve normlama kanıtı</h3><p class="muted mb-3">Kimlik içermeyen faz, katalog ve yaş grubu segmentleri. En az {{ calibration()?.minimumPublishableSampleSize || 30 }} tamamlanmış ölçüm olmadan eşik yayına hazır sayılmaz.</p>
            @if (calibration()?.dataAvailable === false) {<p class="empty">{{ calibration()?.unavailableReason }}</p>}
            <div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Faz</th><th>Katalog</th><th>Yaş grubu</th><th>N</th><th>WPM ort./medyan</th><th>SS</th><th>Anlama ort./medyan</th><th>Kanıt</th></tr></thead><tbody>
              @for (segment of calibration()?.segments || []; track segment.phase + segment.levelCatalogVersion + segment.ageGroup + segment.cohortCode) {<tr><td>{{ assessmentPhaseName(segment.phase) }}</td><td>{{ segment.levelCatalogVersion }}<div class="muted">{{ segment.studyCode || 'Rutin' }}{{ segment.cohortCode ? ' · ' + segment.cohortCode : '' }}</div></td><td>{{ segment.ageGroup }}</td><td>{{ segment.sampleSize }}</td><td>{{ segment.meanWpm }} / {{ segment.medianWpm }}</td><td>{{ segment.standardDeviationWpm }}</td><td>%{{ segment.meanComprehension }} / %{{ segment.medianComprehension }}</td><td>{{ segment.isPublishable ? 'Yayına hazır örneklem' : 'Pilot kanıtı' }}</td></tr>}
              @empty {<tr><td colspan="8" class="empty">Henüz tamamlanmış, kalibrasyona uygun değerlendirme yok.</td></tr>}
            </tbody></table></div>
          </div>
          <div class="data-card space-y-3"><div class="flex items-center justify-between gap-3"><div><h3 class="font-medium text-gray-900 dark:text-white">Araştırma çalışmaları</h3><p class="muted">Çalışma kodu, protokol, kohort ve onam belgesi burada bir kez tanımlanır.</p></div><button type="button" (click)="openStudyDefinitionDialog()" class="primary">Yeni çalışma</button></div>
            @if (studyDefinitionEditing()) {<div class="dialog-backdrop" (click)="closeStudyDefinitionDialog()" aria-hidden="true"></div><form (ngSubmit)="saveStudyDefinition()" (click)="$event.stopPropagation()" class="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="study-definition-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true"><header class="dialog-header"><h3 id="study-definition-dialog-title">{{ studyDefinitionEditingId ? 'Çalışmayı düzenle' : 'Yeni çalışma' }}</h3><button type="button" (click)="closeStudyDefinitionDialog()" class="secondary">Kapat</button></header><div class="form-grid dialog-body"><label>Çalışma kodu<input [(ngModel)]="studyDefinitionDraft.studyCode" name="studyDefinitionCode" required maxlength="100" [disabled]="!!studyDefinitionEditingId" /></label><label>Çalışma adı<input [(ngModel)]="studyDefinitionDraft.name" name="studyDefinitionName" required maxlength="200" /></label><label>Protokol sürümü<input [(ngModel)]="studyDefinitionDraft.protocolVersion" name="studyDefinitionProtocol" required maxlength="100" /></label><label>Kohort<input [(ngModel)]="studyDefinitionDraft.cohortCode" name="studyDefinitionCohort" required maxlength="100" /></label><label>Onam metni sürümü<input [(ngModel)]="studyDefinitionDraft.consentDocumentVersion" name="studyDefinitionConsentVersion" required maxlength="100" /></label><label class="wide">Onam belge kaydı<input [(ngModel)]="studyDefinitionDraft.consentDocumentReference" name="studyDefinitionConsentReference" required maxlength="500" placeholder="Belge numarası veya güvenli kayıt yolu" /></label></div><div class="form-actions"><button type="button" (click)="closeStudyDefinitionDialog()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div></form>}
            <div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Çalışma</th><th>Protokol</th><th>Kohort</th><th>Onam</th><th>Durum</th><th></th></tr></thead><tbody>@for (study of studyDefinitions(); track study.id) {<tr><td><strong>{{ study.studyCode }}</strong><div class="muted">{{ study.name }}</div></td><td>{{ study.protocolVersion }}</td><td>{{ study.cohortCode }}</td><td>{{ study.consentDocumentVersion }}<div class="muted">{{ study.consentDocumentReference }}</div></td><td>{{ study.isActive ? 'Aktif' : 'Emekli' }}</td><td class="actions"><button type="button" (click)="editStudyDefinition(study)">Düzenle</button>@if (study.isActive) {<button type="button" class="danger" (click)="retireStudyDefinition(study)">Emekliye ayır</button>}</td></tr>} @empty {<tr><td colspan="6" class="empty">Araştırma çalışması tanımlanmadı.</td></tr>}</tbody></table></div>
          </div>
          <div class="data-card space-y-3"><div class="flex items-center justify-between gap-3"><div><h3 class="font-medium text-gray-900 dark:text-white">Pilot/RCT katılımcı atamaları</h3><p class="muted">Onamı kaydedilmiş öğrenciyi protokol ve kohorta bağlar. Öğrenci istemcisi bu alanları değiştiremez.</p></div><button type="button" (click)="openStudyDialog()" class="primary">Katılımcı ekle</button></div>
            @if (studyEditing()) {<div class="dialog-backdrop" (click)="closeStudyDialog()" aria-hidden="true"></div><form (ngSubmit)="saveStudyEnrollment()" (click)="$event.stopPropagation()" class="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="study-dialog-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true"><header class="dialog-header"><h3 id="study-dialog-title">Katılımcı ekle</h3><button type="button" (click)="closeStudyDialog()" class="secondary">Kapat</button></header><div class="form-grid dialog-body"><div class="student-picker"><label>Öğrenci ara<input [(ngModel)]="studyStudentSearch" (ngModelChange)="searchStudyStudents($event)" name="studyStudentSearch" required placeholder="Ad, soyad veya e-posta yazın" autocomplete="off" /></label><input type="hidden" [(ngModel)]="studyDraft.studentId" name="studyStudent" required />@if (studyStudentSearch.trim().length > 0 && studyStudentSearch.trim().length < 2) {<p class="muted">En az iki karakter yazın.</p>} @if (studyStudents().length > 0) {<div class="student-results" role="listbox" aria-label="Öğrenci sonuçları">@for (student of studyStudents(); track student.studentId) {<button type="button" role="option" (click)="selectStudyStudent(student)"><strong>{{ student.displayName }}</strong><span>{{ student.email || student.studentId }}</span></button>}</div>}</div><label>Çalışma<select [(ngModel)]="studyDraft.studyDefinitionId" name="studyDefinition" required><option value="">Çalışma seçin</option>@for (study of activeStudyDefinitions(); track study.id) {<option [value]="study.id">{{ study.studyCode }} · {{ study.cohortCode }} · {{ study.protocolVersion }}</option>}</select></label>@if (selectedStudyDefinition(); as study) {<p class="muted wide">Onam: {{ study.consentDocumentVersion }} · {{ study.consentDocumentReference }}</p>}<label>Onam zamanı<input type="datetime-local" [(ngModel)]="studyDraft.consentRecordedAt" name="studyConsent" required /></label></div><div class="form-actions"><button type="button" (click)="closeStudyDialog()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving() || !studyDraft.studentId || !studyDraft.studyDefinitionId">Kaydet</button></div></form>}
            <div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Çalışma</th><th>Protokol</th><th>Kohort</th><th>Öğrenci</th><th>Onam</th><th>Durum</th><th></th></tr></thead><tbody>@for (enrollment of studyEnrollments(); track enrollment.id) {<tr><td>{{ enrollment.studyCode }}</td><td>{{ enrollment.protocolVersion }}</td><td>{{ enrollment.cohortCode }}</td><td>{{ enrollment.studentName || 'Öğrenci profili' }}<div class="muted">{{ enrollment.studentEmail || enrollment.studentId }}</div></td><td>{{ enrollment.consentRecordedAt | date:'short' }}</td><td>{{ enrollment.isActive ? 'Aktif' : 'Çekildi' }}</td><td>@if (enrollment.isActive) {<button type="button" class="danger" (click)="withdrawStudyEnrollment(enrollment)">Çalışmadan çek</button>}</td></tr>} @empty {<tr><td colspan="7" class="empty">Çalışma katılımcısı bulunmuyor.</td></tr>}</tbody></table></div>
          </div>
        </section>
      }
    </main>
  `,
  styles: [`
    :host { display: block; }
    .muted { color: var(--ui-text-muted); font-size: .85rem; }
    .data-card, .form-card { border: 1px solid var(--ui-border); border-radius: .75rem; padding: 1rem; background: var(--ui-surface); }
    .form-card { display: grid; gap: 1rem; }
    .form-grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); }
    label { display: grid; gap: .35rem; font-size: .875rem; font-weight: 500; color: var(--ui-text); }
    input, textarea, select { width: 100%; border: 1px solid var(--ui-border-strong); border-radius: .5rem; padding: .55rem .7rem; background: transparent; font: inherit; color: inherit; }
    textarea { min-height: 5rem; resize: vertical; }
    .wide { grid-column: 1 / -1; }
    .check { display: flex; align-items: center; gap: .5rem; }
    .check input { width: auto; }
    .form-actions { display: flex; justify-content: flex-end; gap: .5rem; }
    .primary, .secondary, .danger { border-radius: .5rem; padding: .55rem .8rem; font-size: .875rem; font-weight: 600; }
    .primary { background: var(--ui-brand); color: var(--ui-brand-contrast); }
    .secondary { border: 1px solid var(--ui-border-strong); }
    .danger { color: var(--ui-danger); }
    .actions { display: flex; flex-wrap: wrap; gap: .4rem; }
    .actions button { color: var(--ui-brand); font-size: .8rem; }
    .table-actions { display: flex; flex-wrap: wrap; gap: .5rem; }
    .action-button { border: 1px solid var(--ui-border-strong); border-radius: .5rem; padding: .4rem .65rem; background: var(--ui-surface); color: var(--ui-brand); font-size: .8rem; font-weight: 600; }
    .action-button:hover { background: var(--ui-surface-muted); }
    .action-button.danger { border-color: color-mix(in srgb, var(--ui-danger) 45%, var(--ui-border-strong)); color: var(--ui-danger); }
    .student-picker { position: relative; }
    .student-results { position: absolute; z-index: 1; width: 100%; max-height: 13rem; overflow: auto; border: 1px solid var(--ui-border-strong); border-radius: .5rem; background: var(--ui-surface); box-shadow: 0 10px 20px rgb(0 0 0 / .12); }
    .student-results button { display: grid; width: 100%; gap: .15rem; border: 0; border-bottom: 1px solid var(--ui-border); padding: .65rem .75rem; text-align: left; color: var(--ui-text); }
    .student-results button:hover { background: var(--ui-surface-muted); }
    .student-results span { color: var(--ui-text-muted); font-size: .8rem; }
    .exercise-row { display: flex; gap: .75rem; align-items: flex-start; border-top: 1px solid var(--ui-border); padding: .75rem 0; }
    .exercise-row input { margin-top: .4rem; }
    .empty { color: var(--ui-text-muted); padding: 1.25rem; text-align: center; }
    .dialog-backdrop { position: fixed; inset: 0; z-index: 999; background: rgb(15 23 42 / .5); }
    .dialog-panel { position: fixed; z-index: 1000; top: 50%; left: 50%; width: min(48rem, calc(100vw - 2rem)); max-height: calc(100vh - 2rem); overflow: auto; transform: translate(-50%, -50%); border: 1px solid var(--ui-border); border-radius: .75rem; background: var(--ui-surface); padding: 1.25rem; box-shadow: 0 25px 50px rgb(0 0 0 / .25); }
    .dialog-panel-wide { width: min(64rem, calc(100vw - 2rem)); }
    .dialog-header { display: flex; align-items: start; justify-content: space-between; gap: 1rem; margin-bottom: 1rem; }
    .dialog-header h3 { margin: 0; font-size: 1.125rem; color: var(--ui-text); }
    .dialog-body { margin-bottom: 1rem; }
  `]
})
export class SpeedReadingContentConfigurationComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService);
  private readonly toaster = inject(ToasterService);

  readonly tabs: { value: ConfigurationTab; label: string }[] = [
    { value: 'age-groups', label: 'Yaş grupları' },
    { value: 'assessments', label: 'Seviye tespit' },
    { value: 'levels', label: 'Seviye sözlüğü' }
  ];
  readonly selectedTab = signal<ConfigurationTab>('age-groups');
  readonly ageGroups = signal<SpeedReadingAgeGroup[]>([]);
  readonly assessmentTemplates = signal<SpeedReadingAssessmentTemplate[]>([]);
  readonly exercises = signal<SpeedReadingExercise[]>([]);
  readonly levelCatalogs = signal<SpeedReadingLevelCatalog[]>([]);
  readonly levels = signal<SpeedReadingLevelDefinition[]>([]);
  readonly measurementCapabilities = signal<SpeedReadingMeasurementCapability[]>([]);
  readonly calibration = signal<SpeedReadingCalibrationReport | null>(null);
  readonly studyEnrollments = signal<SpeedReadingStudyEnrollment[]>([]);
  readonly studyDefinitions = signal<SpeedReadingStudyDefinition[]>([]);
  readonly studyStudents = signal<SpeedReadingStudyStudentOption[]>([]);
  readonly studyEditing = signal(false);
  readonly studyDefinitionEditing = signal(false);
  readonly assessmentEditing = signal(false);
  readonly selectedAssessmentExercises = signal<AssessmentExerciseDraft[]>([]);
  readonly currentTemplate = signal<SpeedReadingAssessmentTemplate | null>(null);
  readonly assessmentAgeGroupId = signal('');
  readonly error = signal('');
  readonly saving = signal(false);
  readonly ageEditing = signal(false);
  readonly levelEditing = signal(false);

  ageEditingId: string | null = null;
  ageDraft: SpeedReadingAgeGroupRequest = this.emptyAgeGroup();
  assessmentName = '';
  exerciseToAdd = '';
  levelCatalogEditingId: string | null = null;
  levelCatalogVersion = '';
  levelCatalogName = '';
  levelDraft: SpeedReadingLevelDefinition[] = [];
  studyDraft = this.emptyStudyEnrollment();
  studyDefinitionEditingId: string | null = null;
  studyDefinitionDraft: SpeedReadingStudyDefinitionRequest = this.emptyStudyDefinition();
  studyStudentSearch = '';
  private studyStudentSearchTimer?: ReturnType<typeof setTimeout>;
  private studyStudentSearchRequest = 0;

  ngOnInit(): void {
    this.loadAgeGroups();
    this.loadAssessmentTemplates();
    this.loadExercises();
    this.loadLevels();
    this.loadMeasurementCapabilities();
    this.loadCalibration();
    this.loadStudyEnrollments();
    this.loadStudyDefinitions();
  }

  selectTab(tab: ConfigurationTab): void {
    this.selectedTab.set(tab);
    this.error.set('');
  }

  @HostListener('document:keydown.escape')
  closeActiveDialog(): void {
    if (this.ageEditing()) this.cancelAgeGroupEdit();
    else if (this.assessmentEditing()) this.closeAssessmentDialog();
    else if (this.levelEditing()) this.cancelLevelCatalogEdit();
    else if (this.studyDefinitionEditing()) this.closeStudyDefinitionDialog();
    else if (this.studyEditing()) this.closeStudyDialog();
  }

  loadAgeGroups(): void {
    this.service.getAgeGroups().subscribe({
      next: value => this.ageGroups.set(value),
      error: () => this.error.set('Yaş grupları yüklenemedi.')
    });
  }

  loadAssessmentTemplates(): void {
    this.service.getAssessmentTemplates().subscribe({
      next: value => this.assessmentTemplates.set(value),
      error: () => this.error.set('Seviye tespit şablonları yüklenemedi.')
    });
  }

  loadExercises(): void {
    this.service.getAllExercises().subscribe({
      next: value => this.exercises.set(value),
      error: () => this.error.set('Egzersiz listesi yüklenemedi.')
    });
  }

  loadLevels(): void {
    this.service.getAssessmentLevels().subscribe({
      next: value => {
        this.levelCatalogs.set(value);
        this.levels.set((value.find(item => item.status === 2) ?? value[0])?.definitions ?? []);
      },
      error: () => this.error.set('Seviye sözlüğü yüklenemedi.')
    });
  }

  startLevelCatalogCreate(): void {
    if (this.saving()) return;
    const active = this.levelCatalogs().find(item => item.status === 2) ?? this.levelCatalogs()[0];
    this.levelCatalogEditingId = null;
    this.levelCatalogVersion = '';
    this.levelCatalogName = active ? `${active.name} - Yeni Sürüm` : 'Yeni Seviye Kataloğu';
    this.levelDraft = (active?.definitions ?? [
      { level: 1, code: 'beginner', displayName: 'Başlangıç', minimumWpm: 0, minimumComprehension: 0 },
      { level: 2, code: 'basic', displayName: 'Temel', minimumWpm: 100, minimumComprehension: 40 }
    ]).map(item => ({ ...item }));
    this.levelEditing.set(true);
  }

  startLevelCatalogEdit(catalog: SpeedReadingLevelCatalog): void {
    if (this.saving() || !catalog.id || catalog.status !== 1) return;
    this.levelCatalogEditingId = catalog.id;
    this.levelCatalogVersion = catalog.version;
    this.levelCatalogName = catalog.name;
    this.levelDraft = catalog.definitions.map(item => ({ ...item }));
    this.levelEditing.set(true);
  }

  cancelLevelCatalogEdit(): void {
    if (this.saving()) return;
    this.dismissLevelCatalogDialog();
  }

  private dismissLevelCatalogDialog(): void {
    this.levelEditing.set(false);
    this.levelCatalogEditingId = null;
    this.levelDraft = [];
  }

  addLevel(): void {
    const previous = this.levelDraft[this.levelDraft.length - 1];
    this.levelDraft = [...this.levelDraft, {
      level: this.levelDraft.length + 1,
      code: `level_${this.levelDraft.length + 1}`,
      displayName: `Seviye ${this.levelDraft.length + 1}`,
      minimumWpm: Math.min(2000, (previous?.minimumWpm ?? 0) + 50),
      minimumComprehension: Math.min(100, (previous?.minimumComprehension ?? 0) + 5)
    }];
  }

  removeLevel(index: number): void {
    if (this.levelDraft.length <= 2) return;
    this.levelDraft = this.levelDraft.filter((_, itemIndex) => itemIndex !== index)
      .map((item, itemIndex) => ({ ...item, level: itemIndex + 1 }));
  }

  saveLevelCatalog(): void {
    if (this.saving() || !this.levelCatalogVersion.trim() || !this.levelCatalogName.trim()) return;
    const definitions = this.levelDraft.map((item, index) => ({ ...item, level: index + 1 }));
    this.saving.set(true);
    const request: Observable<unknown> = this.levelCatalogEditingId
      ? this.service.updateAssessmentLevelCatalog(this.levelCatalogEditingId, { name: this.levelCatalogName.trim(), definitions })
      : this.service.createAssessmentLevelCatalog({ version: this.levelCatalogVersion.trim(), name: this.levelCatalogName.trim(), definitions });
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.dismissLevelCatalogDialog(); this.loadLevels(); },
      error: response => this.error.set(response?.error || 'Seviye kataloğu kaydedilemedi.')
    });
  }

  async publishLevelCatalog(catalog: SpeedReadingLevelCatalog): Promise<void> {
    if (!catalog.id || !await this.toaster.confirm(`“${catalog.version}” sürümü yayınlansın mı?`, { title: 'Seviye kataloğunu yayınla' })) return;
    this.service.publishAssessmentLevelCatalog(catalog.id).subscribe({
      next: () => this.loadLevels(),
      error: response => this.error.set(response?.error || 'Seviye kataloğu yayınlanamadı.')
    });
  }

  levelCatalogStatus(status: number): string {
    return status === 2 ? 'Yayında' : status === 3 ? 'Emekli' : 'Taslak';
  }

  loadMeasurementCapabilities(): void {
    this.service.getAssessmentMeasurementCapabilities().subscribe({
      next: value => this.measurementCapabilities.set(value),
      error: () => this.error.set('Motor ölçüm yetenekleri yüklenemedi.')
    });
  }

  loadCalibration(): void {
    this.service.getAssessmentCalibration().subscribe({
      next: value => this.calibration.set(value),
      error: () => this.error.set('Kalibrasyon kanıtı yüklenemedi.')
    });
  }

  assessmentPhaseName(phase: number): string {
    return phase === 1 ? 'Başlangıç' : phase === 2 ? 'Eğitim sonrası' : phase === 3 ? 'Kalıcılık' : phase === 4 ? 'Transfer' : `Faz ${phase}`;
  }

  loadStudyEnrollments(): void {
    this.service.getAssessmentStudyEnrollments().subscribe({
      next: value => this.studyEnrollments.set(value),
      error: () => this.error.set('Çalışma katılımcıları yüklenemedi.')
    });
  }

  loadStudyDefinitions(): void {
    this.service.getAssessmentStudies().subscribe({
      next: value => this.studyDefinitions.set(value),
      error: () => this.error.set('Araştırma çalışmaları yüklenemedi.')
    });
  }

  activeStudyDefinitions(): SpeedReadingStudyDefinition[] {
    return this.studyDefinitions().filter(study => study.isActive);
  }

  selectedStudyDefinition(): SpeedReadingStudyDefinition | undefined {
    return this.studyDefinitions().find(study => study.id === this.studyDraft.studyDefinitionId);
  }

  openStudyDefinitionDialog(): void {
    if (this.saving()) return;
    this.studyDefinitionEditingId = null;
    this.studyDefinitionDraft = this.emptyStudyDefinition();
    this.studyDefinitionEditing.set(true);
  }

  editStudyDefinition(study: SpeedReadingStudyDefinition): void {
    if (this.saving()) return;
    this.studyDefinitionEditingId = study.id;
    this.studyDefinitionDraft = {
      studyCode: study.studyCode, name: study.name, protocolVersion: study.protocolVersion,
      cohortCode: study.cohortCode, consentDocumentVersion: study.consentDocumentVersion,
      consentDocumentReference: study.consentDocumentReference
    };
    this.studyDefinitionEditing.set(true);
  }

  closeStudyDefinitionDialog(): void {
    if (this.saving()) return;
    this.dismissStudyDefinitionDialog();
  }

  private dismissStudyDefinitionDialog(): void {
    this.studyDefinitionEditing.set(false);
    this.studyDefinitionEditingId = null;
    this.studyDefinitionDraft = this.emptyStudyDefinition();
  }

  saveStudyDefinition(): void {
    if (this.saving()) return;
    this.saving.set(true);
    const request: Observable<unknown> = this.studyDefinitionEditingId
      ? this.service.updateAssessmentStudy(this.studyDefinitionEditingId, this.studyDefinitionDraft)
      : this.service.createAssessmentStudy(this.studyDefinitionDraft);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.dismissStudyDefinitionDialog(); this.loadStudyDefinitions(); },
      error: (response: { error?: string }) => this.error.set(response?.error || 'Araştırma çalışması kaydedilemedi.')
    });
  }

  async retireStudyDefinition(study: SpeedReadingStudyDefinition): Promise<void> {
    if (!await this.toaster.confirm(`“${study.studyCode} · ${study.cohortCode}” çalışması emekliye ayrılsın mı?`, { title: 'Araştırma çalışmasını emekliye ayır' })) return;
    this.service.retireAssessmentStudy(study.id).subscribe({
      next: () => this.loadStudyDefinitions(),
      error: () => this.error.set('Araştırma çalışması emekliye ayrılamadı.')
    });
  }

  saveStudyEnrollment(): void {
    if (this.saving()) return;
    const request = { ...this.studyDraft, consentRecordedAt: new Date(this.studyDraft.consentRecordedAt).toISOString() };
    this.saving.set(true);
    this.service.createAssessmentStudyEnrollment(request).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.dismissStudyDialog(); this.loadStudyEnrollments(); },
      error: response => this.error.set(response?.error || 'Çalışma katılımcısı kaydedilemedi.')
    });
  }

  openStudyDialog(): void {
    if (this.saving()) return;
    this.studyDraft = this.emptyStudyEnrollment();
    this.studyEditing.set(true);
  }

  closeStudyDialog(): void {
    if (this.saving()) return;
    this.dismissStudyDialog();
  }

  private dismissStudyDialog(): void {
    this.studyStudentSearchRequest++;
    if (this.studyStudentSearchTimer) clearTimeout(this.studyStudentSearchTimer);
    this.studyEditing.set(false);
    this.studyDraft = this.emptyStudyEnrollment();
    this.studyStudentSearch = '';
    this.studyStudents.set([]);
  }

  searchStudyStudents(searchTerm: string): void {
    this.studyDraft.studentId = '';
    this.studyStudents.set([]);
    this.studyStudentSearchRequest++;
    if (this.studyStudentSearchTimer) clearTimeout(this.studyStudentSearchTimer);
    const term = searchTerm.trim();
    if (term.length < 2) return;
    const requestId = this.studyStudentSearchRequest;
    this.studyStudentSearchTimer = setTimeout(() => {
      this.service.searchAssessmentStudyStudents(term).subscribe({
        next: students => {
          if (requestId === this.studyStudentSearchRequest) this.studyStudents.set(students);
        },
        error: () => {
          if (requestId === this.studyStudentSearchRequest) this.error.set('Öğrenci araması yapılamadı.');
        }
      });
    }, 250);
  }

  selectStudyStudent(student: SpeedReadingStudyStudentOption): void {
    this.studyDraft.studentId = student.studentId;
    this.studyStudentSearch = student.email
      ? `${student.displayName} — ${student.email}`
      : student.displayName;
    this.studyStudentSearchRequest++;
    if (this.studyStudentSearchTimer) clearTimeout(this.studyStudentSearchTimer);
    this.studyStudents.set([]);
  }

  async withdrawStudyEnrollment(enrollment: SpeedReadingStudyEnrollment): Promise<void> {
    if (!await this.toaster.confirm(`Öğrenci ${enrollment.studentId} “${enrollment.studyCode}” çalışmasından çekilsin mi?`, { title: 'Katılımcıyı çalışmadan çek' })) return;
    this.service.withdrawAssessmentStudyEnrollment(enrollment.id).subscribe({
      next: () => this.loadStudyEnrollments(),
      error: () => this.error.set('Katılımcı çalışmadan çekilemedi.')
    });
  }

  startAgeGroupCreate(): void {
    if (this.saving()) return;
    this.ageEditingId = null;
    this.ageDraft = this.emptyAgeGroup();
    this.ageEditing.set(true);
  }

  startAgeGroupEdit(ageGroup: SpeedReadingAgeGroup): void {
    if (this.saving()) return;
    this.ageEditingId = ageGroup.id;
    this.ageDraft = {
      name: ageGroup.name,
      displayName: ageGroup.displayName,
      minAge: ageGroup.minAge,
      maxAge: ageGroup.maxAge,
      minWpm: ageGroup.minWpm,
      recommendedWpm: ageGroup.recommendedWpm,
      maxWpm: ageGroup.maxWpm,
      recommendedComprehension: ageGroup.recommendedComprehension,
      recommendedDailyMinutes: ageGroup.recommendedDailyMinutes,
      defaultDifficultyLevel: ageGroup.defaultDifficultyLevel,
      orderIndex: ageGroup.orderIndex,
      isActive: ageGroup.isActive,
      description: ageGroup.description
    };
    this.ageEditing.set(true);
  }

  cancelAgeGroupEdit(): void {
    if (this.saving()) return;
    this.dismissAgeGroupDialog();
  }

  private dismissAgeGroupDialog(): void {
    this.ageEditing.set(false);
    this.ageEditingId = null;
  }

  saveAgeGroup(): void {
    if (this.saving()) return;
    this.error.set('');
    this.saving.set(true);
    const request: Observable<unknown> = this.ageEditingId
      ? this.service.updateAgeGroup(this.ageEditingId, this.ageDraft)
      : this.service.createAgeGroup(this.ageDraft);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.dismissAgeGroupDialog();
        this.loadAgeGroups();
      },
      error: () => this.error.set('Yaş grubu kaydedilemedi.')
    });
  }

  async deleteAgeGroup(ageGroup: SpeedReadingAgeGroup): Promise<void> {
    if (this.saving() || !await this.toaster.confirm(`“${ageGroup.displayName}” yaş grubu silinsin mi?`, { title: 'Yaş grubunu sil' })) return;
    this.service.deleteAgeGroup(ageGroup.id).subscribe({
      next: () => this.loadAgeGroups(),
      error: () => this.error.set('Yaş grubu silinemedi; bağlı içerikleri kontrol edin.')
    });
  }

  loadAssessmentForAgeGroup(): void {
    const ageGroupId = this.assessmentAgeGroupId();
    this.resetAssessmentForm();
    if (!ageGroupId) return;

    const ageGroup = this.ageGroups().find(item => item.id === ageGroupId);
    this.assessmentName = ageGroup ? `Seviye Tespit - ${ageGroup.displayName}` : '';
    this.service.getAssessmentTemplateByAgeGroup(ageGroupId).subscribe({
      next: template => this.applyAssessmentTemplate(template),
      error: response => {
        if (response.status !== 404) this.error.set('Seviye tespit şablonu yüklenemedi.');
      }
    });
  }

  selectAssessmentTemplate(template: SpeedReadingAssessmentTemplate): void {
    if (this.saving()) return;
    this.assessmentAgeGroupId.set(template.targetAgeGroupId);
    this.applyAssessmentTemplate(template);
    this.assessmentEditing.set(true);
  }

  openAssessmentDialog(): void {
    if (this.saving() || !this.assessmentAgeGroupId()) return;
    this.assessmentEditing.set(true);
  }

  closeAssessmentDialog(): void {
    if (this.saving()) return;
    this.dismissAssessmentDialog();
    this.restoreAssessmentDraft();
  }

  private dismissAssessmentDialog(): void {
    this.assessmentEditing.set(false);
  }

  addAssessmentExercise(exerciseId: string): void {
    this.exerciseToAdd = '';
    const exercise = this.exercises().find(item => item.id === exerciseId);
    if (!exercise || this.selectedAssessmentExercises().some(item => item.exerciseId === exerciseId)) return;
    const selected = [...this.selectedAssessmentExercises(), {
      exerciseId: exercise.id,
      exerciseTitle: exercise.title,
      exerciseType: exercise.exerciseTypeName,
      difficultyLevel: exercise.difficultyLevel,
      customTitle: exercise.title,
      customDescription: '',
      displayOrder: this.selectedAssessmentExercises().length + 1
    }];
    this.selectedAssessmentExercises.set(selected);
  }

  removeAssessmentExercise(index: number): void {
    this.setAssessmentExercises(this.selectedAssessmentExercises().filter((_, itemIndex) => itemIndex !== index));
  }

  moveAssessmentExercise(index: number, direction: -1 | 1): void {
    const targetIndex = index + direction;
    const selected = [...this.selectedAssessmentExercises()];
    if (targetIndex < 0 || targetIndex >= selected.length) return;
    [selected[index], selected[targetIndex]] = [selected[targetIndex], selected[index]];
    this.setAssessmentExercises(selected);
  }

  saveAssessment(): void {
    const ageGroupId = this.assessmentAgeGroupId();
    if (this.saving() || !ageGroupId || !this.assessmentName.trim() || this.selectedAssessmentExercises().length === 0) return;
    const exercises = this.selectedAssessmentExercises().map((exercise, index) => ({
      exerciseId: exercise.exerciseId,
      customTitle: exercise.customTitle || null,
      customDescription: exercise.customDescription || null,
      displayOrder: index + 1
    }));
    this.error.set('');
    this.saving.set(true);
    const request: Observable<unknown> = this.currentTemplate()
      ? this.service.updateAssessmentTemplate(this.currentTemplate()!.id, { name: this.assessmentName.trim(), exercises })
      : this.service.createAssessmentTemplate({ name: this.assessmentName.trim(), targetAgeGroupId: ageGroupId, exercises });
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.dismissAssessmentDialog();
        this.loadAssessmentTemplates();
        this.loadAssessmentForAgeGroup();
      },
      error: () => this.error.set('Seviye tespit şablonu kaydedilemedi.')
    });
  }

  async deleteAssessment(template = this.currentTemplate()): Promise<void> {
    if (this.saving() || !template || !await this.toaster.confirm(`“${template.name}” şablonu silinsin mi?`, { title: 'Seviye tespit şablonunu sil' })) return;
    const deletedCurrentTemplate = this.currentTemplate()?.id === template.id;
    this.saving.set(true);
    this.service.deleteAssessmentTemplate(template.id).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        if (deletedCurrentTemplate) {
          this.dismissAssessmentDialog();
          this.resetAssessmentForm();
          this.loadAssessmentForAgeGroup();
        }
        this.loadAssessmentTemplates();
      },
      error: () => this.error.set('Seviye tespit şablonu silinemedi.')
    });
  }

  resetAssessmentSelection(): void {
    this.assessmentAgeGroupId.set('');
    this.resetAssessmentForm();
  }

  private applyAssessmentTemplate(template: SpeedReadingAssessmentTemplate): void {
    this.currentTemplate.set(template);
    this.assessmentName = template.name;
    this.setAssessmentExercises(template.exercises.map(exercise => ({
      exerciseId: exercise.exerciseId,
      exerciseTitle: exercise.exerciseTitle,
      exerciseType: exercise.exerciseType,
      difficultyLevel: exercise.difficultyLevel,
      customTitle: exercise.customTitle,
      customDescription: exercise.customDescription,
      displayOrder: exercise.displayOrder
    })));
  }

  private resetAssessmentForm(): void {
    this.currentTemplate.set(null);
    this.selectedAssessmentExercises.set([]);
    this.exerciseToAdd = '';
  }

  private restoreAssessmentDraft(): void {
    const template = this.currentTemplate();
    if (template) {
      this.applyAssessmentTemplate(template);
      return;
    }

    this.resetAssessmentForm();
    const ageGroup = this.ageGroups().find(item => item.id === this.assessmentAgeGroupId());
    this.assessmentName = ageGroup ? `Seviye Tespit - ${ageGroup.displayName}` : '';
  }

  private setAssessmentExercises(exercises: AssessmentExerciseDraft[]): void {
    this.selectedAssessmentExercises.set(exercises.map((exercise, index) => ({ ...exercise, displayOrder: index + 1 })));
  }

  private emptyAgeGroup(): SpeedReadingAgeGroupRequest {
    return {
      name: '', displayName: '', minAge: 0, maxAge: null, minWpm: 0, recommendedWpm: 0,
      maxWpm: 0, recommendedComprehension: 0, recommendedDailyMinutes: 15,
      defaultDifficultyLevel: 1, orderIndex: 0, isActive: true, description: ''
    };
  }

  private emptyStudyEnrollment() {
    const now = new Date();
    const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
    return { studentId: '', studyDefinitionId: '', consentRecordedAt: local };
  }

  private emptyStudyDefinition(): SpeedReadingStudyDefinitionRequest {
    return { studyCode: '', name: '', protocolVersion: '', cohortCode: '', consentDocumentVersion: '', consentDocumentReference: '' };
  }
}
