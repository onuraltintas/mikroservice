import { describe, expect, it } from 'vitest';
import {
  filterCalibrationSegments,
  measurementDataScopeLabel
} from './speed-reading-content-configuration';

describe('measurement data scope helpers', () => {
  const routine = { studyCode: null };
  const research = { studyCode: 'PILOT-2026' };

  it('keeps routine measurements separate from explicitly enrolled research measurements', () => {
    expect(filterCalibrationSegments([routine, research], 'routine')).toEqual([routine]);
    expect(filterCalibrationSegments([routine, research], 'research')).toEqual([research]);
    expect(filterCalibrationSegments([routine, research], 'all')).toEqual([routine, research]);
  });

  it('uses clear Turkish labels for the available measurement scopes', () => {
    expect(measurementDataScopeLabel('all')).toBe('Tüm ölçümler');
    expect(measurementDataScopeLabel('routine')).toBe('Rutin kullanım');
    expect(measurementDataScopeLabel('research')).toBe('Araştırma katılımı');
  });
});
