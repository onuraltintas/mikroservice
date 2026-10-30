// Program revision v6 (decisions 30.10.2026).
// Every day follows the same blocks:
//   0 warm-up (one eye or attention exercise) → 1 perception and chunking → 2 reading → 3 comprehension → 4 vocabulary.
// The weekly weight follows the decided sequence: week 1 eye warm-ups and perception speed (tachistoscope, peripheral
// letters), week 2 chunking, week 3 chunked fast reading with level-dependent speed tools, week 4 real reading and
// strategies. Vocabulary only for Child and Teen; YoungAdult and Adult read a free text instead. Child keeps 6 tasks a day;
// the other ages have 6 tasks on days 1–14 and 7 on days 15–28.

export const eyeWarmups = new Set(['EyeTracking', 'Saccade', 'Fixation']);
const blocks = new Map([
  ...['EyeTracking', 'Saccade', 'Fixation', 'SchulteTable', 'TargetHunt'].map(type => [type, 0]),
  ...['Tachistoscope', 'VisualExpansion', 'Chunking'].map(type => [type, 1]),
  ...['SpeedReading', 'FreeReading', 'Scanning', 'Skimming', 'RSVP', 'TextFading', 'RegressionReduction',
    'SubvocalizationReduction', 'Visualization', 'ErrorAnalysis'].map(type => [type, 2]),
  ['Comprehension', 3],
  ['Vocabulary', 4]
]);

export function blockOf(type) {
  const block = blocks.get(type);
  if (block === undefined) throw new Error(`Unclassified exercise type: ${type}`);
  return block;
}

// Levels the live catalogue has per type and age group (the levels programs v5 already resolved live: chunking and text
// fading cover 1–5). A type is only used from its first level. The runtime audit requires every slot at the program's
// own level, so no type may be capped below 5.
export function levelRange(type, age) {
  const child = age === 'Child';
  switch (type) {
    case 'Skimming': return [3, 5];
    case 'ErrorAnalysis': return [4, 5];
    case 'RSVP': return child ? [4, 5] : [3, 5];
    case 'RegressionReduction': return child ? [2, 5] : [1, 5];
    case 'SubvocalizationReduction': return child ? [4, 5] : age === 'Teen' ? [3, 5] : [2, 5];
    default: blockOf(type); return [1, 5];
  }
}

export const tasksPerDay = (age, day) => age === 'Child' || day <= 14 ? 6 : 7;
const hasVocabulary = age => age === 'Child' || age === 'Teen';
const available = (age, level) => type => level >= levelRange(type, age)[0];
const rotate = (list, index) => list[index % list.length];

const warmups = {
  early: ['EyeTracking', 'TargetHunt', 'Saccade', 'SchulteTable', 'Fixation', 'TargetHunt', 'EyeTracking'],
  late: ['TargetHunt', 'EyeTracking', 'SchulteTable', 'TargetHunt', 'Saccade', 'SchulteTable', 'TargetHunt']
};

function dayTypes(age, level, week, day) {
  const usable = available(age, level);
  const odd = day % 2 === 1;
  const child = age === 'Child';
  const kids = hasVocabulary(age);
  const speedTools = ['RegressionReduction', 'TextFading', 'RSVP', 'SubvocalizationReduction', 'Scanning'].filter(usable);
  const realReading = (kids ? ['FreeReading', 'Skimming', 'Scanning'] : ['Skimming', 'Scanning']).filter(usable);
  const strategies = ['Visualization', 'ErrorAnalysis', 'RegressionReduction'].filter(usable);
  const perception = odd ? 'Tachistoscope' : 'VisualExpansion';
  const guided = kids && week <= 2 && !odd ? 'FreeReading' : 'SpeedReading';
  const types = [rotate(week <= 2 ? warmups.early : warmups.late, day - 1)];
  if (week === 1) types.push('Tachistoscope', 'VisualExpansion', guided);
  else if (week === 2) types.push(perception, 'Chunking', guided);
  else if (week === 3) {
    types.push('Chunking');
    if (!child) types.push(perception, guided, rotate(speedTools, day - 1));
    else if (odd) types.push(guided, rotate(speedTools, (day - 1) / 2));
    else types.push('Tachistoscope', guided);
  } else {
    types.push('Chunking', guided);
    if (!child) types.push(rotate(realReading, day - 1), rotate(strategies, day - 1));
    else types.push(odd ? rotate(realReading, (day - 1) / 2) : rotate(strategies, day / 2 - 1));
  }
  if (!kids) types.push('FreeReading');
  types.push('Comprehension');
  if (kids) types.push('Vocabulary');
  return types;
}

