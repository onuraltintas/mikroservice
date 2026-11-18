import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { AUTH_COPY, AUTH_VOICE, authVoiceForHost } from './auth-voice';

// Paket F #12 (07.11.2026): "sen" on the Coaching sites, "siz" on the management panel (alan adı planı 18.11.2026).
describe('authentication page voice', () => {
  it('uses siz only on the management hosts', () => {
    for (const host of ['admin.eduivme.com', 'eduivme.com', 'www.eduivme.com', 'EDUIVME.COM'])
      expect(authVoiceForHost(host)).toBe('siz');
    for (const host of ['maxkoc.com', 'www.maxkoc.com', 'veli.maxkoc.com', 'ogretmen.eduivme.com', 'kurum.eduivme.com', 'localhost', '', null, 'noteduivme.com'])
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
