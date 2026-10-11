import { readFileSync, writeFileSync } from 'node:fs';
const settings = readFileSync(new URL('./settings.json', import.meta.url), 'utf8');
const template = readFileSync(new URL('./apply.template.sql', import.meta.url), 'utf8');
writeFileSync(new URL('./apply.sql', import.meta.url), template.replace('{{SETTINGS}}', settings.trim()));
console.log(`Generated ${JSON.parse(settings).length} Visual Expansion training updates.`);
