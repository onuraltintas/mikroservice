import { Component } from '@angular/core';
import { StudentShellComponent } from './shell/student-shell.component';

@Component({
  selector: 'app-student-layout',
  standalone: true,
  imports: [StudentShellComponent],
  template: `<app-student-shell />`
})
export class StudentLayoutComponent {}
