import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingAdminService } from './speed-reading-admin.service';

describe('SpeedReadingAdminService', () => {
  let service: SpeedReadingAdminService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SpeedReadingAdminService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(SpeedReadingAdminService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads capabilities through the Gateway speed-reading route', () => {
    const response = {
      mode: 'Standalone',
      coachingIntegrationEnabled: false,
      notificationIntegrationEnabled: false,
      subscriptionIntegrationEnabled: false
    };

    service.getCapabilities().subscribe(value => expect(value).toEqual(response));

    const request = http.expectOne('/api/speed-reading/capabilities');
    expect(request.request.method).toBe('GET');
    request.flush(response);
  });

  it('loads platform analytics with an explicit date range', () => {
    service.getPlatformUsage('2026-08-01', '2026-08-31').subscribe();

    const request = http.expectOne('/api/speed-reading/analytics/admin/platform-usage?dateFrom=2026-08-01&dateTo=2026-08-31');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });

  it('loads content, health, institution and program analytics from admin contracts', () => {
    service.getContentAnalysis('2026-08-01', '2026-08-31').subscribe();
    expect(http.expectOne('/api/speed-reading/analytics/admin/content-analysis?dateFrom=2026-08-01&dateTo=2026-08-31').request.method).toBe('GET');

    service.getSystemHealth('2026-08-01', '2026-08-31').subscribe();
    expect(http.expectOne('/api/speed-reading/analytics/admin/system-health?dateFrom=2026-08-01&dateTo=2026-08-31').request.method).toBe('GET');

    service.getInstitutionAnalytics('2026-08-01', '2026-08-31').subscribe();
    expect(http.expectOne('/api/speed-reading/analytics/admin/institutions?dateFrom=2026-08-01&dateTo=2026-08-31').request.method).toBe('GET');

    service.getProgramAnalytics().subscribe();
    expect(http.expectOne('/api/speed-reading/analytics/admin/programs').request.method).toBe('GET');
  });

  it('loads teacher analytics through the scoped admin contracts', () => {
    service.getTeacherClassOverview('teacher-1', '2026-08-01', '2026-08-31').subscribe();
    expect(http.expectOne('/api/speed-reading/analytics/admin/teachers/teacher-1/class-overview?dateFrom=2026-08-01&dateTo=2026-08-31').request.method).toBe('GET');

    service.getTeacherAssignmentAnalytics('teacher-1', '2026-08-01', '2026-08-31').subscribe();
    expect(http.expectOne('/api/speed-reading/analytics/admin/teachers/teacher-1/assignments?dateFrom=2026-08-01&dateTo=2026-08-31').request.method).toBe('GET');

    service.getTeacherContentAnalysis('teacher-1', '2026-08-01', '2026-08-31').subscribe();
    expect(http.expectOne('/api/speed-reading/analytics/admin/teachers/teacher-1/content-analysis?dateFrom=2026-08-01&dateTo=2026-08-31').request.method).toBe('GET');

    service.getTeacherTimeProgress('teacher-1', '2026-08-01', '2026-08-31').subscribe();
    expect(http.expectOne('/api/speed-reading/analytics/admin/teachers/teacher-1/time-progress?dateFrom=2026-08-01&dateTo=2026-08-31').request.method).toBe('GET');
  });

  it('lists and opens student program progress through the report contract', () => {
    service.getStudentProgress(2, 25, 'Ada').subscribe();
    const listRequest = http.expectOne('/api/speed-reading/student-progress?pageNumber=2&pageSize=25&searchTerm=Ada');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({ items: [], pageNumber: 2, pageSize: 25, totalCount: 0 });

    service.getStudentProgressDetails('progress-1').subscribe();
    const detailsRequest = http.expectOne('/api/speed-reading/student-progress/progress-1');
    expect(detailsRequest.request.method).toBe('GET');
    detailsRequest.flush({});
  });

  it('loads subscription products, plans, subscriptions and payment history', () => {
    service.getSubscriptionProducts().subscribe(value => expect(value).toEqual([]));
    const productsRequest = http.expectOne('/api/speed-reading/products/all');
    expect(productsRequest.request.method).toBe('GET');
    productsRequest.flush({ success: true, data: [] });

    service.getSubscriptionPlans().subscribe(value => expect(value).toEqual([]));
    const plansRequest = http.expectOne('/api/speed-reading/subscription-plans/all');
    expect(plansRequest.request.method).toBe('GET');
    plansRequest.flush({ success: true, data: [] });

    service.getUserSubscriptions(2, 25, 'active', 'Ada').subscribe(value => expect(value.totalCount).toBe(0));
    const subscriptionsRequest = http.expectOne('/api/speed-reading/subscriptions?page=2&pageSize=25&status=active&search=Ada');
    expect(subscriptionsRequest.request.method).toBe('GET');
    subscriptionsRequest.flush({ success: true, data: { items: [], totalCount: 0, page: 2, pageSize: 25 } });

    service.getPaymentHistory(2, 25, 'success', 'Ada').subscribe(value => expect(value.totalCount).toBe(0));
    const paymentsRequest = http.expectOne('/api/speed-reading/payment?page=2&pageSize=25&status=success&search=Ada');
    expect(paymentsRequest.request.method).toBe('GET');
    paymentsRequest.flush({ items: [], total: 0, pageNumber: 2, pageSize: 25 });
  });

  it('writes product and subscription plan changes through ContentManage routes', () => {
    service.createSubscriptionProduct({
      slug: 'hizliokuma', name: 'Hızlı Okuma', description: 'Ürün',
      includedProductSlugs: [], isActive: true, isPublic: true, sortOrder: 1
    }).subscribe();
    const productCreate = http.expectOne('/api/speed-reading/products');
    expect(productCreate.request.method).toBe('POST');
    productCreate.flush({ success: true, data: {} });

    service.updateSubscriptionPlan('plan-1', { isActive: false }).subscribe();
    const planUpdate = http.expectOne('/api/speed-reading/subscription-plans/plan-1');
    expect(planUpdate.request.method).toBe('PUT');
    planUpdate.flush({ success: true, data: {} });
  });

  it('loads and manages age-group configurations through SettingsManage routes', () => {
    service.getAgeGroups().subscribe(value => expect(value).toEqual([]));
    const listRequest = http.expectOne('/api/speed-reading/age-group-configurations?activeOnly=false');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);

    const request = {
      name: 'child',
      displayName: 'Çocuk',
      minAge: 7,
      maxAge: 12,
      minWpm: 80,
      recommendedWpm: 120,
      maxWpm: 180,
      recommendedComprehension: 70,
      recommendedDailyMinutes: 15,
      defaultDifficultyLevel: 1,
      orderIndex: 1,
      isActive: true,
      description: 'Çocuk grubu'
    };

    service.createAgeGroup(request).subscribe();
    const createRequest = http.expectOne('/api/speed-reading/age-group-configurations');
    expect(createRequest.request.method).toBe('POST');
    createRequest.flush({ id: 'age-1' });

    service.updateAgeGroup('age-1', request).subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/age-group-configurations/age-1');
    expect(updateRequest.request.method).toBe('PUT');
    updateRequest.flush(null);

    service.deleteAgeGroup('age-1').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/age-group-configurations/age-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    deleteRequest.flush(null);
  });

  it('loads and manages assessment templates by age group', () => {
    service.getAssessmentTemplates().subscribe(value => expect(value).toEqual([]));
    const listRequest = http.expectOne('/api/speed-reading/admin/assessment-templates');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);

    service.getAssessmentTemplateByAgeGroup('age-1').subscribe();
    const detailRequest = http.expectOne('/api/speed-reading/admin/assessment-templates/age-group/age-1');
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush({ id: 'template-1' });

    const request = {
      name: 'Çocuk seviye tespit',
      targetAgeGroupId: 'age-1',
      exercises: [{ exerciseId: 'exercise-1', customTitle: 'Odak', customDescription: null, displayOrder: 1 }]
    };
    service.createAssessmentTemplate(request).subscribe();
    const createRequest = http.expectOne('/api/speed-reading/admin/assessment-templates');
    expect(createRequest.request.method).toBe('POST');
    createRequest.flush('template-1');

    service.updateAssessmentTemplate('template-1', { name: request.name, exercises: request.exercises }).subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/admin/assessment-templates/template-1');
    expect(updateRequest.request.method).toBe('PUT');
    updateRequest.flush(null);

    service.deleteAssessmentTemplate('template-1').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/admin/assessment-templates/template-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    deleteRequest.flush(null);
  });

  it('lists and manages visualization scenes, questions and CSV imports', () => {
    service.getVisualizationScenes(2, 25, 3, 'orman').subscribe(value => expect(value.totalCount).toBe(0));
    const listRequest = http.expectOne('/api/speed-reading/admin/visualization-scenes?pageNumber=2&pageSize=25&difficultyLevel=3&searchTerm=orman');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 25 });

    service.getVisualizationExercises().subscribe(value => expect(value).toEqual([]));
    const exercisesRequest = http.expectOne('/api/speed-reading/admin/visualization-scenes/exercises');
    expect(exercisesRequest.request.method).toBe('GET');
    exercisesRequest.flush([]);

    service.getVisualizationScene('scene-1').subscribe();
    const detailRequest = http.expectOne('/api/speed-reading/admin/visualization-scenes/scene-1');
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush({ id: 'scene-1', questions: [] });

    const request = {
      exerciseId: 'exercise-1', description: 'Bir orman sahnesi', imageUrl: null,
      duration: 30, displayOrder: 1, difficultyLevel: 2, questions: [],
      targetAgeGroupConfigurationId: null
    };
    service.createVisualizationScene(request).subscribe();
    const createRequest = http.expectOne('/api/speed-reading/admin/visualization-scenes');
    expect(createRequest.request.method).toBe('POST');
    createRequest.flush('scene-1');

    service.updateVisualizationScene('scene-1', request).subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/admin/visualization-scenes/scene-1');
    expect(updateRequest.request.method).toBe('PUT');
    updateRequest.flush(null);

    service.deleteVisualizationScene('scene-1').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/admin/visualization-scenes/scene-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    deleteRequest.flush(null);

    service.importVisualizationCsv(new File(['ExerciseId,Description'], 'scenes.csv', { type: 'text/csv' })).subscribe();
    const importRequest = http.expectOne('/api/speed-reading/admin/visualization-scenes/import/csv');
    expect(importRequest.request.method).toBe('POST');
    expect(importRequest.request.body instanceof FormData).toBe(true);
    importRequest.flush({ successCount: 1, failedCount: 0, message: 'Imported', errors: [] });
  });

  it('lists and manages the exam question bank with filters', () => {
    service.getExamQuestions(2, 25, 1, 3, 4, 'ana fikir', 'age-1').subscribe(value => expect(value.totalCount).toBe(0));
    const listRequest = http.expectOne('/api/speed-reading/exam-questions?pageNumber=2&pageSize=25&examType=1&difficulty=3&category=4&searchTerm=ana%20fikir&ageGroupId=age-1');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 25, totalPages: 0 });

    const request = {
      content: 'Kısa bir metin', question: 'Ana fikir nedir?', optionA: 'A', optionB: 'B',
      optionC: 'C', optionD: 'D', optionE: null, correctOption: 'A', examType: 1,
      difficulty: 2, wordCount: 3, topic: 'Okuma', category: 1, targetAgeGroupId: 'age-1'
    };
    service.createExamQuestion(request).subscribe();
    const createRequest = http.expectOne('/api/speed-reading/exam-questions');
    expect(createRequest.request.method).toBe('POST');
    createRequest.flush('question-1');

    service.updateExamQuestion('question-1', request).subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/exam-questions/question-1');
    expect(updateRequest.request.method).toBe('PUT');
    updateRequest.flush(null);

    service.deleteExamQuestion('question-1').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/exam-questions/question-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    deleteRequest.flush(null);
  });

  it('lists and manages vocabulary with CSV import/export', () => {
    service.getVocabulary('kitap', 'Genel', 2, 'age-1', 2, 25).subscribe(value => expect(value.totalCount).toBe(0));
    const listRequest = http.expectOne('/api/speed-reading/vocabulary?pageNumber=2&pageSize=25&search=kitap&category=Genel&difficultyLevel=2&ageGroupId=age-1');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 25, totalPages: 0 });

    service.getVocabularyCategories().subscribe(value => expect(value).toEqual([]));
    const categoriesRequest = http.expectOne('/api/speed-reading/vocabulary/categories');
    expect(categoriesRequest.request.method).toBe('GET');
    categoriesRequest.flush([]);

    const request = {
      word: 'kitap', definition: 'Okuma nesnesi', exampleSentence: 'Kitap okudum.',
      synonyms: 'eser', antonyms: null, category: 'Genel', difficultyLevel: 1, targetAgeGroupId: null
    };
    service.createVocabularyItem(request).subscribe();
    const createRequest = http.expectOne('/api/speed-reading/vocabulary');
    expect(createRequest.request.method).toBe('POST');
    createRequest.flush({ id: 'word-1' });

    service.updateVocabularyItem('word-1', request).subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/vocabulary/word-1');
    expect(updateRequest.request.method).toBe('PUT');
    updateRequest.flush({ id: 'word-1' });

    service.deleteVocabularyItem('word-1').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/vocabulary/word-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    deleteRequest.flush(null);

    service.importVocabulary(new File(['Word,Definition'], 'vocabulary.csv', { type: 'text/csv' })).subscribe();
    const importRequest = http.expectOne('/api/speed-reading/vocabulary/import');
    expect(importRequest.request.method).toBe('POST');
    expect(importRequest.request.body instanceof FormData).toBe(true);
    importRequest.flush({ successCount: 1, failureCount: 0, errors: [] });

    service.exportVocabulary('Genel', 1, 'age-1').subscribe();
    const exportRequest = http.expectOne('/api/speed-reading/vocabulary/export?category=Genel&difficultyLevel=1&ageGroupId=age-1');
    expect(exportRequest.request.method).toBe('GET');
    exportRequest.flush(new Blob(['csv'], { type: 'text/csv' }));
  });

  it('lists and manages report templates, snapshots and schedules', () => {
    service.getReportTemplates('Progress', true, 50).subscribe(value => expect(value).toEqual([]));
    const templatesRequest = http.expectOne(request =>
      request.url === '/api/speed-reading/reports/templates'
      && request.params.get('type') === 'Progress'
      && request.params.get('isActive') === 'true'
      && request.params.get('limit') === '50');
    expect(templatesRequest.request.method).toBe('GET');
    templatesRequest.flush([]);

    const template = { name: 'İlerleme', description: 'Haftalık', type: 1, category: 1, configurationJson: '{}' };
    service.createReportTemplate(template, 'report-template-key-123456').subscribe();
    const createTemplateRequest = http.expectOne('/api/speed-reading/reports/templates');
    expect(createTemplateRequest.request.method).toBe('POST');
    expect(createTemplateRequest.request.headers.get('Idempotency-Key')).toBe('report-template-key-123456');
    createTemplateRequest.flush({ id: 'template-1' });

    service.updateReportTemplate('template-1', { ...template, isActive: true }).subscribe();
    const updateTemplateRequest = http.expectOne('/api/speed-reading/reports/templates/template-1');
    expect(updateTemplateRequest.request.method).toBe('PUT');
    updateTemplateRequest.flush({ id: 'template-1' });

    service.getReportSnapshots(20).subscribe(value => expect(value).toEqual([]));
    const snapshotsRequest = http.expectOne('/api/speed-reading/reports/snapshots?limit=20');
    expect(snapshotsRequest.request.method).toBe('GET');
    snapshotsRequest.flush([]);

    service.getScheduledReports(20).subscribe(value => expect(value).toEqual([]));
    const schedulesRequest = http.expectOne('/api/speed-reading/reports/scheduled?limit=20');
    expect(schedulesRequest.request.method).toBe('GET');
    schedulesRequest.flush([]);

    const schedule = { reportTemplateId: 'template-1', frequency: 1, dayOfWeek: 1, dayOfMonth: null, deliveryTime: '09:00:00', sendEmail: false, saveToDashboard: true, emailRecipients: null };
    service.createScheduledReport(schedule, 'report-schedule-key-123456').subscribe();
    const createScheduleRequest = http.expectOne('/api/speed-reading/reports/scheduled');
    expect(createScheduleRequest.request.method).toBe('POST');
    expect(createScheduleRequest.request.headers.get('Idempotency-Key')).toBe('report-schedule-key-123456');
    createScheduleRequest.flush({ id: 'schedule-1' });

    service.updateScheduledReportStatus('schedule-1', false).subscribe();
    const statusRequest = http.expectOne('/api/speed-reading/reports/scheduled/schedule-1/status');
    expect(statusRequest.request.method).toBe('PATCH');
    statusRequest.flush({ id: 'schedule-1' });
  });

  it('loads exercise types from the legacy content catalog', () => {
    const response = {
      items: [{
        id: 'type-1',
        name: 'schulte',
        displayName: 'Schulte Tablosu',
        description: 'Odaklanma',
        iconName: 'grid',
        colorCode: '#2563eb',
        sortOrder: 1,
        isActive: true,
        engineType: 'SchulteTable',
        categoryId: null
      }],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1
    };

    service.getExerciseTypes().subscribe(value => expect(value).toEqual(response));

    const request = http.expectOne('/api/speed-reading/exercise-types?pageNumber=1&pageSize=20');
    expect(request.request.method).toBe('GET');
    request.flush(response);
  });

  it('creates an exercise type with an idempotency key', () => {
    service.createExerciseType({
      name: 'schulte',
      displayName: 'Schulte Tablosu',
      description: 'Odaklanma',
      iconName: 'grid',
      colorCode: '#2563eb',
      sortOrder: 1,
      isActive: true,
      engineType: 'SchulteTable',
      categoryId: null
    }, 'admin-type-key-123456').subscribe();

    const request = http.expectOne('/api/speed-reading/exercise-types');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('admin-type-key-123456');
    request.flush({ id: 'type-1' });
  });

  it('loads exercises with paging and writes through the dedicated route', () => {
    service.getExercises(2, 10).subscribe();
    const listRequest = http.expectOne('/api/speed-reading/exercises?pageNumber=2&pageSize=10');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0 });

    service.createExercise({
      title: 'Odak egzersizi',
      description: 'Kısa açıklama',
      difficultyLevel: 2,
      exerciseTypeId: 'type-1',
      configurationJson: '{}',
      targetAgeGroupConfigurationId: null
    }, 'exercise-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/exercises');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('exercise-key-123456');
    createRequest.flush({ id: 'exercise-1' });
  });

  it('loads reading texts and details, then sends idempotent mutations', () => {
    service.getReadingTexts('exercise-1').subscribe(value => expect(value).toEqual([]));
    const listRequest = http.expectOne('/api/speed-reading/reading-texts?exerciseId=exercise-1');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);

    service.getReadingText('text-1').subscribe(value => expect(value.id).toBe('text-1'));
    const detailRequest = http.expectOne('/api/speed-reading/reading-texts/text-1');
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush({
      id: 'text-1', title: 'Metin', content: 'İçerik', wordCount: 1, category: 'Genel',
      difficultyLevel: 1, language: 'tr', isActive: true, exerciseId: null,
      targetAgeGroupConfigurationId: null, tags: [], recommendedMinLevel: 0, recommendedMaxLevel: 10,
      questions: []
    });

    const request = {
      title: 'Metin',
      content: 'İçerik',
      wordCount: 1,
      category: 'Genel',
      difficultyLevel: 1,
      targetAgeGroupConfigurationId: null,
      language: 'tr',
      isActive: true,
      tags: 'odak',
      recommendedMinLevel: 0,
      recommendedMaxLevel: 10,
      exerciseId: null
    };

    service.createReadingText(request, 'reading-text-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/reading-texts');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('reading-text-key-123456');
    createRequest.flush({ id: 'text-1' });

    service.updateReadingText('text-1', request, 'reading-text-update-123456').subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/reading-texts/text-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('reading-text-update-123456');
    updateRequest.flush({ id: 'text-1' });

    service.deleteReadingText('text-1', 'reading-text-delete-123456').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/reading-texts/text-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('reading-text-delete-123456');
    deleteRequest.flush(null);
  });

  it('supports reading-text import/export and program cloning contracts', () => {
    service.exportReadingText('text-1', 'pdf').subscribe();
    const exportRequest = http.expectOne('/api/speed-reading/reading-texts/text-1/export/pdf');
    expect(exportRequest.request.method).toBe('GET');
    expect(exportRequest.request.responseType).toBe('blob');
    exportRequest.flush(new Blob(['pdf']));

    service.importReadingTexts(new File(['title,content'], 'texts.csv', { type: 'text/csv' }), 'csv', 'import-key-123456').subscribe();
    const importRequest = http.expectOne('/api/speed-reading/reading-texts/import/csv');
    expect(importRequest.request.method).toBe('POST');
    expect(importRequest.request.headers.get('Idempotency-Key')).toBe('import-key-123456');
    expect(importRequest.request.body instanceof FormData).toBe(true);
    importRequest.flush({ importedCount: 1 });

    service.cloneProgramTemplate('program-1', 'clone-key-123456').subscribe();
    const cloneRequest = http.expectOne('/api/speed-reading/program-templates/program-1/clone');
    expect(cloneRequest.request.method).toBe('POST');
    expect(cloneRequest.request.headers.get('Idempotency-Key')).toBe('clone-key-123456');
    cloneRequest.flush({ id: 'program-2' });
  });

  it('writes reading questions through the dedicated route with idempotency', () => {
    const request = {
      readingTextId: 'text-1',
      questionText: 'Ana fikir nedir?',
      type: 1,
      bloomLevel: 2,
      difficultyLevel: 1,
      explanation: 'Metnin ana düşüncesi.',
      optionA: 'A',
      optionB: 'B',
      optionC: 'C',
      optionD: 'D',
      correctAnswer: 'A',
      orderIndex: 0
    };

    service.createReadingQuestion(request, 'question-create-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/reading-questions');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('question-create-key-123456');
    createRequest.flush({ id: 'question-1' });

    service.updateReadingQuestion('question-1', {
      questionText: 'Güncellenen soru',
      type: request.type,
      bloomLevel: request.bloomLevel,
      difficultyLevel: request.difficultyLevel,
      explanation: request.explanation,
      optionA: request.optionA,
      optionB: request.optionB,
      optionC: request.optionC,
      optionD: request.optionD,
      correctAnswer: request.correctAnswer,
      orderIndex: request.orderIndex
    }, 'question-update-key-123456').subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/reading-questions/question-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('question-update-key-123456');
    updateRequest.flush({ id: 'question-1' });

    service.deleteReadingQuestion('question-1', 'question-delete-key-123456').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/reading-questions/question-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('question-delete-key-123456');
    deleteRequest.flush(null);
  });

  it('loads and writes program templates through the ProgramManage route', () => {
    service.getProgramTemplates().subscribe(value => expect(value).toEqual([]));
    const listRequest = http.expectOne('/api/speed-reading/program-templates/admin');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);

    const request = {
      name: 'Temel program',
      description: 'Başlangıç programı',
      targetAgeGroupConfigurationId: 'age-1',
      minAssessmentScore: 0,
      maxAssessmentScore: 100,
      weeklyPatternJson: '{}',
      initialDifficultyLevel: 0,
      weeksPerDifficultyIncrease: 1,
      maxDifficultyLevel: 10,
      totalWeeks: 4,
      totalDays: 28,
      isActive: true,
      displayOrder: 1,
      programType: 0,
      examType: null,
      isAssessment: false
    };

    service.createProgramTemplate(request, 'program-create-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/program-templates');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('program-create-key-123456');
    createRequest.flush({ id: 'program-1' });

    service.updateProgramTemplate('program-1', request, 'program-update-key-123456').subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/program-templates/program-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('program-update-key-123456');
    updateRequest.flush({ id: 'program-1' });

    service.deleteProgramTemplate('program-1', 'program-delete-key-123456').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/program-templates/program-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('program-delete-key-123456');
    deleteRequest.flush(null);
  });

  it('loads and writes learning path templates through the ProgramManage route', () => {
    service.getLearningPathTemplates().subscribe(value => expect(value).toEqual([]));
    const listRequest = http.expectOne('/api/speed-reading/learning-paths/templates/admin');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);

    const request = {
      name: 'Temel yol',
      targetAgeGroupConfigurationId: null,
      description: 'Başlangıç yolu',
      estimatedDays: 14,
      isActive: true
    };

    service.createLearningPathTemplate(request, 'path-create-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/learning-paths/templates');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('path-create-key-123456');
    createRequest.flush({ id: 'path-1' });

    service.updateLearningPathTemplate('path-1', request, 'path-update-key-123456').subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/learning-paths/templates/path-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('path-update-key-123456');
    updateRequest.flush({ id: 'path-1' });

    service.deleteLearningPathTemplate('path-1', 'path-delete-key-123456').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/learning-paths/templates/path-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('path-delete-key-123456');
    deleteRequest.flush(null);
  });

  it('loads learning path nodes and writes node mutations idempotently', () => {
    service.getLearningPathTemplateDetails('path-1').subscribe(value => expect(value.nodes).toEqual([]));
    const detailRequest = http.expectOne('/api/speed-reading/learning-paths/templates/path-1/admin');
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush({
      template: {
        id: 'path-1', name: 'Yol', targetAgeGroupConfigurationId: null,
        description: null, totalNodes: 0, estimatedDays: 1, isActive: true
      },
      nodes: []
    });

    service.createLearningPathNode({
      templateId: 'path-1', parentNodeId: null, nodeType: 'Exercise', title: 'Başlangıç',
      contentType: null, contentId: null, order: 0
    }, 'node-create-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/learning-paths/nodes');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('node-create-key-123456');
    createRequest.flush({ id: 'node-1' });

    service.updateLearningPathNode('node-1', {
      parentNodeId: null, nodeType: 'Exercise', title: 'Güncel', contentType: null,
      contentId: null, order: 1
    }, 'node-update-key-123456').subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/learning-paths/nodes/node-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('node-update-key-123456');
    updateRequest.flush({ id: 'node-1' });

    service.deleteLearningPathNode('node-1', 'node-delete-key-123456').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/learning-paths/nodes/node-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('node-delete-key-123456');
    deleteRequest.flush(null);
  });

  it('writes learning path node content and prerequisites through dedicated routes', () => {
    service.createLearningPathNodeContent({
      nodeId: 'node-1', exerciseId: 'exercise-1', readingTextId: null, description: 'İçerik'
    }, 'node-content-create-key-123456').subscribe();
    const contentRequest = http.expectOne('/api/speed-reading/learning-paths/node-contents');
    expect(contentRequest.request.method).toBe('POST');
    expect(contentRequest.request.headers.get('Idempotency-Key')).toBe('node-content-create-key-123456');
    contentRequest.flush({ id: 'content-1' });

    service.updateLearningPathNodeContent('content-1', {
      exerciseId: null, readingTextId: 'text-1', description: 'Güncel'
    }, 'node-content-update-key-123456').subscribe();
    const updateContentRequest = http.expectOne('/api/speed-reading/learning-paths/node-contents/content-1');
    expect(updateContentRequest.request.method).toBe('PUT');
    expect(updateContentRequest.request.headers.get('Idempotency-Key')).toBe('node-content-update-key-123456');
    updateContentRequest.flush({ id: 'content-1' });

    service.createLearningPathPrerequisite({ nodeId: 'node-2', prerequisiteNodeId: 'node-1' }, 'prereq-create-key-123456').subscribe();
    const prerequisiteRequest = http.expectOne('/api/speed-reading/learning-paths/prerequisites');
    expect(prerequisiteRequest.request.method).toBe('POST');
    expect(prerequisiteRequest.request.headers.get('Idempotency-Key')).toBe('prereq-create-key-123456');
    prerequisiteRequest.flush(null);

    service.deleteLearningPathPrerequisite('node-2', 'node-1', 'prereq-delete-key-123456').subscribe();
    const deletePrerequisiteRequest = http.expectOne('/api/speed-reading/learning-paths/prerequisites/node-2/node-1');
    expect(deletePrerequisiteRequest.request.method).toBe('DELETE');
    expect(deletePrerequisiteRequest.request.headers.get('Idempotency-Key')).toBe('prereq-delete-key-123456');
    deletePrerequisiteRequest.flush(null);

    service.deleteLearningPathNodeContent('content-1', 'node-content-delete-key-123456').subscribe();
    const deleteContentRequest = http.expectOne('/api/speed-reading/learning-paths/node-contents/content-1');
    expect(deleteContentRequest.request.method).toBe('DELETE');
    expect(deleteContentRequest.request.headers.get('Idempotency-Key')).toBe('node-content-delete-key-123456');
    deleteContentRequest.flush(null);
  });

  it('loads and writes achievement definitions through the GamificationManage route', () => {
    service.getAchievementsForAdmin(2, 10).subscribe(value => expect(value.items).toEqual([]));
    const listRequest = http.expectOne('/api/speed-reading/achievements/admin?pageNumber=2&pageSize=10');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0 });

    const request = {
      name: 'İlk hafta',
      description: 'Yedi gün düzenli çalışma',
      category: 'Streak',
      tier: 'Bronze',
      iconUrl: null,
      iconEmoji: '🔥',
      criteriaType: 'streak',
      criteriaValue: '{"days":7}',
      triggerType: 'StreakMilestone',
      triggerValue: 7,
      isRepeatable: false,
      xpReward: 50,
      isActive: true,
      sortOrder: 1
    };

    service.createAchievement(request, 'achievement-create-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/achievements');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('achievement-create-key-123456');
    createRequest.flush({ id: 'achievement-1' });

    service.updateAchievement('achievement-1', request, 'achievement-update-key-123456').subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/achievements/achievement-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('achievement-update-key-123456');
    updateRequest.flush({ id: 'achievement-1' });

    service.deleteAchievement('achievement-1', 'achievement-delete-key-123456').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/achievements/achievement-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('achievement-delete-key-123456');
    deleteRequest.flush(null);
  });

  it('loads achievement detail, statistics, categories and tiers', () => {
    service.getAchievementForAdmin('achievement-1').subscribe();
    expect(http.expectOne('/api/speed-reading/achievements/admin/achievement-1').request.method).toBe('GET');

    service.getAchievementStats().subscribe();
    expect(http.expectOne('/api/speed-reading/achievements/admin/stats').request.method).toBe('GET');

    service.getAchievementCategories().subscribe();
    expect(http.expectOne('/api/speed-reading/achievements/categories').request.method).toBe('GET');

    service.getAchievementTiers().subscribe();
    expect(http.expectOne('/api/speed-reading/achievements/tiers').request.method).toBe('GET');
  });

  it('loads and manages CMS, announcements, email and broadcast notification contracts', () => {
    service.getCmsBlocks('HomePage').subscribe(value => expect(value).toEqual([]));
    const blocksRequest = http.expectOne('/api/speed-reading/admin/cms/blocks?group=HomePage');
    expect(blocksRequest.request.method).toBe('GET');
    blocksRequest.flush({ data: [] });

    service.getCmsPages(2, 25).subscribe(value => expect(value.totalCount).toBe(0));
    const pagesRequest = http.expectOne('/api/speed-reading/admin/cms/pages?pageNumber=2&pageSize=25');
    expect(pagesRequest.request.method).toBe('GET');
    pagesRequest.flush({ data: { items: [], totalCount: 0, pageNumber: 2, pageSize: 25 } });

    service.createCmsBlock({ key: 'hero.title', group: 'HomePage', label: 'Başlık', type: 1, value: 'Merhaba' }).subscribe();
    const createBlockRequest = http.expectOne('/api/speed-reading/admin/cms/blocks');
    expect(createBlockRequest.request.method).toBe('POST');
    createBlockRequest.flush({ data: { id: 'block-1' } });

    service.getAnnouncements({ isActive: true, includeExpired: false, take: 25 }).subscribe(value => expect(value).toEqual([]));
    const announcementsRequest = http.expectOne('/api/speed-reading/announcements?isActive=true&includeExpired=false&take=25');
    expect(announcementsRequest.request.method).toBe('GET');
    announcementsRequest.flush([]);

    service.createAnnouncement({
      title: 'Bakım', content: 'Planlı bakım', plainTextContent: 'Planlı bakım', priority: 2,
      targetAudience: 0, targetInstitutionId: null, targetRoles: [], isPinned: false,
      startDate: null, expiresAt: null, displayType: 0, icon: null, colorTheme: null,
      actionUrl: null, actionText: null, sendEmailNotification: false, createInAppNotification: true
    }).subscribe();
    const announcementCreateRequest = http.expectOne('/api/speed-reading/announcements');
    expect(announcementCreateRequest.request.method).toBe('POST');
    announcementCreateRequest.flush({ id: 'announcement-1' });

    service.getSpeedReadingEmailTemplates().subscribe(value => expect(value).toEqual([]));
    const templatesRequest = http.expectOne('/api/speed-reading/email-templates');
    expect(templatesRequest.request.method).toBe('GET');
    templatesRequest.flush([]);

    service.getSpeedReadingEmailCampaigns(1).subscribe(value => expect(value).toEqual([]));
    const campaignsRequest = http.expectOne('/api/speed-reading/email-campaigns?status=1');
    expect(campaignsRequest.request.method).toBe('GET');
    campaignsRequest.flush([]);

    service.getSpeedReadingNotifications(2, 25, { isRead: false, searchTerm: 'Ada' }).subscribe(value => expect(value.totalCount).toBe(0));
    const notificationsRequest = http.expectOne('/api/speed-reading/notifications/all?pageNumber=2&pageSize=25&isRead=false&searchTerm=Ada');
    expect(notificationsRequest.request.method).toBe('GET');
    notificationsRequest.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 25 });

    service.sendSpeedReadingBulkNotification({ targetType: 'All', targetRole: null, title: 'Duyuru', message: 'Mesaj', sendEmail: false }).subscribe();
    const bulkRequest = http.expectOne('/api/speed-reading/notifications/bulk');
    expect(bulkRequest.request.method).toBe('POST');
    bulkRequest.flush({ success: true, totalSent: 0, totalFailed: 0, emailsSent: 0, errors: [] });
  });

  it('loads, uploads and deletes CMS media assets', () => {
    service.getCmsMedia(2, 30).subscribe(value => expect(value.totalCount).toBe(0));
    const listRequest = http.expectOne('/api/speed-reading/admin/cms/media?pageNumber=2&pageSize=30');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({ data: { items: [], totalCount: 0, pageNumber: 2, pageSize: 30 } });

    service.uploadCmsMedia(new File(['png'], 'hero.png', { type: 'image/png' }), 'Hero görseli').subscribe();
    const uploadRequest = http.expectOne('/api/speed-reading/admin/cms/media');
    expect(uploadRequest.request.method).toBe('POST');
    expect(uploadRequest.request.body instanceof FormData).toBe(true);
    expect(uploadRequest.request.body.get('file')).toBeTruthy();
    expect(uploadRequest.request.body.get('altText')).toBe('Hero görseli');
    uploadRequest.flush({ data: { id: 'media-1', url: '/api/speed-reading/cms/media/media-1' } });

    service.deleteCmsMedia('media-1').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/admin/cms/media/media-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    deleteRequest.flush(null);
  });
});
