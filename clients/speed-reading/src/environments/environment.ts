// Development environment - uses Angular proxy
// See proxy.conf.json for proxy configuration
export const environment = {
  production: false,
  apiUrl: '/api',  // Proxy redirects /api to the local Gateway
  speedReadingApiUrl: '/api/speed-reading',
  centralAdminUrl: 'https://admin.eduivme.com/dashboard',
  centralAdminLoginUrl: 'https://admin.eduivme.com/auth/login',
  // Canonical address of the public site; www and other hosts are folded onto it (27.11.2026).
  publicSiteUrl: 'https://masterhizliokuma.com',
  // Alan adı planı (18.11.2026): every group has its own address.
  coachingSiteUrl: 'https://maxkoc.com',
  parentSiteUrl: 'https://veli.maxkoc.com',
  teacherPortalUrl: 'https://ogretmen.eduivme.com/staff/',
  institutionPortalUrl: 'https://kurum.eduivme.com/staff/',
  googleClientId: '503453099078-s04ol7rissjlt59sc86lo3ef79igg4mj.apps.googleusercontent.com',
  vapidPublicKey: 'BDXTL-5iQzjdtDzUdtbPDU5bwYmrbmconj_j6e4M91hR1S5TpOH9m8TQOhJrdtpHKxmMOt1uugbSL1rynlVqoUI'
};
