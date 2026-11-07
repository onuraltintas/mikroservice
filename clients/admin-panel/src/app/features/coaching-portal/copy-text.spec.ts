import { vi } from 'vitest';
import { copyMessage, copyText } from './copy-text';

// G5 (#45) and PR 128 review L10: the Clipboard API first, then the older copy command, then only selecting the text.
describe('copyText', () => {
  const original = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
  afterEach(() => {
    if (original) Object.defineProperty(navigator, 'clipboard', original); else delete (navigator as { clipboard?: unknown }).clipboard;
    vi.restoreAllMocks(); delete (document as { execCommand?: unknown }).execCommand;
  });
  function code() { const element = document.createElement('strong'); element.textContent = 'AB12CD'; document.body.appendChild(element); return element; }

  it('uses the clipboard when it may', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    expect(await copyText('AB12CD', code())).toBe('copied');
    expect(writeText).toHaveBeenCalledWith('AB12CD');
  });

  it('falls back to the copy command on the selected text', async () => {
    Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true });
    const execCommand = vi.fn(() => true);
    Object.defineProperty(document, 'execCommand', { value: execCommand, configurable: true });
    expect(await copyText('AB12CD', code())).toBe('copied');
    expect(execCommand).toHaveBeenCalledWith('copy');
  });

  it('leaves the text selected when nothing can copy', async () => {
    Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true });
    Object.defineProperty(document, 'execCommand', { value: vi.fn(() => false), configurable: true });
    expect(await copyText('AB12CD', code())).toBe('selected');
    expect(window.getSelection()!.toString()).toBe('AB12CD');
    expect(copyMessage('selected')).toBe('Kodu seçip kopyalayabilirsin.');
    expect(copyMessage('failed')).toContain('Kodu seçip kopyalayabilirsin.');
  });
});
