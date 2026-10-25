const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(process.argv[2] || 'clients/admin-panel');
const ts = require(path.join(root, 'node_modules/typescript'));
const postcss = require(path.join(root, 'node_modules/postcss'));
const base = 'projects/staff-portal/src/app/';
const teacher = base + 'features/coaching/';
const feedbackPath = '../../../../../../src/app/features/coaching-portal/coaching-feedback';
function read(file) { return fs.readFileSync(path.join(root, file), 'utf8'); }
function write(file, text) { fs.writeFileSync(path.join(root, file), text); }
function edit(file, before, after) {
  const text = read(file);
  if (!text.includes(before)) throw Error('Expected source missing: ' + file + ' / ' + before.slice(0, 65));
  write(file, text.replace(before, after));
}

// Use the student token contract only while the coaching teacher workspace is active.
edit(base + 'app.component.ts', 'Component, ViewChild, inject', 'Component, ElementRef, HostListener, ViewChild, inject, signal');
edit(base + 'app.component.ts', "import { DOCUMENT }", "import { CdkTrapFocus } from '@angular/cdk/a11y';\nimport { DarkModeService } from '../../../../src/app/core/services/dark-mode.service';\nimport { CoachingIconComponent } from '../../../../src/app/features/coaching-portal/coaching-icon.component';\nimport { DOCUMENT }");
edit(base + 'app.component.ts', 'imports: [FormsModule,', 'imports: [FormsModule, CdkTrapFocus, CoachingIconComponent,');
edit(base + 'app.component.ts', '@ViewChild(CoachingTeacherWorkspaceComponent)', "@ViewChild('coachingTeacherWorkspace')");
edit(base + 'app.component.html', '<staff-coaching-teacher-workspace />', '<staff-coaching-teacher-workspace #coachingTeacherWorkspace />');
edit(base + 'app.component.ts', 'readonly auth = inject(StaffAuthService);', `readonly auth = inject(StaffAuthService);
  readonly theme = inject(DarkModeService);
  readonly menuOpen = signal(false);
  @ViewChild('teacherMenuToggle') private teacherMenuToggle?: ElementRef<HTMLButtonElement>;
  closeTeacherMenu(): void { this.menuOpen.set(false); this.teacherMenuToggle?.nativeElement.focus(); }
  @HostListener('document:keydown.escape') closeTeacherMenuOnEscape(): void { if (this.menuOpen()) this.closeTeacherMenu(); }
  async chooseProductFromMenu(product: StaffProduct): Promise<void> { this.closeTeacherMenu(); await this.switchProduct(product); }`);
edit(base + 'app.component.html', '<div class="portal-shell">', `<div class="portal-shell" [class.coaching-shell]="isTeacherWorkspaceSelected" [class.coaching-staff-shell]="isTeacherWorkspaceSelected" [class.menu-open]="menuOpen()">
    @if (isTeacherWorkspaceSelected) { <a class="teacher-skip" href="#teacher-main">İçeriğe geç</a> }
    @if (isTeacherWorkspaceSelected && menuOpen()) { <button class="teacher-scrim" type="button" aria-label="Menüyü kapat" (click)="closeTeacherMenu()"></button> }`);
edit(base + 'app.component.html', '<aside class="sidebar" aria-label="Personel portalı">', '<aside class="sidebar" aria-label="Personel portalı" [attr.role]="isTeacherWorkspaceSelected && menuOpen() ? \'dialog\' : null" [attr.aria-modal]="isTeacherWorkspaceSelected && menuOpen() ? \'true\' : null" [cdkTrapFocus]="isTeacherWorkspaceSelected && menuOpen()" [cdkTrapFocusAutoCapture]="isTeacherWorkspaceSelected && menuOpen()">\n      @if (isTeacherWorkspaceSelected && menuOpen()) { <button class="teacher-drawer-close" type="button" aria-label="Menüyü kapat" (click)="closeTeacherMenu()"><app-coaching-icon name="close" /></button> }');
edit(base + 'app.component.html', '(click)="switchProduct(access.product)"', '(click)="chooseProductFromMenu(access.product)"');
edit(base + 'app.component.html', '<main class="main-area">', '<main class="main-area" [class.coaching-body]="isTeacherWorkspaceSelected" id="teacher-main" tabindex="-1">');
edit(base + 'app.component.html', '<header class="topbar">', `<header class="topbar">
        @if (isTeacherWorkspaceSelected) { <button #teacherMenuToggle class="teacher-menu-toggle" type="button" aria-label="Personel menüsünü aç" [attr.aria-expanded]="menuOpen()" (click)="menuOpen.set(!menuOpen())"><app-coaching-icon name="menu" /></button> }`);
