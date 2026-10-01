import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { StudentShellComponent } from './student-shell.component';

describe('Staff training shell menu', () => {
  it('uses the training navigation rather than the preview-only menu', () => {
    const items = [{ label: 'Eğitim Programları', route: '/student/training-programs' }];
    const shell = {
      authService: { hasRole: (role: string) => role === 'Admin', canPreviewExercises: () => true },
      navService: { getStudentMenuItems: () => items },
      menuItems: signal([]), isTeacherPreview: signal(false),
      router: { events: new Subject() }, destroy$: new Subject()
    };
    StudentShellComponent.prototype.ngOnInit.call(shell as any);
    expect(shell.menuItems()).toEqual(items as any);
    expect(shell.isTeacherPreview()).toBeFalse();
  });
  it('preserves the personal student menu for dual admin/student accounts', () => {
    const items = [{ label: 'Ana Sayfa', route: '/student/dashboard' }];
    const shell = {
      authService: { hasRole: (role: string) => ['Admin', 'Student'].includes(role), canPreviewExercises: () => false },
      navService: { getStudentMenuItems: () => items },
      menuItems: signal([]), isTeacherPreview: signal(false),
      router: { events: new Subject() }, destroy$: new Subject()
    };
    StudentShellComponent.prototype.ngOnInit.call(shell as any);
    expect(shell.menuItems()).toEqual(items as any);
  });
});
