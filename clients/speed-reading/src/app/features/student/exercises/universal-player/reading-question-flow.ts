export function shouldShowReadingQuestions(
  engineType: string | undefined,
  textStreamMode: string | undefined,
  questionCount: number,
  exerciseTypeName?: string
): boolean {
  if (questionCount <= 0) return false;
  if (exerciseTypeName?.toLowerCase() === 'tachistoscope') return false;
  return engineType !== 'text_stream'
    || exerciseTypeName?.toLowerCase() === 'rsvp'
    || textStreamMode?.toLowerCase() === 'rsvp';
}