export function buildPattern(age, level) {
  const pattern = {};
  for (let week = 1; week <= 4; week++) {
    pattern[`week${week}`] = {};
    for (let day = 1; day <= 7; day++) {
      pattern[`week${week}`][`day${day}`] = dayTypes(age, level, week, day).map(type => {
        const [minimum, maximum] = levelRange(type, age);
        if (level < minimum) throw new Error(`${type} is not available at ${age}/${level}`);
        const slot = { type, count: 1, difficulty: Math.min(level, maximum) };
        return type === 'Comprehension' ? { ...slot, comprehensionMode: day === 7 ? 'evaluation' : 'learning' } : slot;
      });
    }
  }
  return pattern;
}

const ageText = { Child: '9–12 yaş', Teen: '13–16 yaş', YoungAdult: '17–21 yaş', Adult: '22 yaş ve üzeri' };

export function describe(age) {
  const load = age === 'Child' ? '28 gün, 168 görev (her gün 6)' : '28 gün, 182 görev (ilk 14 gün 6, son 14 gün 7)';
  const daily = hasVocabulary(age)
    ? 'ısınma (göz ya da dikkat), algı ve gruplama, okuma, anlama ve kelime'
    : 'ısınma (göz ya da dikkat), algı ve gruplama, okuma, serbest okuma ve anlama';
  return `${ageText[age]}. ${load}. Her gün: ${daily}. 1. hafta göz ısınması ve algılama hızı, 2. hafta kelimeleri `
    + 'öbekler hâlinde görme (gruplama), 3. hafta öbekle hızlı okuma, 4. hafta gerçek metinlerde okuma ve stratejiler. '
    + 'Her haftanın 7. günü anlama ölçülür; bir üst zorluğa geçmek için anlamanın en az %80 olması gerekir.';
}

// Levels every age group has for every level; the day's effective level (program level plus the H7 adaptive offset) is
// read from these types.
const fullRange = new Set(['EyeTracking', 'Saccade', 'Fixation', 'SchulteTable', 'TargetHunt', 'Tachistoscope',
  'VisualExpansion', 'SpeedReading', 'FreeReading', 'Scanning', 'Visualization', 'Comprehension', 'Vocabulary']);

function effectiveLevel(exercises) {
  const counts = new Map();
  for (const exercise of exercises.filter(item => fullRange.has(item.type)))
    counts.set(exercise.level, (counts.get(exercise.level) ?? 0) + 1);
  if (counts.size === 0) throw new Error('Cannot read the day level from the assigned exercises');
  return [...counts.entries()].sort((left, right) => right[1] - left[1] || right[0] - left[0])[0][0];
}

function pick(exercises, program, slot, level, day) {
  const candidates = exercises.filter(item => item.type === slot.type && item.ageId === program.ageId
    && (!slot.comprehensionMode || item.mode === slot.comprehensionMode));
  if (candidates.length === 0) throw new Error(`Missing exercise: ${program.age}/${slot.type}`);
  const levels = [...new Set(candidates.map(item => item.level))];
  const below = levels.filter(value => value <= level);
  const chosen = below.length > 0 ? Math.max(...below) : Math.min(...levels);
  const atLevel = candidates.filter(item => item.level === chosen).sort((left, right) => left.id.localeCompare(right.id));
  return atLevel[(day - 1) % atLevel.length];
}

// Unstarted days of an active assignment (decision 30.10.2026) are rebuilt from the v6 day at the level the day already
// had; finished, started and past days (protectedDays) keep their exercises.
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
    const ordered = [...slots].sort((left, right) => left.Order - right.Order);
    if (protectedDays.has(day)) return structuredClone(ordered);
    const assigned = ordered.map(slot => {
      const exercise = catalogue.get(slot.ExerciseId);
      if (!exercise || exercise.ageId !== program.ageId) throw new Error('Invalid assigned exercise age/scope');
      return exercise;
    });
    const level = effectiveLevel(assigned);
    const week = Math.floor((day - 1) / 7) + 1, dayNumber = (day - 1) % 7 + 1;
    return program.pattern[`week${week}`][`day${dayNumber}`].map((slot, index) => {
      const target = Math.min(level, levelRange(slot.type, program.age)[1]);
      return { WeekNumber: week, DayNumber: dayNumber, Order: index + 1, ExerciseId: pick(exercises, program, slot, target, day).id };
    });
  });
}
