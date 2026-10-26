const fs = require('node:fs');
const [image] = JSON.parse(fs.readFileSync('runtime-image.json', 'utf8'));
const c = image.Config;
if (c.User !== 'node' || c.WorkingDir !== '/app' || c.Healthcheck || c.OnBuild?.length || c.Volumes) throw Error('Unexpected runtime configuration');
if (JSON.stringify(c.Entrypoint) !== JSON.stringify(['node', 'dist/admin-panel/server/server.mjs'])) throw Error('Unexpected entrypoint');
// A scratch stage copies the existing runtime filesystem without inheriting its
// accumulated layer history. Preserve image settings; do not change Node or OS.
const lines = ['ARG BASE_IMAGE', 'FROM ${BASE_IMAGE} AS previous', 'FROM scratch', 'COPY --from=previous / /'];
for (const env of c.Env ?? []) {
  const at = env.indexOf('=');
  const key = env.slice(0, at);
  if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(key)) throw Error('Invalid image environment key');
  lines.push('ENV ' + key + '=' + JSON.stringify(env.slice(at + 1)));
}
lines.push('WORKDIR /app', 'USER root',
  'RUN rm -f /app/dist/admin-panel/browser/index.html /app/dist/admin-panel/browser/index.html.gz /app/dist/admin-panel/browser/index.html.br',
  'COPY payload/admin/ /app/dist/admin-panel/',
  'RUN chown -R node:node /app/dist/admin-panel && chmod -R u=rwX,go=rX /app/dist/admin-panel', 'USER node');
for (const port of Object.keys(c.ExposedPorts ?? {})) {
  if (!/^\d+\/(tcp|udp)$/.test(port)) throw Error('Unexpected exposed port');
  lines.push('EXPOSE ' + port);
}
if (c.StopSignal) lines.push('STOPSIGNAL ' + c.StopSignal);
lines.push('ENTRYPOINT ' + JSON.stringify(c.Entrypoint));
if (c.Cmd) lines.push('CMD ' + JSON.stringify(c.Cmd));
lines.push('ARG SOURCE_REVISION', 'LABEL org.opencontainers.image.revision=$SOURCE_REVISION');
fs.writeFileSync('admin-panel.Dockerfile', lines.join('\n') + '\n');
console.log('Existing runtime filesystem and launch settings preserved in a fresh image stage.');
