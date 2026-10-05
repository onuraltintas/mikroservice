import { of } from 'rxjs';
import { ExercisesListComponent } from './exercises-list.component';

describe('Custom preview catalogue navigation', () => {
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
});
