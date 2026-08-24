import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { QuestionBankRoutingModule } from './question-bank-routing-module';
import { QuestionBankListComponent } from './question-bank-list/question-bank-list';
import { QuestionFormComponent } from './question-form/question-form';


@NgModule({
  imports: [
    CommonModule,
    QuestionBankRoutingModule,
    QuestionBankListComponent,
    QuestionFormComponent
  ]
})
export class QuestionBankModule { }
