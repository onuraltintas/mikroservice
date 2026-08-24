import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideToastr } from 'ngx-toastr';

import { QuestionBankListComponent } from './question-bank-list';

describe('QuestionBankListComponent', () => {
  let component: QuestionBankListComponent;
  let fixture: ComponentFixture<QuestionBankListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuestionBankListComponent],
      providers: [provideHttpClient(), provideToastr()]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QuestionBankListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
