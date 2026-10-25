export async function collectCourseReportPages<T>(
  pageSize: number,
  loadPage: (skip: number, take: number) => Promise<T[]>
): Promise<T[]> {
  const items: T[] = [];
  let skip = 0;
  while (true) {
    const page = await loadPage(skip, pageSize);
    items.push(...page);
    if (page.length < pageSize) return items;
    skip += page.length;
  }
}
