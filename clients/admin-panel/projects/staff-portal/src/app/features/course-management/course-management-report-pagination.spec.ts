import { collectCourseReportPages } from './course-report-pagination';

describe('course management reports', () => {
  it('loads sequential pages until a short page completes the roster', async () => {
    const requests: { skip: number; take: number }[] = [];
    const result = await collectCourseReportPages(2, async (skip, take) => {
      requests.push({ skip, take });
      return skip === 0 ? ['student-1', 'student-2'] : ['student-3'];
    });

    expect(result).toEqual(['student-1', 'student-2', 'student-3']);
    expect(requests).toEqual([{ skip: 0, take: 2 }, { skip: 2, take: 2 }]);
  });

  it('handles an empty result without requesting another page', async () => {
    let calls = 0;
    const result = await collectCourseReportPages(200, async () => {
      calls++;
      return [];
    });

    expect(result).toEqual([]);
    expect(calls).toBe(1);
  });
});
