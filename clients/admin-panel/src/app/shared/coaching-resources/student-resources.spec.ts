import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StudentResource, resourceRangeText, sectionsFromTopics } from '../../core/services/coaching-resources.service';
import { StudentResourcesComponent, resourceDraftProblem } from './student-resources.component';

const resource = (over: Partial<StudentResource> = {}): StudentResource => ({
  id: 'r1', version: 2, name: 'TEST Matematik Soru Bankası', publisher: 'TEST Yayınları', lessonName: 'Matematik', kind: 'QuestionBank', unit: 'Test',
  totalUnits: 40, doneBefore: 2, doneUnits: 6, percent: 15, plannedUnits: 2, isArchived: false, canDelete: false, createdAt: '2026-11-22T10:00:00Z',
  updatedAt: null, sections: [{ title: 'Ünite 1', fromUnit: 1, toUnit: 10, doneUnits: 6 }], next: { fromUnit: 7, toUnit: 10, sectionTitle: 'Ünite 1', topicId: null },
  ...over
});

const catalog = (lessons: { id: string; name: string; examCode: string | null }[], topics: { id: string; name: string }[] = []) => ({
  curriculum: null, missingDurationCount: 0, warnings: [],
  lessons: lessons.map(x => ({ ...x, gradeNumber: 8, topicCount: topics.length, missingDurationCount: 0 })),
  topics: { items: topics.map(x => ({ ...x, lessonName: 'Matematik', unitName: 'Sayılar', gradeNumber: 8, examCode: 'LGS', estimatedMinutes: 60 })),
    totalCount: topics.length, pageNumber: 1, pageSize: 50 }
});

