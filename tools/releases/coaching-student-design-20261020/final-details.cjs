const fs = require('node:fs');
const root = './source/admin-panel/src/app/features/coaching-portal/pages/';
function edit(file, before, after) {
  const path = root + file;
  const source = fs.readFileSync(path, 'utf8');
  if (!source.includes(before)) throw Error('Expected live source missing: ' + file);
  fs.writeFileSync(path, source.replace(before, after));
}
edit('student-assignment-detail.component.html',
  'rounded-2xl bg-gradient-to-br from-indigo-700 to-violet-700 p-6 text-white shadow-lg sm:p-8',
  'assignment-heading rounded-2xl p-6 sm:p-8');
edit('student-assignment-detail.component.html', "date:'d MMMM y, HH:mm'", "date:'dd.MM.yyyy HH:mm'");
edit('student-assignment-detail.component.html', "item.status === 'Cancelled' ? 'İptal edildi' : record.status", "item.status === 'Cancelled' ? 'İptal edildi' : assignmentStatusLabel(record.status)");
edit('student-assignment-detail.component.ts', "case 'Cancelled': return 'İptal edildi';", "case 'Cancelled': return 'İptal edildi';\n      case 'Assigned': return 'Çalışmanı bekliyor';\n      case 'Submitted': return 'Teslim edildi';\n      case 'Graded': return 'Değerlendirildi';");
edit('student-assignments.component.html', 'role="group" aria-label="Ödev filtresi"', 'role="group" aria-label="Ödev filtresi" aria-describedby="assignment-filter-scope"');
edit('student-assignments.component.html', '[ngClass]="filter() === item.value', '[attr.aria-pressed]="filter() === item.value"\n        [ngClass]="filter() === item.value');
edit('student-assignments.component.html', '<div *ngIf="errorMessage()"', '<p id="assignment-filter-scope" class="text-sm text-slate-500 dark:text-slate-400">Filtreler bu sayfadaki ödevleri gösterir. Diğer kayıtlar için sayfa değiştirebilirsin.</p>\n\n  <div *ngIf="errorMessage()"');
edit('student-assignments.component.html', 'Bu filtrede ödev yok', 'Bu sayfada filtreye uygun ödev yok');
edit('student-assignments.component.html', 'Yeni bir ödev atandığında burada görünecek.', 'Filtreyi değiştirebilir veya diğer sayfalara bakabilirsin.');
console.log('Assignment presentation and accessible filter labels updated.');
