import { Component, inject, signal } from '@angular/core';
import {
  CourseManagementService, CourseReportTable, buildCourseAssignmentProgressReport, buildCourseFinanceSummaryCsv,
  buildCourseFinanceWorkspaceReport, buildCourseLessonAttendanceReport, buildCourseReportCsv, buildCourseReportPrintHtml,
  buildCourseStudentRosterReport, courseReportFileName
} from '../../course-management.service';
import { CoursePanelState, problemText } from '../panel-state';

type ReportId = 'students' | 'attendance' | 'homework' | 'debts' | 'finance';
const REPORTS: { id: ReportId; title: string; text: string; ownerOnly: boolean; print: boolean }[] = [
  { id: 'students', title: 'Öğrenci listesi', text: 'Etkin ve arşivdeki öğrenciler. E-posta ve telefon dosyaya yazılmaz.', ownerOnly: false, print: true },
  { id: 'attendance', title: 'Ders ve yoklama', text: 'Son 7 gün ile önümüzdeki 30 gündeki dersler ve her öğrencinin yoklaması.', ownerOnly: false, print: true },
  { id: 'homework', title: 'Ödev durumu', text: 'Verilen ödevler ve kaç öğrenciye verildiği.', ownerOnly: false, print: true },
  { id: 'debts', title: 'Borç ve paket durumu', text: 'Geciken ve önümüzdeki 30 gündeki taksitler, kalan paket hakları.', ownerOnly: true, print: true },
  { id: 'finance', title: 'Tahsilat özeti', text: 'Toplam borç, tahsilat, bakiye ve yönteme göre tahsilat.', ownerOnly: true, print: false }
];

/** Raporlar: the same tables as before, as CSV (Excel opens it) or a printable page (save as PDF from the print window). */
@Component({
  selector: 'course-reports-section',
  standalone: true,
  styleUrl: '../panel.scss',
  template: `
    <header class="page-head"><div><h1>Raporlar</h1><p>CSV dosyasını Excel açar; yazdırma penceresinden PDF olarak da kaydedebilirsiniz.</p></div></header>
    @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }
    <div class="kpis">
      @for (report of visible(); track report.id) {
        <section class="card card-pad stack" style="gap: 10px">
          <h2>{{ report.title }}</h2>
          <p class="muted" style="margin: 0">{{ report.text }}</p>
          <div class="actions">
            <button type="button" class="btn-secondary btn-small" (click)="csv(report.id)" [disabled]="busy()">CSV indir</button>
            @if (report.print) { <button type="button" class="btn-secondary btn-small" (click)="print(report.id)" [disabled]="busy()">Yazdır</button> }
          </div>
        </section>
      }
    </div>
  `
})
export class ReportsSectionComponent {
  readonly state = inject(CoursePanelState);
  private readonly service = inject(CourseManagementService);
  readonly busy = signal(false);
  readonly error = signal('');
  visible() { return REPORTS.filter(report => this.state.isOwner() || !report.ownerOnly); }

  private async table(id: ReportId): Promise<CourseReportTable> {
    const workspace = this.state.id();
    switch (id) {
      case 'students': return buildCourseStudentRosterReport(await this.service.getStudentRosterForReport(workspace));
      case 'attendance': return buildCourseLessonAttendanceReport(await this.service.getLessonAttendanceForReport(workspace), id => this.state.teacherName(id));
      case 'homework': return buildCourseAssignmentProgressReport(await this.service.getAssignmentProgressForReport(workspace),
        groupId => this.state.groups().find(group => group.id === groupId)?.name ?? '', id => this.state.teacherName(id));
      default: return buildCourseFinanceWorkspaceReport(await this.service.getFinanceWorkspaceReport(workspace));
    }
  }

  private download(content: string, name: string): void {
    const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `${name}.csv`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  async csv(id: ReportId): Promise<void> {
    this.busy.set(true); this.error.set('');
    try {
      const name = this.state.workspace()?.name ?? 'kurs';
      if (id === 'finance') this.download(buildCourseFinanceSummaryCsv(await this.service.getFinanceSummary(this.state.id())), courseReportFileName('Tahsilat özeti'));
      else {
        const table = await this.table(id);
        this.download(buildCourseReportCsv(name, table), courseReportFileName(table.title));
      }
    } catch (error) { this.error.set(problemText(error, 'Rapor hazırlanamadı.')); }
    finally { this.busy.set(false); }
  }

  async print(id: ReportId): Promise<void> {
    const printWindow = window.open('', '_blank');
    if (!printWindow) { this.error.set('Yazdırma penceresi açılamadı. Tarayıcının açılır pencere ayarını kontrol edin.'); return; }
    printWindow.opener = null;
    this.busy.set(true); this.error.set('');
    try {
      printWindow.document.open();
      printWindow.document.write(buildCourseReportPrintHtml(this.state.workspace()?.name ?? 'Kurs', await this.table(id)));
      printWindow.document.close();
      printWindow.focus();
      setTimeout(() => printWindow.print(), 100);
    } catch (error) { printWindow.close(); this.error.set(problemText(error, 'Rapor hazırlanamadı.')); }
    finally { this.busy.set(false); }
  }
}
