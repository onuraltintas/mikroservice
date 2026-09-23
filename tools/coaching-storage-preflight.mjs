import { readFile } from 'node:fs/promises';

export function checkCoachingStorage(config) {
  const issues = [];
  const coaching = config?.services?.['coaching-service'];
  const coachingEnvironment = coaching?.environment ?? {};
  const rootPath = coachingEnvironment.Coaching__Attachments__RootPath;

  if (coachingEnvironment.Coaching__Attachments__Provider !== 'Local') {
    issues.push('Production Coaching must use local attachment storage.');
  }

  if (!rootPath || !coaching?.volumes?.some(volume =>
    (volume.type === 'volume' || volume.type === 'bind') && volume.target === rootPath)) {
    issues.push('Coaching attachment path must be backed by a persistent mount.');
  }

  if (coaching?.depends_on?.minio || coaching?.depends_on?.['minio-provision']) {
    issues.push('Production Coaching must not depend on MinIO.');
  }

  return issues;
}

if (process.argv[1] && import.meta.url === new URL(`file:///${process.argv[1].replaceAll('\\', '/')}`).href) {
  try {
    const input = process.argv[2]
      ? await readFile(process.argv[2], 'utf8')
      : await new Promise((resolve, reject) => {
        let text = '';
        process.stdin.setEncoding('utf8');
        process.stdin.on('data', chunk => { text += chunk; });
        process.stdin.on('end', () => resolve(text));
        process.stdin.on('error', reject);
      });
    const issues = checkCoachingStorage(JSON.parse(input));
    for (const issue of issues) console.error(issue);
    if (!issues.length) console.log('Coaching storage configuration preflight passed.');
    process.exitCode = issues.length ? 1 : 0;
  } catch {
    console.error('Unable to read Coaching storage configuration.');
    process.exitCode = 1;
  }
}
