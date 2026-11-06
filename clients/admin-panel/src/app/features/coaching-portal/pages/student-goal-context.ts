import { LearningContext } from '../../../core/services/coaching-learning-setup.service';
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
