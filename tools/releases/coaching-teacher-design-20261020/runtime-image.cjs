const fs=require('node:fs');
const [image]=JSON.parse(fs.readFileSync('runtime-image.json','utf8')),c=image.Config;
if(c.User!=='root'||c.WorkingDir!=='/'||c.Volumes||c.OnBuild?.length)throw Error('Unexpected staff runtime configuration');
if(JSON.stringify(c.Entrypoint)!==JSON.stringify(['/docker-entrypoint.sh'])||JSON.stringify(c.Cmd)!==JSON.stringify(['nginx','-g','daemon off;']))throw Error('Unexpected staff entrypoint');
const lines=['ARG BASE_IMAGE','FROM ${BASE_IMAGE} AS previous','FROM scratch','COPY --from=previous / /'];
for(const env of c.Env??[]){const at=env.indexOf('='),key=env.slice(0,at);if(!/^[A-Za-z_][A-Za-z0-9_]*$/.test(key))throw Error('Invalid environment key');lines.push('ENV '+key+'='+JSON.stringify(env.slice(at+1)));}
lines.push('WORKDIR /','USER root','RUN rm -f /usr/share/nginx/html/index.html /usr/share/nginx/html/index.html.gz /usr/share/nginx/html/index.html.br','COPY payload/staff/ /usr/share/nginx/html/','RUN chmod -R u=rwX,go=rX /usr/share/nginx/html');
for(const port of Object.keys(c.ExposedPorts??{})){if(!/^\d+\/(tcp|udp)$/.test(port))throw Error('Invalid port');lines.push('EXPOSE '+port);}
if(c.StopSignal)lines.push('STOPSIGNAL '+c.StopSignal);
if(c.Healthcheck){
 const h=c.Healthcheck,flags=[];
 for(const [key,flag] of [['Interval','interval'],['Timeout','timeout'],['StartPeriod','start-period'],['StartInterval','start-interval']])if(h[key]>0)flags.push('--'+flag+'='+h[key]+'ns');
 if(h.Retries>0)flags.push('--retries='+h.Retries);
 if(h.Test?.[0]==='NONE')lines.push('HEALTHCHECK NONE');
 else if(h.Test?.[0]==='CMD')lines.push('HEALTHCHECK '+flags.join(' ')+' CMD '+JSON.stringify(h.Test.slice(1)));
 else if(h.Test?.[0]==='CMD-SHELL'&&h.Test.length===2&&!/[\r\n]/.test(h.Test[1]))lines.push('HEALTHCHECK '+flags.join(' ')+' CMD '+h.Test[1]);
 else throw Error('Unsupported existing health check');
}
lines.push('ENTRYPOINT '+JSON.stringify(c.Entrypoint),'CMD '+JSON.stringify(c.Cmd),'ARG SOURCE_REVISION','LABEL org.opencontainers.image.revision=$SOURCE_REVISION');
fs.writeFileSync('staff-portal.Dockerfile',lines.join('\n')+'\n');
console.log('Existing Nginx runtime filesystem and launch configuration preserved.');
