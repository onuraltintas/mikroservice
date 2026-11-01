// Test doubles for KM7a's embedded components (same selectors, inputs and outputs). Specs of Müfredat Yönetimi and the
// Katalog page swap them in with TestBed.overrideComponent so the real editors' HTTP calls stay out of those specs.
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CoachingExamSessionsEditorComponent } from './coaching-exam-sessions-editor';
import { CoachingOutcomeSourceEditorComponent } from './coaching-outcome-source-editor';
import { CoachingTargetProgramsComponent } from './coaching-target-programs';
import { CoachingYksScopeEditorComponent } from './coaching-yks-scope-editor';

@Component({ selector: 'app-yks-scope-editor', standalone: true, template: `<p class="embed-pending">YKS kapsamı düzenleyicisi yükleniyor.</p>` })
export class YksScopeEditorStub { @Input() topicId = ''; @Output() saved = new EventEmitter<void>(); }

@Component({ selector: 'app-outcome-source-editor', standalone: true, template: `<p class="embed-pending">Kaynak düzenleyicisi yükleniyor.</p>` })
export class OutcomeSourceEditorStub { @Input() outcomeId = ''; @Output() saved = new EventEmitter<void>(); }

export interface ExamSessionModel { id: string | null; date: string; label: string; applicationStart: string; applicationEnd: string }
@Component({ selector: 'app-exam-sessions-editor', standalone: true, template: `<p class="embed-pending">Oturum düzenleyicisi yükleniyor.</p>` })
export class ExamSessionsEditorStub {
  @Input() sessions: ExamSessionModel[] = []; @Output() sessionsChange = new EventEmitter<ExamSessionModel[]>(); @Output() validChange = new EventEmitter<boolean>();
}

@Component({ selector: 'app-coaching-target-programs', standalone: true, template: `<p class="embed-pending">Üniversite programları yükleniyor.</p>` })
export class TargetProgramsStub {}

export const KM7A_REAL = [CoachingExamSessionsEditorComponent, CoachingYksScopeEditorComponent, CoachingOutcomeSourceEditorComponent];
export const KM7A_STUBS = [ExamSessionsEditorStub, YksScopeEditorStub, OutcomeSourceEditorStub];
export const TARGET_PROGRAMS_REAL = CoachingTargetProgramsComponent;
export const TARGET_PROGRAMS_STUB = TargetProgramsStub;
