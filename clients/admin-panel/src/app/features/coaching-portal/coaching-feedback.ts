import { DestroyRef, effect, inject, Injector, Signal } from '@angular/core';
import type { ToasterService } from '../../core/services/toaster.service';

function feedbackLoader() {
  const injector=inject(Injector), destroy=inject(DestroyRef);
  return async () => {
    const {ToasterService}=await import('../../core/services/toaster.service');
    return destroy.destroyed ? null : injector.get(ToasterService);
  };
}

/** Shared Material feedback loads on demand, including in the lean staff application. */
export function coachingToaster(): Pick<ToasterService,'confirm'|'success'|'error'> {
  const load=feedbackLoader();
  return {
    confirm: async (message,options) => (await load())?.confirm(message,options) ?? false,
    success: (message) => { void load().then(toaster=>toaster?.success(message)); },
    error: (message) => { void load().then(toaster=>toaster?.error(message)); }
  };
}

/** Keep field-level messages visible while announcing outcomes through the shared toaster. */
export function coachingFeedback(success?: Signal<string | null>, error?: Signal<string | null>): void {
  const load=feedbackLoader();
  for(const [source,kind] of [[success,'success'],[error,'error']] as const) if(source) effect(onCleanup=>{
    const message=source();let cancelled=false;onCleanup(()=>{cancelled=true;});
    if(message) void load().then(toaster=>{if(!cancelled)toaster?.[kind](message);});
  });
}
