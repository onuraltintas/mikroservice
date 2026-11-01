import { LearningContext } from '../../../core/services/coaching-learning-setup.service';
import { GoalTarget } from '../../../core/services/coaching-study-planning.service';
export function contextExamCodes(context: LearningContext | null): string[] {
  if (context?.purpose === 'Lgs') return ['LGS'];
  // KM1: AGS with its ÖABT field, YDS in English, YÖKDİL in the chosen field.
  if (context?.purpose === 'Ags') return ['AGS', ...(context.primaryScoreType ? [context.primaryScoreType] : [])];
  if (context?.purpose === 'Yds') return ['YDS_EN'];
  if (context?.purpose === 'Yokdil') return context.primaryScoreType ? [context.primaryScoreType] : [];
  if (context?.purpose !== 'Yks') return [];
  // The profile stores the YKS language field as DIL; programs and net targets call the same exam YDT.
  const advanced: Record<string,string> = {SAY:'AYT_SAY',EA:'AYT_EA',SOZ:'AYT_SOZ',YDT:'YDT_EN',DIL:'YDT_EN'};
  return ['TYT', ...(advanced[context.primaryScoreType || ''] ? [advanced[context.primaryScoreType!]] : [])];
}
export function programScoreType(profileScoreType: string): string { return profileScoreType === 'DIL' ? 'YDT' : profileScoreType; }
export function goalFitsContext(target: GoalTarget, context: LearningContext | null): boolean {
  const codes = contextExamCodes(context);
  const metrics = target.netTargets ?? (target.netTarget ? [target.netTarget] : []);
  if (codes.length && metrics.some(x => x.examCode && !codes.includes(x.examCode))) return false;
  if (context?.purpose === 'Lgs') return !target.targetUniversityProgramId;
  if (context?.purpose === 'Yks') return !target.targetSchoolId && (!target.catalogTarget?.scoreType
    || !context.primaryScoreType || target.catalogTarget.scoreType === programScoreType(context.primaryScoreType) || target.catalogTarget.scoreType === 'TYT');
  // AGS, YDS and YÖKDİL goals are net targets only: no school or programme.
  if (context?.purpose === 'Ags' || context?.purpose === 'Yds' || context?.purpose === 'Yokdil') return !target.targetSchoolId && !target.targetUniversityProgramId;
  return false;
}
