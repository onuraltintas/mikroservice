import { Injectable, signal, computed } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class AdminContextService {
    // Signals for reactive state
    private _institutionId = signal<string | null>(null);
    private _institutionName = signal<string | null>(null);

    private _teacherId = signal<string | null>(null);
    private _teacherName = signal<string | null>(null);

    // Read-only signals
    institutionId = this._institutionId.asReadonly();
    institutionName = this._institutionName.asReadonly();

    teacherId = this._teacherId.asReadonly();
    teacherName = this._teacherName.asReadonly();

    // Computeds
    isImpersonatingInstitution = computed(() => !!this._institutionId());
    isImpersonatingTeacher = computed(() => !!this._teacherId());

    constructor() {
        // Restore state from localStorage if needed? 
        // For now, let's keep it per-session (in memory).
    }

    setInstitutionContext(id: string, name: string) {
        this._institutionId.set(id);
        this._institutionName.set(name);
        // Clearing teacher context when switching to institution
        this.clearTeacherContext();
    }

    clearInstitutionContext() {
        this._institutionId.set(null);
        this._institutionName.set(null);
    }

    setTeacherContext(id: string, name: string) {
        this._teacherId.set(id);
        this._teacherName.set(name);
        // Clearing institution context when switching to teacher?
        // Maybe not, a teacher belongs to an institution. 
        // But for simplicity, let's mutually exclude strictly "Acting as Institution Admin" vs "Acting as Teacher".
        this.clearInstitutionContext();
    }

    clearTeacherContext() {
        this._teacherId.set(null);
        this._teacherName.set(null);
    }

    clearAll() {
        this.clearInstitutionContext();
        this.clearTeacherContext();
    }
}