edit(base + 'app.component.html', '<div class="topbar-actions">', `<div class="topbar-actions">
          @if (isTeacherWorkspaceSelected) { <button class="teacher-theme-toggle" type="button" [attr.aria-label]="theme.isDarkMode() ? 'Açık temaya geç' : 'Koyu temaya geç'" (click)="theme.toggle()"><app-coaching-icon [name]="theme.isDarkMode() ? 'sun' : 'moon'" /></button> }`);
edit(base + 'app.component.html', '<div class="content-wrap">', '<div class="content-wrap" [class.coaching-content]="isTeacherWorkspaceSelected">');
edit(base + 'app.component.html', "{{ activeWorkspaceRole === 'institution' ? 'Kurumunuz için tek çalışma alanı.' : 'Öğrencileriniz için tek çalışma alanı.' }}", "{{ isTeacherWorkspaceSelected ? 'Birlikte, adım adım ilerleyelim.' : activeWorkspaceRole === 'institution' ? 'Kurumunuz için tek çalışma alanı.' : 'Öğrencileriniz için tek çalışma alanı.' }}");
edit(base + 'app.component.html', '{{ currentProductLabel }} erişiminiz, bu üründeki rolünüze göre sınırlandırılmıştır.', "@if (isTeacherWorkspaceSelected) { Öğrencilerinin çalışmalarını takip et, geri bildirim ver ve bir sonraki adımı birlikte planla. } @else { {{ currentProductLabel }} erişiminiz, bu üründeki rolünüze göre sınırlandırılmıştır.");
edit(base + 'app.component.html', 'Diğer ürünler ayrı yetkiyle açılır.', 'Diğer ürünler ayrı yetkiyle açılır. }');
edit(base + 'app.component.html', '<section class="session-panel"', '@if (!isTeacherWorkspaceSelected) { <section class="session-panel"');
edit(base + 'app.component.html', '@if (accountSettingsVisible', '}\n        @if (accountSettingsVisible');

// Replace the remaining native confirms and await the decision in every caller.
edit(teacher + 'coaching-teacher-assignment-detail.component.ts', 'canLeavePage(): boolean {', 'async canLeavePage(): Promise<boolean> {');
edit(teacher + 'coaching-teacher-assignment-detail.component.ts', 'window.confirm(', 'await this.toaster.confirm(');
edit(teacher + 'coaching-teacher-assignment-detail.component.ts', 'export class CoachingTeacherAssignmentDetailComponent implements OnInit {', 'export class CoachingTeacherAssignmentDetailComponent implements OnInit {\n  private readonly toaster = coachingToaster();');
edit(teacher + 'coaching-teacher-student-profile.component.ts', 'canLeavePage(){return !this.dirty||window.confirm(', 'async canLeavePage(){return !this.dirty||await this.toaster.confirm(');
edit(teacher + 'coaching-teacher-student-profile.component.ts', 'load(){if(this.busy()||!this.canLeavePage())return;', 'async load(){if(this.busy()||this.dirty&&!await this.canLeavePage())return;');
edit(teacher + 'coaching-teacher-student-profile.component.ts', 'export class CoachingTeacherStudentProfileComponent implements OnInit {', 'export class CoachingTeacherStudentProfileComponent implements OnInit {\n private readonly toaster=coachingToaster();');
edit(teacher + 'coaching-teacher-assignments.component.ts', 'canLeavePage(): boolean {', 'async canLeavePage(): Promise<boolean> {');
edit(teacher + 'coaching-teacher-assignments.component.ts', 'reviewAssignment(assignmentId: string): boolean {', 'async reviewAssignment(assignmentId: string): Promise<boolean> {');
edit(teacher + 'coaching-teacher-assignments.component.ts', 'if (!this.canLeavePage()) return false;', 'if ((this.detail || this.formMode() || this.isCancelling()) && !await this.canLeavePage()) return false;');
edit(teacher + 'coaching-teacher-assignments.component.ts', 'closeAssignmentDetail(): void {', 'async closeAssignmentDetail(): Promise<void> {');
edit(teacher + 'coaching-teacher-assignments.component.ts', 'if (!(this.detail?.canLeavePage() ?? true)) return;', 'if (!(await this.detail?.canLeavePage() ?? true)) return;');

