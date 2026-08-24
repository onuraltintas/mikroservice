import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { BaseComponent } from '../../../core/components/base.component';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ExerciseService } from '../../../core/services/exercise.service';
import { ExerciseTypeService } from '../../../core/services/exercise-type.service';
import { Exercise } from '../../../core/models/exercise.model';
import { ExerciseLevelDialogComponent } from './exercise-level-dialog/exercise-level-dialog.component';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';

interface ExerciseCard {
  id?: string;
  name: string;
  route: string;
  icon: string;
  color: string;
  description: string;
  difficulty: string;
  category: string;
  estimatedTime: string;
  benefits: string[];
  exercises?: Exercise[];
  exerciseTypeId?: string;
}

@Component({
  selector: 'app-exercises-list',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule
  ],
  templateUrl: './exercises-list.component.html',
  styleUrls: ['./exercises-list.component.scss']
})
export class ExercisesListComponent extends BaseComponent implements OnInit {
  private router = inject(Router);
  private exerciseService = inject(ExerciseService);
  private exerciseTypeService = inject(ExerciseTypeService);
  private dialog = inject(MatDialog);
  private authService = inject(AuthService);

  // Teacher preview mode
  isTeacher = false;

  // loading inherited from BaseComponent
  selectedCategory = 'all';
  categories: string[] = ['all'];
  exercises: ExerciseCard[] = [];
  filteredExercises: ExerciseCard[] = [];

  // Map backend types (Name) to frontend display categories
  private categoryMap: { [key: string]: string } = {
    'SchulteTable': 'Göz Hareketleri',
    'VisualExpansion': 'Göz Hareketleri',
    'Fixation': 'Göz Hareketleri',
    'Saccade': 'Göz Hareketleri',
    'EyeTracking': 'Göz Hareketleri',
    'Tachistoscope': 'Hız Geliştirme',
    'SpeedReading': 'Hız Geliştirme',
    'RSVP': 'Hız Geliştirme',
    'Scanning': 'Hız Geliştirme',
    'Skimming': 'Hız Geliştirme',
    'SubvocalizationReduction': 'Hız Geliştirme',
    'RegressionReduction': 'Hız Geliştirme',
    'TextFading': 'Odaklanma',
    'Focus': 'Odaklanma',
    'Comprehension': 'Kavrama',
    'Chunking': 'Kavrama',
    'Vocabulary': 'Kavrama',
    'Visualization': 'Kavrama',
    'ExamSimulation': 'Kavrama',
    'FreeReading': 'Okuma',
    'ErrorAnalysis': 'Okuma'
  };

  ngOnInit(): void {
    this.isTeacher = this.authService.hasRole('Teacher') || this.authService.hasRole('InstitutionAdmin');
    this.loadData();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  loadData(): void {
    this.loading.set(true);

    forkJoin({
      types: this.exerciseTypeService.getActiveExerciseTypes(),
      exercises: this.exerciseService.getExercises(undefined, undefined, undefined, 1, 1000)
    }).subscribe({
      next: (data) => {
        this.processData(data.types.items, data.exercises.items);
        this.loading.set(false);
      },
      error: (err) => {
        this.handleError(err, 'Egzersizler yüklenirken hata oluştu');
        this.loading.set(false);
      }
    });
  }

  processData(types: any[], exercises: Exercise[]): void {
    this.exercises = [];

    // Group exercises by TypeId
    const exerciseGroups = new Map<string, Exercise[]>();
    exercises.forEach(ex => {
      const typeId = ex.exerciseTypeId;
      if (!exerciseGroups.has(typeId)) {
        exerciseGroups.set(typeId, []);
      }
      exerciseGroups.get(typeId)?.push(ex);
    });

    // Create cards for each type that has exercises
    types.forEach(type => {
      const groupExercises = exerciseGroups.get(type.id);

      // Skip if type has no exercises (optional)
      if (!groupExercises || groupExercises.length === 0) return;

      // Sort by difficulty
      groupExercises.sort((a, b) => a.difficultyLevel - b.difficultyLevel);

      const displayCategory = this.categoryMap[type.name] || 'Diğer';

      const card: ExerciseCard = {
        name: type.displayName || type.name,
        route: '', // Handled by click
        icon: type.iconName || 'fitness_center',
        color: type.colorCode ? `linear-gradient(135deg, ${type.colorCode} 0%, #444 100%)` : 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
        description: type.description || 'Egzersiz açıklaması bulunamadı.',
        difficulty: groupExercises.length > 1 ? '1-' + groupExercises[groupExercises.length - 1].difficultyLevel : groupExercises[0].difficultyLevel.toString(),
        category: displayCategory,
        estimatedTime: '5-15 dk', // Dynamic?
        benefits: ['Hız Geliştirme', 'Odaklanma'], // Static for now
        exercises: groupExercises,
        exerciseTypeId: type.id
      };

      this.exercises.push(card);
    });

    // Extract unique categories for dynamic filtering
    const uniqueCategories = new Set<string>(this.exercises.map(e => e.category));
    this.categories = ['all', ...Array.from(uniqueCategories).sort()];

    this.filterByCategory(this.selectedCategory);
  }

  filterByCategory(category: string): void {
    this.selectedCategory = category;
    if (category === 'all') {
      this.filteredExercises = [...this.exercises];
    } else {
      this.filteredExercises = this.exercises.filter(ex => ex.category === category);
    }
  }

  startExercise(exerciseCard: ExerciseCard): void {
    if (!exerciseCard.exercises || exerciseCard.exercises.length === 0) {
      this.toaster.error('Bu kategoride aktif egzersiz bulunamadı.');
      return;
    }

    // If only 1 level, start immediately
    if (exerciseCard.exercises.length === 1) {
      this.router.navigate(['/student/exercises/universal-player', exerciseCard.exercises[0].id]);
      return;
    }

    // If multiple levels, show dialog
    const dialogRef = this.dialog.open(ExerciseLevelDialogComponent, {
      width: '500px',
      data: {
        title: exerciseCard.name,
        exercises: exerciseCard.exercises
      }
    });

    dialogRef.afterClosed().subscribe(selectedExercise => {
      if (selectedExercise) {
        this.router.navigate(['/student/exercises/universal-player', selectedExercise.id]);
      }
    });
  }

  showInfo(exercise: ExerciseCard): void {
  }

  getCategoryIcon(category: string): string {
    switch (category) {
      case 'all': return 'apps';
      case 'Göz Hareketleri': return 'visibility';
      case 'Hız Geliştirme': return 'speed';
      case 'Kavrama': return 'psychology';
      case 'Odaklanma': return 'center_focus_strong';
      case 'Hafıza': return 'memory';
      case 'Genel': return 'category';
      case 'Okuma': return 'menu_book';
      default: return 'label';
    }
  }

  goBack(): void {
    if (this.isTeacher) {
      this.router.navigate(['/teacher/dashboard']);
    } else {
      this.router.navigate(['/student/dashboard']);
    }
  }
}
