import { signal } from '@angular/core';
import { of, Subject, throwError } from 'rxjs';
import { DailyExercisesComponent } from './daily-exercises.component';

describe('daily next day action', () => {
  function setup(canAdvance = true, complete = true) {
    const player = Object.create(DailyExercisesComponent.prototype) as any;
    player.advanceState = signal({ programProgressId: 'program', completedProgramDay: 1, nextProgramDay: 2, allowEarlyAdvance: true, canAdvance });
    player.advancing = signal(false);
    player.exercises = signal([{ isCompleted: complete }, { isCompleted: true }]);
    player.loadData = jasmine.createSpy('reload');
    player.toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    player.exerciseService = { advanceDay: jasmine.createSpy('advance').and.returnValue(of({ canAdvance: false })) };
    return player;
  }

  it('sends the displayed completed day and program then reloads the schedule', () => {
    const player = setup(); player.advanceDay();
    expect(player.exerciseService.advanceDay).toHaveBeenCalledOnceWith('program', 1);
    expect(player.loadData).toHaveBeenCalledTimes(1);
  });
  for (const [allowed, complete] of [[false, true], [true, false]]) it(`does not request advance with allowed=${allowed}, completed=${complete}`, () => {
    const player = setup(allowed, complete); player.advanceDay();
    expect(player.exerciseService.advanceDay).not.toHaveBeenCalled();
  });
  it('prevents duplicate clicks and shows server rejection without advancing locally', () => {
    const player = setup(); const response = new Subject();
    player.exerciseService.advanceDay.and.returnValue(response);
    player.advanceDay(); player.advanceDay();
    expect(player.exerciseService.advanceDay).toHaveBeenCalledTimes(1);
    response.error({ error: { detail: 'İzin kaldırıldı.' } });
    expect(player.advancing()).toBeFalse();
    expect(player.toaster.error).toHaveBeenCalledWith('İzin kaldırıldı.');
    expect(player.loadData).not.toHaveBeenCalled();
  });
});
