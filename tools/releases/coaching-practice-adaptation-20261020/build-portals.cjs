const fs = require('fs'), cp = require('child_process'), path = require('path');
const file = path.join(process.cwd(), 'angular.json');
const original = fs.readFileSync(file), config = JSON.parse(original);
for (const name of ['admin-panel','staff-portal']) {
  config.projects[name].architect.build.configurations['adaptation-release'] = {
    optimization: { scripts:true, styles:{minify:true,inlineCritical:true}, fonts:false }
  };
}
try {
  fs.writeFileSync(file,JSON.stringify(config,null,2));
  for (const name of ['admin-panel','staff-portal']) cp.execFileSync(process.execPath,
    ['node_modules/@angular/cli/bin/ng.js','build',name,'--configuration','production,adaptation-release'],
    {stdio:'inherit',env:{...process.env,NG_BUILD_MAX_WORKERS:'1',NG_BUILD_PARALLEL_TS:'0'}});
} finally { fs.writeFileSync(file,original); }
