import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Observable, Subscription, finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { ToasterService } from '../../../core/services/toaster.service';
import {
  AdminContentAnalysisAnalytics,
  AdminInstitutionAnalytics,
  AdminPlatformUsageAnalytics,
  AdminStudentProgressDetails,
  AdminStudentProgressSummary,
  AdminSystemHealthAnalytics,
  SpeedReadingAdminService,
  SpeedReadingProgramAnalytics,
  SpeedReadingPage,
  SpeedReadingTeacherAssignmentAnalytics,
  SpeedReadingTeacherClassOverviewAnalytics,
  SpeedReadingTeacherContentAnalysisAnalytics,
  SpeedReadingTeacherTimeProgressAnalytics
} from '../../../core/services/speed-reading-admin.service';

type SpeedReadingAnalyticsTab = 'platform' | 'content' | 'health' | 'institutions' | 'programs' | 'progress' | 'teacher';

@Component({
  selector: 'app-speed-reading-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="speed-reading-analytics-title">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Hızlı Okuma servisi</p>
        <h1 id="speed-reading-analytics-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Analitik ve öğrenci ilerlemeleri</h1>
        <p class="mt-2 max-w-3xl text-sm text-gray-600 dark:text-gray-300">
          Platform kullanımını, içerik performansını ve program ilerlemelerini servis tarafından hesaplanan gerçek verilerle izleyin.
          Veri bulunmayan alanlar özellikle “veri yok” olarak gösterilir.
        </p>
      </header>

      <form (ngSubmit)="applyFilters()" class="grid grid-cols-1 gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800 md:grid-cols-[1fr_1fr_auto]">
        <label class="text-sm font-medium text-gray-700 dark:text-gray-200">Başlangıç tarihi
          <input type="date" [(ngModel)]="dateFrom" name="dateFrom" required class="mt-1 block w-full rounded-lg border border-gray-300 bg-transparent px-3 py-2 text-sm dark:border-gray-600" />
        </label>
        <label class="text-sm font-medium text-gray-700 dark:text-gray-200">Bitiş tarihi
          <input type="date" [(ngModel)]="dateTo" name="dateTo" required class="mt-1 block w-full rounded-lg border border-gray-300 bg-transparent px-3 py-2 text-sm dark:border-gray-600" />
        </label>
        <button type="submit" class="self-end rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50" [disabled]="loading() || !dateFrom || !dateTo">Yenile</button>
      </form>

      <nav class="flex flex-wrap gap-2" aria-label="Hızlı Okuma analitik sekmeleri">
        @for (tab of tabs; track tab.value) {
          @if (tab.visible()) {
            <button type="button" (click)="selectTab(tab.value)" [attr.aria-pressed]="selectedTab() === tab.value"
              [class.bg-indigo-600]="selectedTab() === tab.value" [class.text-white]="selectedTab() === tab.value"
              class="rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-200 dark:hover:bg-gray-700">
              {{ tab.label }}
            </button>
          }
        }
      </nav>

      @if (error()) {
        <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div>
      }

      @if (selectedTab() === 'platform') {
        @if (platformUsage(); as data) {
          <section class="space-y-4" aria-labelledby="platform-title">
            <h2 id="platform-title" class="text-lg font-semibold text-gray-900 dark:text-white">Platform kullanımı</h2>
            <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
              <div class="metric-card"><span>Toplam kullanıcı</span><strong>{{ data.totalUsers }}</strong></div>
              <div class="metric-card"><span>Aktif kullanıcı</span><strong>{{ data.activeUsers }}</strong></div>
              <div class="metric-card"><span>Okuma oturumu</span><strong>{{ data.totalReadingSessions }}</strong></div>
              <div class="metric-card"><span>Toplam aktivite</span><strong>{{ data.totalActivities }}</strong></div>
            </div>
            <div class="grid grid-cols-1 gap-4 lg:grid-cols-2">
              <div class="data-card"><h3>Kullanıcı büyümesi</h3><p class="muted">{{ data.newUserDataAvailable ? (data.newUsers + ' yeni kullanıcı') : 'Kayıt tarihi verisi mevcut değil.' }}</p><p class="muted">{{ data.userGrowthRateDataAvailable ? ('Büyüme oranı: ' + data.userGrowthRate + '%') : 'Büyüme oranı hesaplanamadı.' }}</p></div>
              <div class="data-card"><h3>Etkileşim</h3><p class="muted">Etkileşim oranı: {{ data.engagementRate }}%</p><p class="muted">Elde tutma oranı: {{ data.retentionRate }}%</p><p class="muted">Ortalama oturum: {{ data.averageSessionDuration }} dakika</p></div>
            </div>
            <div class="data-card"><h3>En çok kullanılan içerikler</h3><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>İçerik</th><th>Tür</th><th>Kullanım</th></tr></thead><tbody>@for (item of data.popularContent; track item.title + item.type) {<tr><td>{{ item.title }}</td><td>{{ item.type }}</td><td>{{ item.usageCount }}</td></tr>} @empty {<tr><td colspan="3" class="empty">Veri yok.</td></tr>}</tbody></table></div></div>
          </section>
        }
      }

      @if (selectedTab() === 'content') {
        @if (contentAnalysis(); as data) {
          <section class="space-y-4" aria-labelledby="content-title">
            <h2 id="content-title" class="text-lg font-semibold text-gray-900 dark:text-white">İçerik analizi</h2>
            <div class="grid grid-cols-2 gap-3 lg:grid-cols-4"><div class="metric-card"><span>Egzersiz</span><strong>{{ data.totalExercises }}</strong></div><div class="metric-card"><span>Okuma metni</span><strong>{{ data.totalReadingTexts }}</strong></div><div class="metric-card"><span>Program şablonu</span><strong>{{ data.totalProgramTemplates }}</strong></div><div class="metric-card"><span>Atama</span><strong>{{ data.assignmentDataAvailable ? data.totalAssignments : 'Veri yok' }}</strong></div></div>
            <div class="grid grid-cols-1 gap-4 lg:grid-cols-2"><div class="data-card"><h3>En çok kullanılan içerikler</h3><ul class="simple-list">@for (item of data.mostUsedContent; track item.contentId) {<li><span>{{ item.title }}</span><strong>{{ item.usageCount }}</strong></li>} @empty {<li class="empty">Veri yok.</li>}</ul></div><div class="data-card"><h3>İçerik boşlukları</h3><ul class="simple-list">@for (item of data.contentGaps; track item) {<li>{{ item }}</li>} @empty {<li class="empty">Tespit edilmiş boşluk yok.</li>}</ul></div></div>
            <div class="data-card"><h3>Zorluk seviyesine göre okuma performansı</h3><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Seviye</th><th>Okuma</th><th>Ort. WPM</th><th>Ort. anlama</th></tr></thead><tbody>@for (item of data.readingAnalysis; track item.difficultyLevel) {<tr><td>{{ item.difficultyLevel }}</td><td>{{ item.totalReads }}</td><td>{{ item.averageWpm }}</td><td>{{ item.averageComprehension }}%</td></tr>} @empty {<tr><td colspan="4" class="empty">Veri yok.</td></tr>}</tbody></table></div></div>
          </section>
        }
      }

      @if (selectedTab() === 'health') {
        @if (systemHealth(); as data) {
          <section class="space-y-4" aria-labelledby="health-title"><h2 id="health-title" class="text-lg font-semibold text-gray-900 dark:text-white">Öğrenme performansı ve servis sağlığı</h2><div class="grid grid-cols-2 gap-3 lg:grid-cols-4"><div class="metric-card"><span>Sağlık durumu</span><strong>{{ data.healthStatus }}</strong></div><div class="metric-card"><span>Ort. WPM</span><strong>{{ data.averagePlatformWpm }}</strong></div><div class="metric-card"><span>Ort. anlama</span><strong>{{ data.averagePlatformComprehension }}%</strong></div><div class="metric-card"><span>Başarı oranı</span><strong>{{ data.successRate }}%</strong></div></div><div class="data-card"><h3>Toplam ölçümler</h3><p class="muted">Tamamlanan egzersiz: {{ data.totalExercisesCompleted }} · Yanıtlanan soru: {{ data.totalQuestionsAnswered }}</p><p class="muted">{{ data.errorRateDataAvailable ? ('Hata oranı: ' + data.errorRate + '%') : 'Operasyonel hata telemetrisi bu serviste tutulmuyor.' }}</p></div><div class="data-card"><h3>Uyarılar</h3><ul class="simple-list">@for (alert of data.systemAlerts; track alert.detectedAt + alert.alertType) {<li><span>{{ alert.message }}</span><strong>{{ alert.severity }}</strong></li>} @empty {<li class="empty">{{ data.systemAlertsDataAvailable ? 'Uyarı yok.' : 'Uyarı telemetrisi mevcut değil.' }}</li>}</ul></div></section>
        }
      }

      @if (selectedTab() === 'institutions') {
        @if (institutionAnalytics(); as data) {
          <section class="space-y-4" aria-labelledby="institutions-title"><h2 id="institutions-title" class="text-lg font-semibold text-gray-900 dark:text-white">Kurum karşılaştırması</h2><div class="grid grid-cols-2 gap-3 lg:grid-cols-5"><div class="metric-card"><span>Kurum</span><strong>{{ data.totalInstitutions }}</strong></div><div class="metric-card"><span>Aktif kurum</span><strong>{{ data.activeInstitutions }}</strong></div><div class="metric-card"><span>Kullanıcı</span><strong>{{ data.totalUsers }}</strong></div><div class="metric-card"><span>Öğrenci</span><strong>{{ data.totalStudents }}</strong></div><div class="metric-card"><span>Öğretmen</span><strong>{{ data.totalTeachers }}</strong></div></div><div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Kurum</th><th>Kullanıcı</th><th>Aktivite</th><th>WPM</th><th>Anlama</th><th>Etkileşim</th></tr></thead><tbody>@for (item of data.institutionComparison; track item.institutionId) {<tr><td>{{ item.institutionName }}</td><td>{{ item.activeUsers }} / {{ item.totalUsers }}</td><td>{{ item.totalActivities }}</td><td>{{ item.averageWpmDataAvailable ? item.averageWpm : '—' }}</td><td>{{ item.averageComprehensionDataAvailable ? (item.averageComprehension + '%') : '—' }}</td><td>{{ item.engagementRate }}%</td></tr>} @empty {<tr><td colspan="6" class="empty">Veri yok.</td></tr>}</tbody></table></div></div></section>
        }
      }

      @if (selectedTab() === 'programs') {
        @if (programAnalytics(); as data) {
          <section class="space-y-4" aria-labelledby="programs-title"><h2 id="programs-title" class="text-lg font-semibold text-gray-900 dark:text-white">Program analitiği</h2><div class="grid grid-cols-2 gap-3 lg:grid-cols-4"><div class="metric-card"><span>Aktif öğrenci</span><strong>{{ data.platformStats.totalActiveStudents }}</strong></div><div class="metric-card"><span>Ort. başarı</span><strong>{{ data.platformStats.averageSuccessRate }}%</strong></div><div class="metric-card"><span>Ort. seri</span><strong>{{ data.platformStats.averageCurrentStreak }}</strong></div><div class="metric-card"><span>Tamamlanan egzersiz</span><strong>{{ data.platformStats.totalCompletedExercises }}</strong></div></div><div class="grid grid-cols-1 gap-4 lg:grid-cols-2"><div class="data-card"><h3>Program dağılımı</h3><ul class="simple-list">@for (item of data.programDistribution; track item.programName) {<li><span>{{ item.programName }}</span><strong>{{ item.studentCount }} ({{ item.percentage }}%)</strong></li>} @empty {<li class="empty">Veri yok.</li>}</ul></div><div class="data-card"><h3>Haftalık ilerleme</h3><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Hafta</th><th>Ort. ilerleme</th><th>Tamamlanma</th></tr></thead><tbody>@for (item of data.weeklyProgress; track item.weekNumber) {<tr><td>{{ item.weekNumber }}</td><td>{{ item.averageProgress }}%</td><td>{{ item.completionRate }}%</td></tr>} @empty {<tr><td colspan="3" class="empty">Veri yok.</td></tr>}</tbody></table></div></div></div><div class="data-card"><h3>Son öğrenci aktiviteleri</h3><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Öğrenci</th><th>Program</th><th>Hafta/gün</th><th>Seri</th><th>Son aktivite</th></tr></thead><tbody>@for (item of data.recentStudentProgress; track item.studentEmail + item.lastActivityDate) {<tr><td>{{ item.studentName }}<div class="muted">{{ item.studentEmail }}</div></td><td>{{ item.programName }}</td><td>{{ item.currentWeek }} / {{ item.currentDay }}</td><td>{{ item.currentStreak }} (en uzun {{ item.longestStreak }})</td><td>{{ item.lastActivityDate | date:'dd.MM.yyyy HH:mm' }}</td></tr>} @empty {<tr><td colspan="5" class="empty">Veri yok.</td></tr>}</tbody></table></div></div></section>
        }
      }

  @if (selectedTab() === 'progress') {
        <section class="space-y-4" aria-labelledby="progress-title"><div class="flex flex-col justify-between gap-3 sm:flex-row sm:items-end"><div><h2 id="progress-title" class="text-lg font-semibold text-gray-900 dark:text-white">Öğrenci program ilerlemeleri</h2><p class="muted">İlerleme kayıtlarını inceleyin; sıfırlama işlemi yalnızca ProgramManage yetkisi olan yöneticilere açıktır.</p></div><label class="text-sm font-medium text-gray-700 dark:text-gray-200">Öğrenci ara<input [(ngModel)]="progressSearch" (ngModelChange)="searchProgress()" name="progressSearch" maxlength="100" class="mt-1 block rounded-lg border border-gray-300 bg-transparent px-3 py-2 text-sm dark:border-gray-600" /></label></div><div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>İlerleme ID</th><th>Kullanıcı ID</th><th>Program ID</th><th>Gün</th><th>Tamamlanan gün</th><th>Egzersiz</th><th>Atanma</th><th></th></tr></thead><tbody>@for (item of progressPage()?.items; track item.id) {<tr><td class="font-mono text-xs">{{ item.id }}</td><td class="font-mono text-xs">{{ item.userId }}</td><td class="font-mono text-xs">{{ item.programTemplateId }}</td><td>{{ item.currentDay }}</td><td>{{ item.daysCompleted }}</td><td>{{ item.exercisesCompleted }}</td><td>{{ item.assignedDate | date:'dd.MM.yyyy' }}</td><td class="flex gap-2"><button type="button" (click)="openProgress(item)" class="rounded-md border px-2 py-1 text-xs">Detay</button>@if (canResetProgress()) {<button type="button" (click)="resetProgress(item)" [disabled]="loading()" class="rounded-md border border-amber-300 px-2 py-1 text-xs text-amber-700">Sıfırla</button>}</td></tr>} @empty {<tr><td colspan="8" class="empty">{{ loading() ? 'Yükleniyor…' : 'İlerleme kaydı bulunamadı.' }}</td></tr>}</tbody></table></div><div class="mt-3 flex items-center justify-between text-xs text-gray-500"><span>Toplam {{ progressPage()?.totalCount ?? 0 }} kayıt</span><div class="flex gap-2"><button type="button" (click)="changeProgressPage(progressPageNumber - 1)" [disabled]="progressPageNumber <= 1 || loading()" class="rounded border px-2 py-1 disabled:opacity-40">Önceki</button><button type="button" (click)="changeProgressPage(progressPageNumber + 1)" [disabled]="!progressPage() || progressPageNumber >= progressTotalPages() || loading()" class="rounded border px-2 py-1 disabled:opacity-40">Sonraki</button></div></div></div>
        @if (progressDetails(); as details) {<div class="data-card"><div class="flex items-center justify-between"><h3>İlerleme ayrıntısı</h3><button type="button" (click)="progressDetails.set(null)" class="text-sm text-gray-500">Kapat</button></div><p class="muted">Güncel hafta: {{ details.progress.currentWeek }} · Zorluk: {{ details.progress.currentDifficultyLevel }} · Başarı: {{ details.progress.averageSuccessRate }}%</p><div class="mt-3 overflow-x-auto"><table class="data-table"><thead><tr><th>Tarih</th><th>Gün/hafta</th><th>Sonuç</th><th>WPM</th><th>Anlama</th><th>Ölçüm</th></tr></thead><tbody>@for (log of details.recentLogs; track log.id) {<tr><td>{{ log.completedDate | date:'dd.MM.yyyy HH:mm' }}</td><td>{{ log.dayNumber }} / {{ log.weekNumber }}</td><td>{{ log.isPassed ? 'Geçti' : 'Başarısız' }}</td><td>{{ log.averageWpm ?? '—' }}</td><td>{{ log.averageComprehension ?? '—' }}</td><td>{{ log.measurementStatus }}</td></tr>} @empty {<tr><td colspan="6" class="empty">Son kayıt yok.</td></tr>}</tbody></table></div></div>}
        </section>
      }

      @if (selectedTab() === 'teacher') {
        <section class="space-y-4" aria-labelledby="teacher-analytics-title">
          <div class="data-card"><h2 id="teacher-analytics-title">Öğretmen analitiği</h2><p class="muted">Öğretmen ID’si üzerinden sınıf, atama, içerik ve zaman/ilerleme özetlerini servis kapsam politikasıyla görüntüleyin.</p><form (ngSubmit)="loadTeacherAnalytics()" class="teacher-form"><label>Öğretmen ID<input [(ngModel)]="teacherId" name="teacherId" required maxlength="36" placeholder="GUID" /></label><button type="submit" class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white" [disabled]="loading() || !teacherId.trim()">Yükle</button></form></div>
          @if (teacherClassOverview(); as overview) {
            <div class="grid grid-cols-2 gap-3 lg:grid-cols-4"><div class="metric-card"><span>Öğrenci</span><strong>{{ overview.totalStudents }}</strong></div><div class="metric-card"><span>Aktif öğrenci</span><strong>{{ overview.activeStudentsDataAvailable ? overview.activeStudents : 'Veri yok' }}</strong></div><div class="metric-card"><span>Ort. WPM</span><strong>{{ overview.classAverageWpmDataAvailable ? overview.classAverageWpm : 'Veri yok' }}</strong></div><div class="metric-card"><span>Ort. anlama</span><strong>{{ overview.classAverageComprehensionDataAvailable ? (overview.classAverageComprehension + '%') : 'Veri yok' }}</strong></div></div>
            <div class="grid grid-cols-1 gap-4 lg:grid-cols-2"><div class="data-card"><h3>En iyi performans</h3><table class="data-table"><thead><tr><th>Öğrenci</th><th>WPM</th><th>Anlama</th><th>Aktivite</th></tr></thead><tbody>@for (student of overview.topPerformers; track student.studentIdentifier) {<tr><td>{{ student.studentIdentifier }}</td><td>{{ student.averageWpm }}</td><td>{{ student.averageComprehension }}%</td><td>{{ student.activitiesCompleted }}</td></tr>} @empty {<tr><td colspan="4" class="empty">Veri yok.</td></tr>}</tbody></table></div><div class="data-card"><h3>Destek gerekenler</h3><table class="data-table"><thead><tr><th>Öğrenci</th><th>WPM</th><th>Anlama</th><th>Düzey</th></tr></thead><tbody>@for (student of overview.studentsNeedingSupport; track student.studentIdentifier) {<tr><td>{{ student.studentIdentifier }}</td><td>{{ student.averageWpm }}</td><td>{{ student.averageComprehension }}%</td><td>{{ student.performanceLevel }}</td></tr>} @empty {<tr><td colspan="4" class="empty">Veri yok.</td></tr>}</tbody></table></div></div>
          }
          @if (teacherAssignmentAnalytics(); as assignments) {<div class="data-card"><h3>Atama analitiği</h3>@if (assignments.completionStats; as completion) {<p class="muted">Tamamlanan: {{ completion.completed }} · Devam eden: {{ completion.inProgress }} · Başlamayan: {{ completion.notStarted }} · Tamamlanma: {{ completion.completionRate }}%</p>} @if (!assignments.dataAvailable) {<p class="empty">{{ assignments.unavailableReason || 'Atama verisi yok.' }}</p>} @else {<div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Öğrenci</th><th>Durum</th><th>Puan</th><th>Teslim</th></tr></thead><tbody>@for (student of assignments.studentBreakdown; track student.studentId) {<tr><td>{{ student.studentName }}</td><td>{{ student.status }}</td><td>{{ student.score ?? '-' }}</td><td>{{ student.submittedAt ? (student.submittedAt | date:'dd.MM.yyyy HH:mm') : '-' }}</td></tr>} @empty {<tr><td colspan="4" class="empty">Veri yok.</td></tr>}</tbody></table></div>}</div>}
          @if (teacherContentAnalysis(); as content) {<div class="data-card"><h3>Öğretmen içerik analizi</h3><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Egzersiz türü</th><th>Tamamlanma</th><th>Aktif öğrenci</th><th>Ort. puan</th></tr></thead><tbody>@for (item of content.exerciseAnalysis; track item.exerciseTypeName) {<tr><td>{{ item.exerciseTypeName }}</td><td>{{ item.totalCompletions }}</td><td>{{ item.activeStudents }}</td><td>{{ item.averageScore }}</td></tr>} @empty {<tr><td colspan="4" class="empty">Veri yok.</td></tr>}</tbody></table></div></div>}
          @if (teacherTimeProgress(); as progress) {<div class="data-card"><h3>İlerleme eğilimleri</h3><div class="grid grid-cols-1 gap-4 lg:grid-cols-2"><div><h4>İyileşen öğrenciler</h4><ul class="simple-list">@for (student of progress.improvingStudents; track student.studentId) {<li><span>{{ student.studentName }}</span><strong>+{{ student.improvement }}</strong></li>} @empty {<li class="empty">Veri yok.</li>}</ul></div><div><h4>Gerileyen öğrenciler</h4><ul class="simple-list">@for (student of progress.decliningStudents; track student.studentId) {<li><span>{{ student.studentName }}</span><strong>{{ student.improvement }}</strong></li>} @empty {<li class="empty">Veri yok.</li>}</ul></div></div></div>}
        </section>
      }

      @if (loading()) {<div class="text-center text-sm text-gray-500" role="status">Yükleniyor…</div>}
    </main>
  `,
  styles: [`
    .metric-card, .data-card { border: 1px solid rgb(229 231 235); border-radius: .75rem; background: white; padding: 1rem; }
    .metric-card span { display: block; font-size: .75rem; font-weight: 500; color: rgb(107 114 128); }
    .metric-card strong { display: block; margin-top: .5rem; font-size: 1.25rem; font-weight: 700; color: rgb(17 24 39); }
    .data-card h3 { margin-bottom: .75rem; font-size: .875rem; font-weight: 600; color: rgb(17 24 39); }
    .muted { font-size: .875rem; color: rgb(107 114 128); }
    .data-table { width: 100%; text-align: left; font-size: .875rem; }
    .data-table th { border-bottom: 1px solid rgb(229 231 235); padding: .5rem .75rem; font-size: .75rem; text-transform: uppercase; color: rgb(107 114 128); }
    .data-table td { border-bottom: 1px solid rgb(243 244 246); padding: .5rem .75rem; color: rgb(55 65 81); }
    .simple-list { display: grid; gap: .5rem; font-size: .875rem; color: rgb(55 65 81); }
    .simple-list li { display: flex; align-items: center; justify-content: space-between; gap: .75rem; border-bottom: 1px solid rgb(243 244 246); padding-bottom: .5rem; }
    .empty { padding: 1.5rem 0; text-align: center; font-size: .875rem; color: rgb(107 114 128); }
    .teacher-form { display: flex; align-items: end; gap: .75rem; margin-top: 1rem; } .teacher-form label { display: grid; gap: .35rem; width: min(100%, 28rem); font-size: .875rem; font-weight: 500; color: rgb(55 65 81); } .teacher-form input { border: 1px solid rgb(209 213 219); border-radius: .5rem; padding: .5rem .75rem; background: transparent; }
    @media (prefers-color-scheme: dark) {
      .metric-card, .data-card { border-color: rgb(55 65 81); background: rgb(31 41 55); }
      .metric-card span, .muted, .data-table th, .empty { color: rgb(156 163 175); }
      .metric-card strong, .data-card h3 { color: white; }
      .data-table th, .data-table td, .simple-list li { border-color: rgb(55 65 81); }
      .data-table td, .simple-list, .teacher-form label { color: rgb(209 213 219); }
      .teacher-form input { border-color: rgb(75 85 99); }
    }
  `]
})
export class SpeedReadingAnalyticsComponent implements OnInit, OnDestroy {
  private readonly service = inject(SpeedReadingAdminService);
  private readonly authService = inject(AuthService);
  private readonly toaster = inject(ToasterService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  private request?: Subscription;

  readonly canPlatformAnalytics = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingPlatformAnalytics));
  readonly canProgress = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingProgressView));
  readonly canResetProgress = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingProgramManage));
  readonly canTeacherAnalytics = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingReportView));
  readonly tabs: ReadonlyArray<{ value: SpeedReadingAnalyticsTab; label: string; visible: () => boolean }> = [
    { value: 'platform', label: 'Platform kullanımı', visible: () => this.canPlatformAnalytics() },
    { value: 'content', label: 'İçerik analizi', visible: () => this.canPlatformAnalytics() },
    { value: 'health', label: 'Öğrenme sağlığı', visible: () => this.canPlatformAnalytics() },
    { value: 'institutions', label: 'Kurumlar', visible: () => this.canPlatformAnalytics() },
    { value: 'programs', label: 'Programlar', visible: () => this.canPlatformAnalytics() },
    { value: 'progress', label: 'Öğrenci ilerlemeleri', visible: () => this.canProgress() },
    { value: 'teacher', label: 'Öğretmen analitiği', visible: () => this.canTeacherAnalytics() }
  ];

  readonly selectedTab = signal<SpeedReadingAnalyticsTab>('platform');
  readonly loading = signal(false);
  readonly error = signal('');
  readonly platformUsage = signal<AdminPlatformUsageAnalytics | null>(null);
  readonly contentAnalysis = signal<AdminContentAnalysisAnalytics | null>(null);
  readonly systemHealth = signal<AdminSystemHealthAnalytics | null>(null);
  readonly institutionAnalytics = signal<AdminInstitutionAnalytics | null>(null);
  readonly programAnalytics = signal<SpeedReadingProgramAnalytics | null>(null);
  readonly progressPage = signal<SpeedReadingPage<AdminStudentProgressSummary> | null>(null);
  readonly progressDetails = signal<AdminStudentProgressDetails | null>(null);
  readonly teacherClassOverview = signal<SpeedReadingTeacherClassOverviewAnalytics | null>(null);
  readonly teacherAssignmentAnalytics = signal<SpeedReadingTeacherAssignmentAnalytics | null>(null);
  readonly teacherContentAnalysis = signal<SpeedReadingTeacherContentAnalysisAnalytics | null>(null);
  readonly teacherTimeProgress = signal<SpeedReadingTeacherTimeProgressAnalytics | null>(null);
  dateFrom = this.defaultDate(-29);
  dateTo = this.defaultDate(0);
  progressSearch = '';
  teacherId = '';
  progressPageNumber = 1;
  private readonly progressPageSize = 25;

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const initialTab = this.route.snapshot.data['defaultTab'] as SpeedReadingAnalyticsTab | undefined;
    if (initialTab) this.selectedTab.set(initialTab);
    else if (!this.canPlatformAnalytics() && this.canProgress()) this.selectedTab.set('progress');
    this.load();
  }

  ngOnDestroy(): void { this.request?.unsubscribe(); }

  selectTab(tab: SpeedReadingAnalyticsTab): void {
    if (this.selectedTab() === tab) return;
    this.selectedTab.set(tab);
    this.error.set('');
    this.load();
  }

  applyFilters(): void {
    if (!this.dateFrom || !this.dateTo || this.dateFrom > this.dateTo) {
      this.error.set('Geçerli bir tarih aralığı seçin.');
      return;
    }
    this.load();
  }

  searchProgress(): void {
    if (this.selectedTab() !== 'progress') return;
    this.progressPageNumber = 1;
    this.loadProgress();
  }

  changeProgressPage(page: number): void {
    if (page < 1 || page > this.progressTotalPages()) return;
    this.progressPageNumber = page;
    this.loadProgress();
  }

  progressTotalPages(): number {
    return Math.max(1, Math.ceil((this.progressPage()?.totalCount ?? 0) / this.progressPageSize));
  }

  openProgress(item: AdminStudentProgressSummary): void {
    this.loading.set(true);
    this.error.set('');
    this.service.getStudentProgressDetails(item.id).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: details => this.progressDetails.set(details),
      error: () => this.error.set('Öğrenci ilerleme ayrıntısı yüklenemedi.')
    });
  }

  async resetProgress(item: AdminStudentProgressSummary): Promise<void> {
    if (!this.canResetProgress() || !await this.toaster.confirm('Bu öğrencinin program ilerlemesi sıfırlansın mı?', { title: 'İlerlemeyi sıfırla' })) return;

    this.loading.set(true);
    this.error.set('');
    this.service.resetStudentProgress(item.id).subscribe({
      next: () => {
        this.progressDetails.set(null);
        this.loadProgress();
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Öğrenci ilerlemesi sıfırlanamadı.');
      }
    });
  }

  private load(): void {
    if (this.selectedTab() === 'progress') {
      this.loadProgress();
      return;
    }

    if (this.selectedTab() === 'teacher') {
      if (!this.canTeacherAnalytics()) {
        this.error.set('Bu analitik ekranı için ReportView yetkisi gerekir.');
        return;
      }
      if (!this.teacherId.trim()) {
        this.error.set('Öğretmen analitiği için bir öğretmen ID’si girin.');
        return;
      }
      this.loadTeacherAnalytics();
      return;
    }

    if (!this.canPlatformAnalytics()) {
      this.error.set('Bu analitik ekranı için PlatformAnalyticsView yetkisi gerekir.');
      return;
    }

    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set('');
    const range = [this.dateFrom, this.dateTo] as const;
    switch (this.selectedTab()) {
      case 'platform':
        this.loadAnalyticsRequest(this.service.getPlatformUsage(...range), value => this.platformUsage.set(value));
        break;
      case 'content':
        this.loadAnalyticsRequest(this.service.getContentAnalysis(...range), value => this.contentAnalysis.set(value));
        break;
      case 'health':
        this.loadAnalyticsRequest(this.service.getSystemHealth(...range), value => this.systemHealth.set(value));
        break;
      case 'institutions':
        this.loadAnalyticsRequest(this.service.getInstitutionAnalytics(...range), value => this.institutionAnalytics.set(value));
        break;
      case 'programs':
        this.loadAnalyticsRequest(this.service.getProgramAnalytics(), value => this.programAnalytics.set(value));
        break;
    }
  }

  loadTeacherAnalytics(): void {
    if (!this.teacherId.trim()) {
      this.error.set('Geçerli bir öğretmen ID’si girin.');
      return;
    }

    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set('');
    const range = [this.dateFrom, this.dateTo] as const;
    this.request = forkJoin({
      overview: this.service.getTeacherClassOverview(this.teacherId.trim(), ...range),
      assignments: this.service.getTeacherAssignmentAnalytics(this.teacherId.trim(), ...range),
      content: this.service.getTeacherContentAnalysis(this.teacherId.trim(), ...range),
      time: this.service.getTeacherTimeProgress(this.teacherId.trim(), ...range)
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => { this.teacherClassOverview.set(result.overview); this.teacherAssignmentAnalytics.set(result.assignments); this.teacherContentAnalysis.set(result.content); this.teacherTimeProgress.set(result.time); },
      error: () => this.error.set('Öğretmen analitik verisi yüklenemedi; öğretmen kapsam yetkisini ve ID’yi kontrol edin.')
    });
  }

  private loadAnalyticsRequest<T>(request: Observable<T>, apply: (value: T) => void): void {
    this.request = request.pipe(finalize(() => this.loading.set(false))).subscribe({
      next: apply,
      error: () => this.error.set('Hızlı Okuma analitik verisi yüklenemedi.')
    });
  }

  private loadProgress(): void {
    if (!this.canProgress()) {
      this.error.set('Bu ekran için ProgressView yetkisi gerekir.');
      return;
    }

    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set('');
    this.request = this.service.getStudentProgress(this.progressPageNumber, this.progressPageSize, this.progressSearch)
      .pipe(finalize(() => this.loading.set(false))).subscribe({
        next: page => this.progressPage.set(page),
        error: () => this.error.set('Öğrenci ilerlemeleri yüklenemedi.')
      });
  }

  private defaultDate(offsetDays: number): string {
    const date = new Date();
    date.setDate(date.getDate() + offsetDays);
    return date.toISOString().slice(0, 10);
  }
}
