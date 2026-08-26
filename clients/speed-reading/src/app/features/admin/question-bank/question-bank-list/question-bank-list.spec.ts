import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideToastr } from 'ngx-toastr';

import { QuestionBankListComponent } from './question-bank-list';

describe('QuestionBankListComponent', () => {
  let component: QuestionBankListComponent;
  let fixture: ComponentFixture<QuestionBankListComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuestionBankListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideToastr()]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QuestionBankListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    http = TestBed.inject(HttpTestingController);
    const request = http.expectOne('/api/speed-reading/exam-questions?pageNumber=1&pageSize=10');
    request.flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 10 });
  });

  afterEach(() => http.verify());

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
