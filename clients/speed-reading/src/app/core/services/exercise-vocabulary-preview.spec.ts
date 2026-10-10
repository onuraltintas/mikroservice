import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ExerciseService } from './exercise.service';

describe('Vocabulary preview catalogue', () => {
  it('loads only scenes linked to the selected exercise and their authorized preview details', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    let answer = '';
    TestBed.inject(ExerciseService).getPreviewVisualizationScenes('exercise').subscribe(scenes => answer = scenes[0].questions[0].correctAnswer);
    http.expectOne(r => r.url.endsWith('/visualization/exercises/exercise/scenes')).flush([{ id: 'scene' }]);
    http.expectOne(r => r.url.endsWith('/admin/visualization-scenes/scene')).flush({ id: 'scene', questions: [{ correctAnswer: 'Mavi' }] });
    expect(answer).toBe('Mavi');
    http.verify();
  });
  it('loads every page at the configured difficulty without starting a persistent session', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const service = TestBed.inject(ExerciseService);
    let ids: string[] = [];
    service.getPreviewVocabulary(2, 'Genel').subscribe(words => ids = words.map(word => word.id));
    const first = http.expectOne(r => r.url.endsWith('/vocabulary') && r.params.get('pageNumber') === '1');
    expect(first.request.params.get('difficultyLevel')).toBe('2');
    expect(first.request.params.get('category')).toBe('Genel');
    first.flush({ items: [{ id: 'a', word: 'A', definition: 'Bir' }], pageNumber: 1, totalPages: 2 });
    http.expectOne(r => r.params.get('pageNumber') === '2').flush({ items: [{ id: 'b', word: 'B', definition: 'İki' }], pageNumber: 2, totalPages: 2 });
    expect(ids).toEqual(['a', 'b']);
    http.verify();
  });
});
