// Development environment - uses Angular proxy
// See proxy.conf.json for proxy configuration
export const environment = {
  production: false,
  apiUrl: '/api',  // Proxy redirects /api to the local Gateway
  speedReadingApiUrl: '/api/speed-reading',
  googleClientId: '419291322983-u9dg9ajc2us1qn5bl3gqb07k7bc6ubtg.apps.googleusercontent.com',
  vapidPublicKey: 'BDXTL-5iQzjdtDzUdtbPDU5bwYmrbmconj_j6e4M91hR1S5TpOH9m8TQOhJrdtpHKxmMOt1uugbSL1rynlVqoUI'
};
