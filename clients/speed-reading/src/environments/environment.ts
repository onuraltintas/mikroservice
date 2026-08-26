// Development environment - uses Angular proxy
// See proxy.conf.json for proxy configuration
export const environment = {
  production: false,
  apiUrl: '/api',  // Proxy redirects /api to the local Gateway
  speedReadingApiUrl: '/api/speed-reading',
  googleClientId: '503453099078-s04ol7rissjlt59sc86lo3ef79igg4mj.apps.googleusercontent.com',
  vapidPublicKey: 'BDXTL-5iQzjdtDzUdtbPDU5bwYmrbmconj_j6e4M91hR1S5TpOH9m8TQOhJrdtpHKxmMOt1uugbSL1rynlVqoUI'
};
