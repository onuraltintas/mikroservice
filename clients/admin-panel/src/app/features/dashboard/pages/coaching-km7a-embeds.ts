// Integration point for the standalone components KM7a ships (YKS scope, outcome sources, exam sessions, university
// programs and the ÖABT exam grouping). Müfredat Yönetimi and the Katalog page import them only through this file.
import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface ExamOption { code: string; name: string; id?: string; isActive?: boolean }
export interface ExamOptionGroup { label: string | null; exams: ExamOption[] }

/** Groups OABT_* exams under "ÖABT" for a select; other exams stay ungrouped in their original order. */
export function groupExamsForSelect<T extends ExamOption>(exams: T[]): { label: string | null; exams: T[] }[] {
  const plain = exams.filter(x => !x.code.startsWith('OABT_'));
  const oabt = exams.filter(x => x.code.startsWith('OABT_'));
  return [{ label: null, exams: plain }, ...(oabt.length ? [{ label: 'ÖABT', exams: oabt }] : [])];
}

@Component({ selector: 'app-yks-scope-editor', standalone: true, template: `<p class="embed-pending">YKS kapsamı düzenleyicisi yükleniyor.</p>` })
export class CoachingYksScopeEditorComponent { @Input() topicId = ''; @Output() saved = new EventEmitter<void>(); }

@Component({ selector: 'app-outcome-source-editor', standalone: true, template: `<p class="embed-pending">Kaynak düzenleyicisi yükleniyor.</p>` })
export class CoachingOutcomeSourceEditorComponent { @Input() outcomeId = ''; @Output() saved = new EventEmitter<void>(); }

export interface ExamSessionModel { id: string | null; date: string; label: string; applicationStart: string; applicationEnd: string }
@Component({ selector: 'app-exam-sessions-editor', standalone: true, template: `<p class="embed-pending">Oturum düzenleyicisi yükleniyor.</p>` })
export class CoachingExamSessionsEditorComponent {
  @Input() sessions: ExamSessionModel[] = []; @Output() sessionsChange = new EventEmitter<ExamSessionModel[]>(); @Output() valid = new EventEmitter<boolean>();
}

@Component({ selector: 'app-coaching-target-programs', standalone: true, template: `<p class="embed-pending">Üniversite programları yükleniyor.</p>` })
export class CoachingTargetProgramsComponent {}
