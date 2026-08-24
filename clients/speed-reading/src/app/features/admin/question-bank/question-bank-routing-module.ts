import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { QuestionBankListComponent } from './question-bank-list/question-bank-list';
import { QuestionFormComponent } from './question-form/question-form';

const routes: Routes = [
  { path: '', component: QuestionBankListComponent },
  { path: 'new', component: QuestionFormComponent },
  { path: 'edit/:id', component: QuestionFormComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class QuestionBankRoutingModule { }