// Reuse the central feedback service. Business messages and field errors stay visible.
for (const name of [
  ...fs.readdirSync(path.join(root, teacher)).filter(f => /^coaching-teacher.*\.component\.ts$/.test(f)).map(f => teacher + f),
  teacher+'coaching-staff-study.component.ts',
  base+'account/staff-account-settings.component.ts', base+'notifications/staff-notification-center.component.ts'
]) {
  let source = read(name);
  const ast = ts.createSourceFile(name, source, ts.ScriptTarget.Latest, true);
  const cls = ast.statements.find(ts.isClassDeclaration);
  if (!cls) continue;
  const properties = cls.members.filter(m => ts.isPropertyDeclaration(m) && m.initializer && ts.isCallExpression(m.initializer) && m.initializer.expression.getText(ast) === 'signal' && /error|success/i.test(m.name.getText(ast)));
  const additions = properties.map(m => {const n=m.name.getText(ast);return `\n  private readonly ${n}Feedback = coachingFeedback(${/success/i.test(n)?'this.'+n:'undefined, this.'+n});`;}).join('');
  source = source.slice(0,cls.end-1) + additions + '\n' + source.slice(cls.end-1);
  const usesConfirm = source.includes('coachingToaster()');
  if (properties.length || usesConfirm) source = `import { coachingFeedback${usesConfirm?', coachingToaster':''} } from '${name.startsWith(teacher)?feedbackPath:feedbackPath.slice(3)}';\n` + source;
  write(name, source);
}

function themeCss(text) {
  const ast = postcss.parse(text);
  ast.walkDecls(d => {
    const selector = d.parent.selector ?? '';
    const error = /error|danger|overdue|negative/i.test(selector);
    const success = /success|positive|\.paid\b|\.completed\b/i.test(selector);
    const warning = /warning|\.late\b|pending|\.status\b/i.test(selector);
    if (d.prop === 'font-family') d.value = 'inherit';
    if (d.prop === 'font' && /Georgia/.test(d.value)) d.value = d.value.replace(/Georgia[^;]*/, 'var(--ui-font-body)');
    if (d.prop === 'font-size' && /^\d+(\.\d+)?px$/.test(d.value) && parseFloat(d.value) < 13) d.value = '13px';
    if (d.prop === 'color' && /^(#[\da-f]{3,8}|white|black)$/i.test(d.value)) {
      if (/white|#fff(f|$)/i.test(d.value)) return;
      d.value = error ? 'var(--ui-danger)' : success ? 'var(--ui-success)' : warning ? 'var(--coaching-warning)' : /muted|description|note|eyebrow|small|hint|label|\bp\b|context|caption|secondary/i.test(selector) ? 'var(--ui-text-muted)' : 'var(--ui-text)';
    }
    if (/^background(-color)?$/.test(d.prop) && /^(#[\da-f]{3,8}|white)$/i.test(d.value)) {
      const hex=d.value.toLowerCase(), light=hex==='white'||parseInt(hex.slice(1,3).padEnd(2,hex[1]),16)>170;
      d.value = error ? 'var(--coaching-danger-soft)' : success ? 'var(--ui-brand-soft)' : warning ? 'var(--coaching-warm)' : light ? /thead|filters|summary|empty|secondary|muted|selected/i.test(selector) ? 'var(--ui-surface-muted)' : 'var(--ui-surface)' : 'var(--ui-brand)';
    }
    if (/^border(-.*)?$/.test(d.prop)) d.value = d.value.replace(/#[\da-f]{3,8}\b/gi, 'var(--ui-border)');
  });
  return require('./finish-styles.cjs')(ast);
}
for (const file of fs.readdirSync(path.join(root, teacher)).filter(f => /^coaching-teacher.*\.scss$/.test(f))) write(teacher + file, themeCss(read(teacher + file)));
// The subscription screen keeps its stylesheet in the component metadata.
const subscription=teacher+'coaching-teacher-subscription.component.ts';
write(subscription, read(subscription).replace(/styles: \[`([\s\S]*?)`\]/, (_,css)=>'styles: [`'+themeCss(css)+'`]'));
console.log('Teacher theme bindings, central feedback and awaited confirmations applied.');

// Human-facing dates and labels follow the same readable format as student screens.
for (const file of fs.readdirSync(path.join(root, teacher)).filter(f => /^coaching-teacher.*\.html$/.test(f))) {
  const name=teacher+file;
  write(name,read(name).replaceAll("date:'d MMM y, HH:mm'","date:'dd.MM.yyyy HH:mm'").replaceAll("date:'d MMMM y, HH:mm'","date:'dd.MM.yyyy HH:mm'").replaceAll("date:'d MMM y'","date:'dd.MM.yyyy'"));
}
edit(teacher+'coaching-teacher-assignments.component.html','{{ assignment.type }}',"{{ assignment.type === 'Individual' ? 'Bireysel' : assignment.type === 'Group' ? 'Grup' : assignment.type }}");
edit(teacher+'coaching-teacher-assignment-detail.component.html','{{ item.source }}',"{{ item.source === 'Digital' ? 'Dijital' : item.source === 'Book' ? 'Kitap' : item.source === 'Mixed' ? 'Kitap ve dijital' : item.source }}");
const notificationTemplate=base+'notifications/staff-notification-center.component.html';
write(notificationTemplate,read(notificationTemplate).replaceAll("date:'d MMM y, HH:mm'","date:'dd.MM.yyyy HH:mm'"));
