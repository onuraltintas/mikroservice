import { describe, expect, it } from 'vitest';
import {
  badgeProgress, examPlace, examSourceLabel, examSourceTone, goalGauge, subjectBars, subjectTone, trendChart, weekBars, weekRanges
} from './growth-model';
import { StudentExamOverviewItem, StudentExamSubject, StudentExamTrend } from '../../../core/services/coaching-student-exam-overview.service';
import { ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';

const item = (patch: Partial<StudentExamOverviewItem>): StudentExamOverviewItem => ({
  examId: 'e', resultId: 'r', source: 'Student', title: 'Deneme', examDate: '2026-10-20T00:00:00Z', examType: 'YKS', examCode: 'TYT',
  examName: 'TYT', net: 60, score: 300, maxScore: 500, sections: [], rank: null, participants: null, topPercent: null, ...patch
});

describe('T6 growth model', () => {
  it('names where an exam comes from in plain words, with the place for an institution exam', () => {
    expect(examSourceLabel(item({ source: 'Student' }))).toBe('Ben');
    expect(examSourceLabel(item({ source: 'Teacher' }))).toBe('Öğretmen');
    expect(examSourceLabel(item({ source: 'Institution', rank: 2, participants: 31 }))).toBe('Kurum');
    expect(examPlace(item({ source: 'Institution', rank: 2, participants: 31 }))).toBe('31 kişide 2.');
    expect(examPlace(item({ source: 'Student' }))).toBe('');
    expect(new Set(['Student', 'Teacher', 'Institution'].map(x => examSourceTone(x as never))).size).toBe(3);
  });

  it('draws a net trend inside the box, oldest exam on the left, with readable labels', () => {
    const trend: StudentExamTrend = { examCode: 'TYT', examName: 'TYT', maxNet: 120, points: [
      { examId: 'a', examDate: '2026-09-01T00:00:00Z', net: 40, source: 'Student' },
      { examId: 'b', examDate: '2026-09-20T00:00:00Z', net: 55.5, source: 'Teacher' },
      { examId: 'c', examDate: '2026-10-10T00:00:00Z', net: 61.25, source: 'Institution' }] };
    const chart = trendChart(trend, 320, 160);
    expect(chart.dots).toHaveLength(3);
    expect(chart.dots[0].x).toBeLessThan(chart.dots[2].x);
    expect(chart.dots[2].y).toBeLessThan(chart.dots[0].y); // a higher net sits higher
    for (const dot of chart.dots) {
      expect(dot.x).toBeGreaterThanOrEqual(0); expect(dot.x).toBeLessThanOrEqual(320);
      expect(dot.y).toBeGreaterThanOrEqual(0); expect(dot.y).toBeLessThanOrEqual(160);
    }
    expect(chart.line.startsWith('M')).toBe(true);
    expect(chart.dots[1].label).toContain('55,5 net');
    expect(chart.change).toBeCloseTo(21.25);
    expect(chart.summary).toBe('3 denemede 40 netten 61,25 nete çıktın.');
    expect(chart.ticks.at(-1)!.value).toBeLessThanOrEqual(120);
  });

  it('keeps a single exam drawable and says so', () => {
    const chart = trendChart({ examCode: 'LGS', examName: 'LGS', maxNet: 90, points: [{ examId: 'a', examDate: '2026-10-01T00:00:00Z', net: 50, source: 'Student' }] }, 320, 160);
    expect(chart.dots).toHaveLength(1);
    expect(Number.isFinite(chart.dots[0].x)).toBe(true);
    expect(chart.summary).toBe('İlk denemen: 50 net. Bir deneme daha gir, gidişatını görelim.');
  });

  it('colours each subject from the lesson palette and marks the weakest', () => {
    const subjects: StudentExamSubject[] = [
      { examCode: 'TYT', sectionCode: 'TUR', name: 'Türkçe', questionCount: 40, averageNet: 30, successRatio: 0.75, examsUsed: 3, weakest: false },
      { examCode: 'TYT', sectionCode: 'TAR', name: 'Tarih', questionCount: 5, averageNet: 1.33, successRatio: 0.266, examsUsed: 3, weakest: true },
      { examCode: 'LGS', sectionCode: 'MAT', name: 'Matematik', questionCount: 20, averageNet: 10, successRatio: 0.5, examsUsed: 1, weakest: true }];
    const bars = subjectBars(subjects, 'TYT');
    expect(bars.map(x => x.name)).toEqual(['Türkçe', 'Tarih']);
    expect(bars[1]).toMatchObject({ weakest: true, percent: 27 });
    expect(bars[0].tone).toBe(subjectTone('TUR'));
    expect(subjectTone('MAT')).not.toBe(subjectTone('TUR'));
    expect(subjectTone('UNKNOWN')).toBeGreaterThanOrEqual(0);
    expect(subjectTone('UNKNOWN')).toBeLessThan(8);
  });

  it('turns the score estimate into a now → target gauge', () => {
    const estimate: ScoreEstimate = {
      estimates: [{ scoreType: 'SAY', name: 'SAY', modelYear: 2025, score: 380, scoreLow: 360, scoreHigh: 400, rank: 90000, rankBest: 80000,
        rankWorst: 100000, percentile: 12, examsUsed: 3, lastExamDate: '2026-10-20', includesDiplomaGrade: false }],
      target: { targetName: 'Ege Üniversitesi · Bilgisayar Mühendisliği', scoreType: 'SAY', targetScore: 440, targetRank: 30000, targetPercentile: 4,
        scoreGap: 60, netGap: 12.5, rankGap: 60000, reached: false }, notice: null };
    const gauge = goalGauge(estimate)!;
    expect(gauge.headline).toBe('Hedefe 12,5 net kaldı');
    expect(gauge.rows.map(x => x.label)).toEqual(['Puan', 'Sıralama']);
    expect(gauge.rows[0]).toMatchObject({ current: '380', target: '440' });
    expect(gauge.rows[0].percent).toBeGreaterThan(80);
    expect(gauge.rows[1]).toMatchObject({ current: '90.000', target: '30.000' });
    expect(gauge.rows[1].percent).toBeLessThan(50);
  });

  it('explains a target without taban or rank instead of a gap (K4)', () => {
    const gauge = goalGauge({ estimates: [], notice: null, target: { targetName: 'Bir lise', scoreType: 'LGS', targetScore: null, targetRank: null,
      targetPercentile: null, scoreGap: null, netGap: null, rankGap: null, reached: false } })!;
    expect(gauge.rows).toEqual([]);
    expect(gauge.headline).toBe('Bu hedefin taban puanı ya da sıralaması yok; farkı hesaplayamıyoruz.');
    expect(goalGauge({ estimates: [], notice: null, target: null })).toBeNull();
  });

  it('lists the last weeks Monday to Sunday and scales the bars', () => {
    const weeks = weekRanges('2026-10-31', 4);
    expect(weeks.map(x => x.from)).toEqual(['2026-10-05', '2026-10-12', '2026-10-19', '2026-10-26']);
    expect(weeks.at(-1)).toMatchObject({ to: '2026-11-01', label: 'Bu hafta' });
    const bars = weekBars([{ label: 'a', minutes: 120, tasks: 2 }, { label: 'b', minutes: 0, tasks: 0 }, { label: 'c', minutes: 300, tasks: 5 }]);
    expect(bars.map(x => x.minutesPercent)).toEqual([40, 0, 100]);
    expect(bars[2].minutesText).toBe('5 sa');
    expect(bars[0].minutesText).toBe('2 sa');
  });

  it('shows how far each badge is, closest first', () => {
    const list = badgeProgress([
      { code: 'five-exams', label: '5 deneme', description: '', earnedOn: null, progress: 3, target: 5 },
      { code: 'first-day', label: 'İlk adım', description: '', earnedOn: '2026-10-01', progress: 1, target: 1 },
      { code: 'streak-30', label: '30 günlük seri', description: '', earnedOn: null, progress: 4, target: 30 }]);
    expect(list.map(x => x.text)).toEqual(['5 deneme: 3/5', '30 günlük seri: 4/30', 'İlk adım: kazandın']);
  });
});
