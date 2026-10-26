const warmups = new Set(['EyeTracking', 'Saccade', 'Fixation', 'VisualExpansion', 'Tachistoscope', 'SchulteTable']);
const reading = new Set(['Chunking', 'SpeedReading', 'Scanning', 'Comprehension', 'FreeReading', 'TextFading', 'RSVP', 'Skimming', 'RegressionReduction', 'SubvocalizationReduction', 'Visualization', 'ErrorAnalysis', 'Vocabulary']);
const attention = new Set(['Focus']);
const replacements = ['Chunking', 'SpeedReading', 'Scanning', 'FreeReading'];

export function stage(type) {
  if (warmups.has(type)) return 0;
  if (reading.has(type)) return 1;
  if (attention.has(type)) return 2;
  throw new Error(`Unclassified exercise type: ${type}`);
}

export function reviseDay(age, slots, day) {
  const result = structuredClone(slots);
  if (age === 'YoungAdult' || age === 'Adult') {
    const present = new Set(result.map(slot => slot.type));
    const rotated = replacements.map((_, index) => replacements[(day - 1 + index) % replacements.length]);
    for (const slot of result) {
      if (slot.type !== 'Vocabulary') continue;
      slot.type = rotated.find(type => !present.has(type)) ?? rotated[0];
      present.add(slot.type);
    }
  }
  for (const slot of result) stage(slot.type);
  return result.sort((left, right) => stage(left.type) - stage(right.type));
}

export function reviseAssignedSchedule(progress, program, exercises) {
  if (!progress.schedule) return null;
  const catalogue = new Map(exercises.map(exercise => [exercise.id, exercise]));
  const protectedDays = new Set(progress.protectedDays);
  const grouped = new Map();
  for (const slot of progress.schedule) {
    const day = (slot.WeekNumber - 1) * 7 + slot.DayNumber;
    if (!Number.isInteger(day) || day < 1 || day > 28 || !Number.isInteger(slot.Order) || slot.Order < 1)
      throw new Error('Invalid assigned schedule coordinate');
    if (!grouped.has(day)) grouped.set(day, []);
    grouped.get(day).push(slot);
  }
  return [...grouped.entries()].sort(([left], [right]) => left - right).flatMap(([day, slots]) => {
    if (protectedDays.has(day)) return structuredClone(slots);
    const ordered = [...slots].sort((left, right) => left.Order - right.Order);
    if (ordered.some((slot, index) => slot.Order !== index + 1)) throw new Error('Non-contiguous assigned orders');
    const labelled = ordered.map(slot => {
      const exercise = catalogue.get(slot.ExerciseId);
      if (!exercise || exercise.ageId !== program.ageId) throw new Error('Invalid assigned exercise age/scope');
      return { type: exercise.type, exercise, slot };
    });
    return reviseDay(program.age, labelled, day).map((entry, index) => {
      let exercise = entry.exercise;
      if (exercise.type !== entry.type) {
        exercise = exercises.filter(candidate => candidate.type === entry.type
          && candidate.ageId === program.ageId && candidate.level === entry.exercise.level)
          .sort((left, right) => left.id.localeCompare(right.id))[0];
        if (!exercise) throw new Error(`Missing normal replacement: ${program.age}/${entry.type}/${entry.exercise.level}`);
      }
      return { ...entry.slot, ExerciseId: exercise.id, Order: index + 1 };
    });
  });
}
