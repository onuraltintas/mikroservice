import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const html = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.html', import.meta.url), 'utf8');
const scss = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.scss', import.meta.url), 'utf8');
const motionPath = html.split('<!-- Exercise Running - Motion Path (Eye Tracking / Fixation) -->')[1]
  ?.split('<!-- Exercise Running - Text Stream')[0] ?? '';

test('saccade describes observation and reports seconds and transitions without letter or accuracy claims', () => {
  assert.doesNotMatch(motionPath, /Gerektiğinde tıklayın/);
  assert.match(motionPath, /geçiş/);
  const results = html.split('class="saccade-results"')[1]?.split('measurement-not-available')[0] ?? '';
  assert.match(results, /Aktif çalışma süresi/);
  assert.match(results, /Tamamlanan hedef geçişi/);
  assert.match(results, /Hedef geçiş aralığı/);
  assert.match(html, /Göz hareketleri ölçülmedi/);
});

test('fixation shows one compact statistics strip while observation modes hide it', () => {
  assert.match(motionPath, /class="game-header motion-path-header"/);
  assert.match(motionPath, /class="game-stats motion-path-stats"[^>]*\*ngIf="isFixationMode\(\)"/);
  assert.match(motionPath, /class="motion-path-actions"/);
  assert.match(scss, /\.motion-path-stats\s*\{[^}]*width:\s*auto/);
});

test('saccade does not render the empty grey statistics bar', () => {
  assert.match(motionPath, /class="game-stats motion-path-stats"[^>]*\*ngIf="isFixationMode\(\)"/);
  assert.match(scss, /\.motion-path-stats\s*\{[^}]*background:\s*transparent/);
});

test('exit confirmation renders an icon instead of the warning ligature', () => {
  assert.match(html, /class="material-icons exit-icon">warning<\/span>/);
  assert.doesNotMatch(html, /class="material-icons" class="exit-icon"/);
});

test('fixation describes rounds, exposure and response duration without a false verification label in preview', () => {
  assert.match(motionPath, /isFixationMode\(\) \? 'Tur' : 'Nokta'/);
  assert.match(motionPath, /getMotionPathFixationDuration\(\) \/ 1000 \| number:'1\.2-2'/);
  assert.match(motionPath, /Harfleri gördükten sonra tamamını yazın; sıra önemli değildir/);
  const result = html.split('isFixationMode() && result?.details?.averageResponseTimeMs != null')[1]?.split('measurement-not-available')[0] ?? '';
  assert.match(result, /averageResponseTimeMs \/ 1000 \| number:'1\.2-2'/);
  assert.match(result, /Tamamlanan \/ planlanan tur/);
  assert.doesNotMatch(result, /Doğrulanmış tur/);
  assert.doesNotMatch(html, /Egzersiz tamamlandı ve kaydedildi\. Bu egzersizdeki harf yanıtları/);
});
