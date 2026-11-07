import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { AUTH_COPY, AUTH_VOICE, authVoiceForHost } from './auth-voice';

// Paket F #12 (07.11.2026): "sen" on onuraltintas.net, "siz" on the eduivme.com management panel.
describe('authentication page voice', () => {
  it('uses siz only on the management hosts', () => {
    for (const host of ['eduivme.com', 'www.eduivme.com', 'eduivme.com.tr', 'www.eduivme.com.tr', 'EDUIVME.COM'])
      expect(authVoiceForHost(host)).toBe('siz');
    for (const host of ['onuraltintas.net', 'www.onuraltintas.net', 'staging.onuraltintas.net', 'localhost', '', null, 'noteduivme.com'])
      expect(authVoiceForHost(host)).toBe('sen');
  });

  it('reads the host of the page', () => {
    TestBed.configureTestingModule({ providers: [{ provide: DOCUMENT, useValue: { location: { hostname: 'www.eduivme.com' } } }] });
    expect(TestBed.inject(AUTH_VOICE)).toBe('siz');
  });

  it('has no formal address in the sen copy', () => {
    // Second person plural endings and imperatives ("hesabınız", "giriniz", "deneyin", "girin", "Lütfen").
    const formal = /(?:ınız|iniz|unuz|ünüz|yın|yin|yun|yün|girin|edin|seçin|Olun|Lütfen)(?=$|[\s.,!?…:;)])/i;
    for (const [key, text] of Object.entries(AUTH_COPY.sen)) expect(`${key}: ${text}`).not.toMatch(formal);
    expect(AUTH_COPY.sen.welcome).toBe('Hoş geldin 👋');
    expect(AUTH_COPY.sen.signUp).toBe('Kayıt ol');
    expect(AUTH_COPY.siz.welcome).toBe('Hoş Geldiniz 👋');
  });
});
