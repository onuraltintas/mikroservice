export const environment = {
    production: true,
    // Roadmap O2: where teachers sign in (the separate staff portal).
    staffPortalUrl: 'https://ogretmen.eduivme.com/staff/',
    // Alan adı planı (18.11.2026): institution managers sign in on their own address.
    institutionPortalUrl: 'https://kurum.eduivme.com/staff/',
    apiUrl: '/api',
    // This is a public identifier. Restrict its authorized origins in Google
    // Cloud Console; provision a dedicated production client before go-live.
    googleClientId: '503453099078-s04ol7rissjlt59sc86lo3ef79igg4mj.apps.googleusercontent.com'
};
