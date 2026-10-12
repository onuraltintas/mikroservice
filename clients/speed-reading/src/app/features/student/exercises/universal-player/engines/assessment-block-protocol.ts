export interface AssessmentBlockProtocol {
  version: number;
  practiceRounds: number;
  roundsPerBlock: number;
  blockValues: number[];
}

export function readAssessmentBlockProtocol(raw: unknown, visualExpansion = false): AssessmentBlockProtocol | undefined {
  if (raw == null) return undefined;
  const plan = raw as AssessmentBlockProtocol, values = plan.blockValues;
  if (plan.version !== 2 || plan.practiceRounds !== 3 || ![6, 8, 10].includes(plan.roundsPerBlock)
    || !Array.isArray(values) || values.length !== 3
    || values.some(value => !Number.isInteger(value) || value < (visualExpansion ? 2 : 50) || value > (visualExpansion ? 60 : 5000))
    || (visualExpansion ? values[0] >= values[1] || values[1] >= values[2] : values[0] <= values[1] || values[1] <= values[2]))
    throw new Error('Seviye tespit blok ayarları geçerli değil.');
  return plan;
}

export function assessmentBlockValue(plan: AssessmentBlockProtocol, round: number): number {
  return plan.blockValues[Math.max(0, Math.min(2, Math.floor((round - plan.practiceRounds) / plan.roundsPerBlock)))];
}

export function assessmentRoundLabel(plan: AssessmentBlockProtocol | null | undefined, round: number): string | null {
  if (plan?.version !== 2) return null;
  if (round < plan.practiceRounds) return `Alıştırma ${round + 1}/${plan.practiceRounds} — puana dahil değil`;
  const completed = round - plan.practiceRounds, total = plan.roundsPerBlock * 3;
  return `Ölçüm ${Math.min(completed, total)}/${total} · Blok ${Math.min(3, Math.floor(completed / plan.roundsPerBlock) + 1)}/3`;
}
