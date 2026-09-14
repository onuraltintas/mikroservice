import { isTrustedMapEmbedUrl } from './trusted-resource-url';

describe('isTrustedMapEmbedUrl', () => {
  it('only accepts HTTPS Google Maps embed URLs', () => {
    expect(isTrustedMapEmbedUrl('https://www.google.com/maps/embed?pb=test')).toBeTrue();
    expect(isTrustedMapEmbedUrl('javascript:alert(1)')).toBeFalse();
    expect(isTrustedMapEmbedUrl('https://example.com/embed')).toBeFalse();
  });
});
