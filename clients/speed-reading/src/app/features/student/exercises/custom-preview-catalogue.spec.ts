import { of } from 'rxjs';
import { ExercisesListComponent } from './exercises-list.component';
import { CustomPreviewDialogComponent } from './universal-player/custom-preview-dialog.component';

describe('Custom preview catalogue navigation', () => {
  it('opens one settings dialog directly for a multi-level custom preview', () => {
    const open = jasmine.createSpy('open').and.returnValue({ afterClosed: () => of({ exerciseId: 'second', values: { speedWpm: 400 } }) });
    const navigate = jasmine.createSpy('navigate');
    const context: any = { dialog: { open }, router: { navigate }, authService: { currentUserValue: { roles: ['Teacher'] } },
      toaster: { error: jasmine.createSpy('error') }, openCustomPreview: ExercisesListComponent.prototype.openCustomPreview };
    const exercises = [
      { id: 'first', difficultyLevel: 1, configurationJson: JSON.stringify({ engineType: 'word_highlight' }) },
      { id: 'second', difficultyLevel: 2, configurationJson: JSON.stringify({ engineType: 'word_highlight' }) }
    ];
    (ExercisesListComponent.prototype as any).startExercise.call(context, { exercises }, true);
    expect(open).toHaveBeenCalledTimes(1);
    expect(open.calls.mostRecent().args[0]).toBe(CustomPreviewDialogComponent);
    expect(navigate).toHaveBeenCalledWith(['/student/exercises/universal-player', 'second'], {
      state: { customPreview: { exerciseId: 'second', values: { speedWpm: 400 } } }
    });
  });
  it('passes settings only in matching local navigation state, without an API write', () => {
    const navigate = jasmine.createSpy('navigate');
    const open = jasmine.createSpy('open').and.returnValue({ afterClosed: () => of({ speedWpm: 300, chunkSize: 2 }) });
    const context: any = { router: { navigate }, dialog: { open },
      authService: { currentUserValue: { roles: ['Teacher'] } }, toaster: { error: jasmine.createSpy('error') } };
    (ExercisesListComponent.prototype as any).openCustomPreview.call(context,
      { id: 'exercise', configurationJson: JSON.stringify({ engineType: 'word_highlight' }) });
    expect(navigate).toHaveBeenCalledWith(['/student/exercises/universal-player', 'exercise'], {
      state: { customPreview: { exerciseId: 'exercise', values: { speedWpm: 300, chunkSize: 2 } } }
    });
  });
  it('does not open customization for student accounts', () => {
    const open = jasmine.createSpy('open');
    const context: any = { dialog: { open }, authService: { currentUserValue: { roles: ['Student', 'Teacher'] } } };
    (ExercisesListComponent.prototype as any).openCustomPreview.call(context, { id: 'exercise' });
    expect(open).not.toHaveBeenCalled();
  });
  it('does not navigate when customization is cancelled', () => {
    const navigate = jasmine.createSpy('navigate');
    const open = jasmine.createSpy('open').and.returnValue({ afterClosed: () => of(undefined) });
    const context: any = { router: { navigate }, dialog: { open }, authService: { currentUserValue: { roles: ['Admin'] } }, toaster: { error: jasmine.createSpy('error') } };
    (ExercisesListComponent.prototype as any).openCustomPreview.call(context,
      { id: 'exercise', configurationJson: JSON.stringify({ engineType: 'word_highlight' }) });
    expect(navigate).not.toHaveBeenCalled();
  });
  it('explains configurations without adjustable controls instead of opening an empty form', () => {
    const open = jasmine.createSpy('open').and.returnValue({ afterClosed: () => of(undefined) });
    const error = jasmine.createSpy('error');
    const context: any = { dialog: { open }, authService: { currentUserValue: { roles: ['Teacher'] } }, toaster: { error } };
    (ExercisesListComponent.prototype as any).openCustomPreview.call(context,
      { id: 'exercise', configurationJson: JSON.stringify({ engineType: 'vocabulary_builder', engineConfig: { mode: 'learn' } }) });
    expect(open).not.toHaveBeenCalled();
    expect(error).toHaveBeenCalled();
  });
});
