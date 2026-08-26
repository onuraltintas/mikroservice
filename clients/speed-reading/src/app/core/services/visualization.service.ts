import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface VisualizationQuestion {
  id: string;
  questionText: string;
  options: string[];
  correctAnswer: string;
  questionType: 'detail' | 'color' | 'position' | 'count';
  displayOrder: number;
  hintText?: string;
}

export interface VisualizationScene {
  id: string;
  exerciseId: string;
  description: string;
  imageUrl?: string;
  duration: number;
  displayOrder: number;
  difficultyLevel: number;
  questions: VisualizationQuestion[];
}

@Injectable({
  providedIn: 'root'
})
export class VisualizationService {
  private apiUrl = `${environment.speedReadingApiUrl}/visualization`;

  constructor(private http: HttpClient) {}

  /**
   * Get all visualization scenes for a specific exercise
   * @param exerciseId - The exercise ID
   * @param limit - Optional maximum number of scenes to return (randomly selected)
   */
  getExerciseScenes(exerciseId: string, limit?: number): Observable<VisualizationScene[]> {
    let url = `${this.apiUrl}/exercises/${exerciseId}/scenes`;
    if (limit) {
      url += `?limit=${limit}`;
    }
    return this.http.get<VisualizationScene[]>(url);
  }

  /**
   * Get a specific scene by ID
   */
  getScene(sceneId: string): Observable<VisualizationScene> {
    return this.http.get<VisualizationScene>(`${this.apiUrl}/scenes/${sceneId}`);
  }

  /**
   * Get scenes by difficulty level (1-3)
   */
  getScenesByDifficulty(difficultyLevel: number): Observable<VisualizationScene[]> {
    return this.http.get<VisualizationScene[]>(`${this.apiUrl}/scenes/difficulty/${difficultyLevel}`);
  }
}