function setup(inputs: Record<string, unknown> = {}) {
  TestBed.configureTestingModule({ imports: [StudentResourcesComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
  const fixture = TestBed.createComponent(StudentResourcesComponent);
  for (const [key, value] of Object.entries(inputs)) fixture.componentRef.setInput(key, value);
  fixture.detectChanges();
  return { fixture, http: TestBed.inject(HttpTestingController), element: fixture.nativeElement as HTMLElement };
}

describe('Kaynaklarım (KocApp uyumu)', () => {
  it('writes the range and checks a draft in the reader\'s words', () => {
    expect(resourceRangeText('TEST Kitap', 'Test', 12, 15)).toBe('TEST Kitap, test 12–15');
    expect(resourceRangeText('TEST Kitap', 'Page', 7, 7)).toBe('TEST Kitap, sayfa 7');
    expect(resourceRangeText('TEST Kitap', 'Question', null, null)).toBe('TEST Kitap');
    const draft = { id: null, version: null, name: 'TEST', publisher: '', lessonName: '', kind: 'QuestionBank' as const, unit: 'Page' as const,
      totalUnits: 100, doneBefore: 0, sections: [] as { title: string; fromUnit: number | null; toUnit: number | null; topicId: string | null }[], lessonId: '', examCode: '' };
    expect(resourceDraftProblem(draft)).toBeNull();
    expect(resourceDraftProblem({ ...draft, name: ' ' })).toContain('adını');
    expect(resourceDraftProblem({ ...draft, totalUnits: 0 })).toContain('sayfa');
    expect(resourceDraftProblem({ ...draft, doneBefore: 101 })).toContain('aşamaz');
    expect(resourceDraftProblem({ ...draft, sections: [{ title: 'Ünite 1', fromUnit: 90, toUnit: 120, topicId: null }] })).toContain('1–100');
  });

  it('lists the student\'s resources with progress, the next part and a labelled meter', () => {
    const { fixture, http, element } = setup();
    http.expectOne('/api/coaching/study-planning/resources').flush({ data: [resource()] });
    fixture.detectChanges();
    expect(element.querySelector('h2')!.textContent).toContain('Kaynaklarım');
    const card = element.querySelector('[data-testid="resource-card"]')!;
    expect(card.textContent).toContain('6 / 40 test bitti');
    expect(card.textContent).toContain('2 test planda bekliyor');
    expect(element.querySelector('[data-testid="resource-next"]')!.textContent).toContain('test 7–10, Ünite 1');
    const meter = card.querySelector('[role="meter"]')!;
    expect(meter.getAttribute('aria-valuenow')).toBe('15');
    expect(meter.getAttribute('aria-valuetext')).toContain('6 / 40 test bitti');
    // A resource the plan uses is archived, not deleted.
    expect([...card.querySelectorAll('button')].map(x => x.textContent!.trim())).not.toContain('Sil');
    http.verify();
  });

  it('adds a resource for a teacher\'s student and says what is wrong before asking the server', () => {
    const { fixture, http, element } = setup({ studentId: 's1', audience: 'staff' });
    http.expectOne('/api/coaching/staff/students/s1/study/resources').flush({ data: [] });
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="resources-empty"]')!.textContent).toContain('Öğrencinin henüz kaynağı yok');
    const component = fixture.componentInstance;
    component.startNew();
    http.expectOne(r => r.url === '/api/coaching/staff/students/s1/study/learning-setup/catalog').flush({ data: catalog([]) });
    component.save();
    expect(component.error()).toBe('Kaynağın adını yazın.');
    const draft = component.draft()!;
    Object.assign(draft, { name: ' TEST Fizik ', unit: 'Page', totalUnits: 120, doneBefore: 10 });
    component.addSection();
    Object.assign(component.draft()!.sections[0], { title: 'Kuvvet', toUnit: 40 });
    component.save();
    const request = http.expectOne('/api/coaching/staff/students/s1/study/resources');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(expect.objectContaining({ name: 'TEST Fizik', unit: 'Page', totalUnits: 120, doneBefore: 10,
      sections: [{ title: 'Kuvvet', fromUnit: 1, toUnit: 40, topicId: null }], expectedVersion: null }));
    request.flush({ data: resource({ id: 'r2', name: 'TEST Fizik', canDelete: true }) });
    expect(component.items().map(x => x.id)).toEqual(['r2']);
    expect(component.notice()).toBe('Kaynak eklendi.');
    http.verify();
  });

  it('shows a parent the child\'s resources without any change buttons', () => {
    const { fixture, http, element } = setup({ studentId: 'c1', audience: 'parent', childName: 'Elif' });
    http.expectOne('/api/coaching/parents/me/children/c1/resources').flush({ data: [resource()] });
    fixture.detectChanges();
    expect(element.textContent).toContain('Elif çalıştığı kitaplar');
    expect(element.querySelectorAll('button').length).toBe(0);
    expect(element.querySelector('[data-testid="resource-next"]')).toBeNull();
    http.verify();
  });

  it('lets a teacher copy a resource to other students of hers and says who was skipped', () => {
    const { fixture, http, element } = setup({ studentId: 's1', audience: 'staff' });
    http.expectOne('/api/coaching/staff/students/s1/study/resources').flush({ data: [resource()] });
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.startCopy(component.items()[0]);
    http.expectOne(r => r.url === '/api/coaching/teachers/me/students').flush({ items: [{ userId: 's1', fullName: 'Kendisi' }, { userId: 's2', fullName: 'TEST Ali' }, { userId: 's3', fullName: 'TEST Ayşe' }] });
    fixture.detectChanges();
    const copy = element.querySelector('[data-testid="resource-copy"]')!;
    expect(copy.textContent).toContain('TEST Ali');
    expect(copy.textContent).not.toContain('Kendisi');
    component.toggleCopy('s2', true); component.toggleCopy('s3', true); component.toggleCopy('s3', false); component.toggleCopy('s3', true);
    component.copy(component.items()[0]);
    const request = http.expectOne('/api/coaching/staff/students/s1/study/resources/r1/copy');
    expect(request.request.body).toEqual({ studentIds: ['s2', 's3'] });
    request.flush({ data: { copied: 1, skipped: 1 } });
    expect(component.notice()).toBe('1 öğrenciye eklendi. 1 öğrencide aynı adlı kaynak zaten vardı.');
    http.verify();
  });

  it('keeps an institution administrator to reading', () => {
    const { fixture, http, element } = setup({ studentId: 's1', audience: 'staff', readonly: true });
    http.expectOne('/api/coaching/staff/students/s1/study/resources').flush({ data: [resource()] });
    fixture.detectChanges();
    expect([...element.querySelectorAll('button')].map(x => x.textContent!.trim())).toEqual([]);
    http.verify();
  });

  it('ties a section to a curriculum topic found by name', () => {
    const { fixture, http } = setup();
    http.expectOne('/api/coaching/study-planning/resources').flush({ data: [] });
    const component = fixture.componentInstance;
    component.startNew();
    http.expectOne(r => r.url === '/api/coaching/study-planning/setup/catalog').flush({ data: catalog([]) });
    Object.assign(component.draft()!, { name: 'TEST Konu Kitabı', totalUnits: 30 });
    component.addSection();
    const section = component.draft()!.sections[0];
    section.topicQuery = 'çarpan';
    component.searchTopic(section);
    http.expectOne(r => r.url === '/api/coaching/study-planning/topics' && r.params.get('search') === 'çarpan')
      .flush({ data: { items: [{ id: 't1', name: 'Çarpanlar ve Katlar', lessonName: 'Matematik', unitName: 'Sayılar', gradeNumber: 8, examCode: 'LGS', estimatedMinutes: 60 }], totalCount: 1, pageNumber: 1, pageSize: 20 } });
    component.pickTopic(section, component.draft()!.sections[0].topicResults![0]);
    Object.assign(component.draft()!.sections[0], { toUnit: 12 });
    component.save();
    const create = http.expectOne('/api/coaching/study-planning/resources');
    expect(create.request.body.sections).toEqual([{ title: 'Çarpanlar ve Katlar', fromUnit: 1, toUnit: 12, topicId: 't1' }]);
    create.flush({ data: resource({ id: 'r9' }) });
    http.verify();
  });

  it('ties a resource to a lesson and its exam, and builds the sections from the lesson\'s topics', async () => {
    expect(sectionsFromTopics([{ id: 'a', name: 'A' }, { id: 'b', name: 'B' }, { id: 'c', name: 'C' }], 10).map(x => [x.fromUnit, x.toUnit]))
      .toEqual([[1, 4], [5, 7], [8, 10]]);
    expect(sectionsFromTopics([{ id: 'a', name: 'A' }, { id: 'b', name: 'B' }], 1)).toEqual([{ title: 'A', fromUnit: 1, toUnit: 1, topicId: 'a' }]);
    const { fixture, http, element } = setup({ studentId: 's1', audience: 'staff' });
    http.expectOne('/api/coaching/staff/students/s1/study/resources').flush({ data: [] });
    const component = fixture.componentInstance;
    component.startNew();
    http.expectOne(r => r.url.endsWith('/learning-setup/catalog') && !r.params.has('lessonId'))
      .flush({ data: catalog([{ id: 'math', name: 'Matematik', examCode: 'LGS' }, { id: 'tr', name: 'Türkçe', examCode: 'LGS' }]) });
    fixture.detectChanges();
    expect(element.querySelector('select[name="lessonId"]')!.textContent).toContain('Matematik, LGS');
    Object.assign(component.draft()!, { name: 'TEST Matematik Soru Bankası', unit: 'Test', totalUnits: 9 });
    component.chooseLesson('math');
    expect([component.draft()!.lessonName, component.draft()!.examCode]).toEqual(['Matematik', 'LGS']);
    http.expectOne(r => r.url.endsWith('/learning-setup/catalog') && r.params.get('lessonId') === 'math')
      .flush({ data: catalog([], [{ id: 't1', name: 'Çarpanlar' }, { id: 't2', name: 'Üslü İfadeler' }]) });
    await component.sectionsFromLesson();
    expect(component.draft()!.sections.map(x => [x.title, x.fromUnit, x.toUnit, x.topicId])).toEqual([['Çarpanlar', 1, 5, 't1'], ['Üslü İfadeler', 6, 9, 't2']]);
    expect(component.notice()).toContain('2 bölüm oluşturuldu');
    component.save();
    const create = http.expectOne('/api/coaching/staff/students/s1/study/resources');
    expect(create.request.body).toEqual(expect.objectContaining({ lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS' }));
    create.flush({ data: resource({ id: 'r3', examCode: 'LGS', lessonId: 'math' }) });
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="resource-card"]')!.textContent).toContain('LGS');
    http.verify();
  });
});
