import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingPracticeAdaptationService } from './coaching-practice-adaptation.service';
import { environment } from '../../../environments/environment';

describe('CoachingPracticeAdaptationService', () => {
  it('uses the student-scoped teacher endpoints and sends confirmation plus fingerprint', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingPracticeAdaptationService), http = TestBed.inject(HttpTestingController);
    const root = environment.apiUrl + '/coaching/staff/students/student/study';
    service.get('student').subscribe(); http.expectOne(root + '/practice-analysis').flush({ data: { exams: [], advice: [] } });
    const request = { examCode: 'AYT_SAY', source: 'TeacherRecorded', endDate: '2026-11-03', allowDeadlineExtension: false, topics: [{ topicId: 't', minutes: 30 }] };
    service.preview('student', request).subscribe(); const preview = http.expectOne(root + '/practice-adjustment/preview'); expect(preview.request.body).toEqual(request); preview.flush({ data: {} });
    service.apply('student', 'fingerprint', request, true).subscribe(); const apply = http.expectOne(root + '/practice-adjustment/apply');
    expect(apply.request.body).toEqual({ fingerprint: 'fingerprint', preview: request, confirmed: true }); apply.flush({ data: {} }); http.verify();
  });
});
