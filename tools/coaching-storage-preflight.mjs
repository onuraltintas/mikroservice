import { readFile } from 'node:fs/promises';

export function checkCoachingStorage(config) {
  const issues = [];
  const minio = config?.services?.minio;
  const coaching = config?.services?.['coaching-service'];
  const minioEnvironment = minio?.environment ?? {};
  const coachingEnvironment = coaching?.environment ?? {};

  if (coachingEnvironment.Coaching__Attachments__Provider !== 'Minio') {
    issues.push('Coaching must use the MinIO object-storage provider.');
  }

  if (!minioEnvironment.MINIO_ROOT_USER
    || !coachingEnvironment.Coaching__Attachments__MinioAccessKey
    || minioEnvironment.MINIO_ROOT_USER === coachingEnvironment.Coaching__Attachments__MinioAccessKey) {
    issues.push('Coaching object-storage account must differ from MinIO root account.');
  }

  if (!minio?.volumes?.some(volume => volume.type === 'bind' && volume.target === '/data')) {
    issues.push('MinIO /data must use a host bind mount.');
  }

  if (minio?.ports?.length) {
    issues.push('MinIO must not publish ports to the host.');
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
