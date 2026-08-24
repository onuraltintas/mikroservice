export interface DifficultyLevel {
  level: number;
  name: string;
  stars: string;
  description: string;
  icon: string;
  color: string;
  characteristics: string[];
  recommended?: boolean;
  settings?: any;
}

export const DIFFICULTY_LEVELS: Record<string, DifficultyLevel[]> = {
  // Tachistoscope zorluk seviyeleri
  'tachistoscope': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: 'Yavaş flaş - Tek basit kelime',
      icon: 'school',
      color: '#4caf50',
      characteristics: [
        '500ms flaş süresi',
        'Tek kelime',
        'Basit kelimeler (ev, okul)',
        'Geri bildirim aktif'
      ]
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: 'Orta hız - Uzun kelimeler',
      icon: 'local_library',
      color: '#8bc34a',
      characteristics: [
        '300ms flaş süresi',
        'Tek kelime',
        'Uzun kelimeler (bilgisayar, telefon)',
        'Geri bildirim aktif'
      ]
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: 'Hızlı flaş - Karmaşık kelimeler',
      icon: 'trending_up',
      color: '#ff9800',
      characteristics: [
        '200ms flaş süresi',
        'Tek kelime',
        'Karmaşık kelimeler (teknoloji, demokrasi)',
        'Geri bildirim aktif'
      ],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: 'Çok hızlı - Kelime grupları',
      icon: 'rocket_launch',
      color: '#f44336',
      characteristics: [
        '100ms flaş süresi',
        '2 kelime grupları',
        'Hızlı okuma, görsel algı',
        'Geri bildirim aktif'
      ]
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Ultra hızlı RSVP',
      icon: 'military_tech',
      color: '#9c27b0',
      characteristics: [
        '50ms flaş süresi (!)',
        '3 kelime grupları',
        'Çok uzun kelime grupları',
        'Geri bildirim yok'
      ]
    }
  ],

  // SchulteTable zorluk seviyeleri
  'schulte-table': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: '3x3 grid - Yavaş tempo',
      icon: 'grid_3x3',
      color: '#4caf50',
      characteristics: ['3x3 grid', 'Sayılar 1-9', 'Sınırsız süre']
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: '4x4 grid - Orta tempo',
      icon: 'grid_4x4',
      color: '#8bc34a',
      characteristics: ['4x4 grid', 'Sayılar 1-16', '60 saniye hedef']
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: '5x5 grid - Hızlı tempo',
      icon: 'view_comfy',
      color: '#ff9800',
      characteristics: ['5x5 grid', 'Sayılar 1-25', '45 saniye hedef'],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: '6x6 grid - Çok hızlı',
      icon: 'dashboard',
      color: '#f44336',
      characteristics: ['6x6 grid', 'Sayılar 1-36', '30 saniye hedef']
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: '7x7 grid - Expert',
      icon: 'military_tech',
      color: '#9c27b0',
      characteristics: ['7x7 grid', 'Sayılar 1-49', '20 saniye hedef']
    }
  ],

  // Eye Tracking zorluk seviyeleri
  'eye-tracking': [
    {
      level: 1,
      name: 'Yavaş',
      stars: '⭐',
      description: 'Yatay hareket',
      icon: 'trending_flat',
      color: '#4caf50',
      characteristics: ['Yatay pattern', 'Yavaş hız (speed: 1)', 'Büyük hedef (50px)']
    },
    {
      level: 2,
      name: 'Orta',
      stars: '⭐⭐',
      description: 'Dikey hareket',
      icon: 'unfold_more',
      color: '#8bc34a',
      characteristics: ['Dikey pattern', 'Orta hız (speed: 2)', 'Orta hedef (40px)']
    },
    {
      level: 3,
      name: 'Hızlı',
      stars: '⭐⭐⭐',
      description: 'Çapraz hareket',
      icon: 'open_in_full',
      color: '#ff9800',
      characteristics: ['Çapraz pattern', 'Hızlı (speed: 3)', 'Küçük hedef (30px)'],
      recommended: true
    },
    {
      level: 4,
      name: 'Çok Hızlı',
      stars: '⭐⭐⭐⭐',
      description: 'Dairesel',
      icon: 'motion_photos_on',
      color: '#f44336',
      characteristics: ['Dairesel pattern', 'Çok hızlı (speed: 4)', 'Çok küçük hedef (25px)']
    },
    {
      level: 5,
      name: 'Master',
      stars: '⭐⭐⭐⭐⭐',
      description: '8 şekli + Zigzag',
      icon: 'all_out',
      color: '#9c27b0',
      characteristics: ['Karmaşık patternler', 'Maksimum hız (speed: 5)', 'Mini hedef (20px)']
    }
  ],

  // Saccade Exercise zorluk seviyeleri
  'saccade': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: 'Kısa mesafe',
      icon: 'arrow_range',
      color: '#4caf50',
      characteristics: ['200px mesafe', '1000ms interval', 'Yavaş tempo']
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: 'Orta mesafe',
      icon: 'swap_horiz',
      color: '#8bc34a',
      characteristics: ['300px mesafe', '800ms interval', 'Orta tempo']
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: 'Uzun mesafe',
      icon: 'compare_arrows',
      color: '#ff9800',
      characteristics: ['400px mesafe', '600ms interval', 'Hızlı tempo'],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: 'Çok uzun',
      icon: 'open_in_full',
      color: '#f44336',
      characteristics: ['500px mesafe', '400ms interval', 'Çok hızlı']
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Maksimum',
      icon: 'alt_route',
      color: '#9c27b0',
      characteristics: ['600px mesafe', '200ms interval', 'Ultra hızlı']
    }
  ],

  // Fixation Exercise zorluk seviyeleri
  'fixation': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: '3x3 grid',
      icon: 'grid_3x3',
      color: '#4caf50',
      characteristics: ['3x3 grid', '2000ms sabitleme', '9 nokta']
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: '4x4 grid',
      icon: 'grid_4x4',
      color: '#8bc34a',
      characteristics: ['4x4 grid', '1500ms sabitleme', '16 nokta']
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: '5x5 grid',
      icon: 'view_module',
      color: '#ff9800',
      characteristics: ['5x5 grid', '1000ms sabitleme', '25 nokta'],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: '6x6 grid',
      icon: 'dashboard',
      color: '#f44336',
      characteristics: ['6x6 grid', '800ms sabitleme', '36 nokta']
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: '7x7 grid',
      icon: 'view_comfy',
      color: '#9c27b0',
      characteristics: ['7x7 grid', '500ms sabitleme', '49 nokta']
    }
  ],

  // Regression Reduction zorluk seviyeleri
  'regression-reduction': [
    {
      level: 1,
      name: 'Yavaş',
      stars: '⭐',
      description: 'Yavaş scroll',
      icon: 'speed',
      color: '#4caf50',
      characteristics: ['50 px/s scroll', 'Kısa metin', 'Geriye bakış algılama']
    },
    {
      level: 2,
      name: 'Orta',
      stars: '⭐⭐',
      description: 'Orta scroll',
      icon: 'fast_forward',
      color: '#8bc34a',
      characteristics: ['100 px/s scroll', 'Orta metin', 'Geriye bakış uyarısı']
    },
    {
      level: 3,
      name: 'Hızlı',
      stars: '⭐⭐⭐',
      description: 'Hızlı scroll',
      icon: 'rocket',
      color: '#ff9800',
      characteristics: ['150 px/s scroll', 'Uzun metin', 'Minimum uyarı'],
      recommended: true
    },
    {
      level: 4,
      name: 'Çok Hızlı',
      stars: '⭐⭐⭐⭐',
      description: 'Çok hızlı scroll',
      icon: 'bolt',
      color: '#f44336',
      characteristics: ['200 px/s scroll', 'Çok uzun metin', 'Uyarı yok']
    },
    {
      level: 5,
      name: 'Maksimum',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Ultra hızlı',
      icon: 'flash_on',
      color: '#9c27b0',
      characteristics: ['300 px/s scroll', 'Maksimum metin', 'Zorlayıcı']
    }
  ],

  // Skimming zorluk seviyeleri
  'skimming': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: 'Kısa paragraf',
      icon: 'article',
      color: '#4caf50',
      characteristics: ['Kısa paragraf', '3 anahtar kelime', 'Bol süre']
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: 'Orta paragraf',
      icon: 'description',
      color: '#8bc34a',
      characteristics: ['Orta paragraf', '5 anahtar kelime', 'Yeterli süre']
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: 'Uzun paragraf',
      icon: 'subject',
      color: '#ff9800',
      characteristics: ['Uzun paragraf', '7 anahtar kelime', 'Orta süre'],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: 'Çok uzun metin',
      icon: 'menu_book',
      color: '#f44336',
      characteristics: ['Çok uzun metin', '10 anahtar kelime', 'Az süre']
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Maksimum metin',
      icon: 'auto_stories',
      color: '#9c27b0',
      characteristics: ['Maksimum metin', '15 anahtar kelime', 'Zaman baskısı']
    }
  ],

  // Speed Reading zorluk seviyeleri
  'speed-reading': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: '200 kelime/dk',
      icon: 'trending_up',
      color: '#4caf50',
      characteristics: ['200 kelime/dk', 'Basit metin', 'Kolay sorular']
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: '300 kelime/dk',
      icon: 'speed',
      color: '#8bc34a',
      characteristics: ['300 kelime/dk', 'Orta metin', 'Orta sorular']
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: '400 kelime/dk',
      icon: 'fast_forward',
      color: '#ff9800',
      characteristics: ['400 kelime/dk', 'Karmaşık metin', 'Zor sorular'],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: '500 kelime/dk',
      icon: 'rocket_launch',
      color: '#f44336',
      characteristics: ['500 kelime/dk', 'Akademik metin', 'Çok zor sorular']
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: '600+ kelime/dk',
      icon: 'flash_on',
      color: '#9c27b0',
      characteristics: ['600+ kelime/dk', 'İleri seviye metin', 'Uzman sorular']
    }
  ],

  // Chunking zorluk seviyeleri
  'chunking': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: '2 kelime',
      icon: 'view_column',
      color: '#4caf50',
      characteristics: ['2 kelime grupları', 'Yavaş tempo', 'Basit cümleler']
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: '3 kelime',
      icon: 'view_agenda',
      color: '#8bc34a',
      characteristics: ['3 kelime grupları', 'Orta tempo', 'Normal cümleler']
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: '4 kelime',
      icon: 'view_module',
      color: '#ff9800',
      characteristics: ['4 kelime grupları', 'Hızlı tempo', 'Karmaşık cümleler'],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: '5 kelime',
      icon: 'dashboard',
      color: '#f44336',
      characteristics: ['5 kelime grupları', 'Çok hızlı', 'Uzun cümleler']
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: '6 kelime',
      icon: 'view_comfy',
      color: '#9c27b0',
      characteristics: ['6 kelime grupları', 'Ultra hızlı', 'Maksimum zorluk']
    }
  ],

  // Text Fading zorluk seviyeleri
  'text-fading': [
    {
      level: 1,
      name: 'Yavaş',
      stars: '⭐',
      description: 'Yavaş kaybolma',
      icon: 'opacity',
      color: '#4caf50',
      characteristics: ['5s fade süresi', 'Kısa metin', 'Kolay']
    },
    {
      level: 2,
      name: 'Orta',
      stars: '⭐⭐',
      description: 'Orta kaybolma',
      icon: 'blur_on',
      color: '#8bc34a',
      characteristics: ['3s fade süresi', 'Orta metin', 'Normal']
    },
    {
      level: 3,
      name: 'Hızlı',
      stars: '⭐⭐⭐',
      description: 'Hızlı kaybolma',
      icon: 'blur_linear',
      color: '#ff9800',
      characteristics: ['2s fade süresi', 'Uzun metin', 'Zor'],
      recommended: true
    },
    {
      level: 4,
      name: 'Çok Hızlı',
      stars: '⭐⭐⭐⭐',
      description: 'Çok hızlı',
      icon: 'auto_fix_high',
      color: '#f44336',
      characteristics: ['1s fade süresi', 'Çok uzun metin', 'Çok zor']
    },
    {
      level: 5,
      name: 'Flash',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Flash kaybolma',
      icon: 'flash_on',
      color: '#9c27b0',
      characteristics: ['0.5s fade', 'Maksimum metin', 'Ultra zor']
    }
  ],

  // Scanning zorluk seviyeleri
  'scanning': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: 'Az yoğunluk',
      icon: 'search',
      color: '#4caf50',
      characteristics: ['Az kelime', '1 hedef', 'Bol süre']
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: 'Orta yoğunluk',
      icon: 'find_in_page',
      color: '#8bc34a',
      characteristics: ['Orta kelime', '2 hedef', 'Normal süre']
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: 'Yoğun metin',
      icon: 'pageview',
      color: '#ff9800',
      characteristics: ['Çok kelime', '3 hedef', 'Az süre'],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: 'Çok yoğun',
      icon: 'manage_search',
      color: '#f44336',
      characteristics: ['Yoğun metin', '4 hedef', 'Çok az süre']
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Maksimum',
      icon: 'travel_explore',
      color: '#9c27b0',
      characteristics: ['Maksimum yoğunluk', '5 hedef', 'Zaman baskısı']
    }
  ],

  // Visual Expansion zorluk seviyeleri
  'visual-expansion': [
    {
      level: 1,
      name: 'Dar',
      stars: '⭐',
      description: 'Dar alan',
      icon: 'crop_free',
      color: '#4caf50',
      characteristics: ['Dar görüş alanı', 'Az kelime', 'Yavaş']
    },
    {
      level: 2,
      name: 'Orta',
      stars: '⭐⭐',
      description: 'Orta alan',
      icon: 'aspect_ratio',
      color: '#8bc34a',
      characteristics: ['Orta alan', 'Orta kelime', 'Normal']
    },
    {
      level: 3,
      name: 'Geniş',
      stars: '⭐⭐⭐',
      description: 'Geniş alan',
      icon: 'open_in_full',
      color: '#ff9800',
      characteristics: ['Geniş alan', 'Çok kelime', 'Hızlı'],
      recommended: true
    },
    {
      level: 4,
      name: 'Çok Geniş',
      stars: '⭐⭐⭐⭐',
      description: 'Çok geniş',
      icon: 'fit_screen',
      color: '#f44336',
      characteristics: ['Çok geniş alan', 'Maksimum kelime', 'Çok hızlı']
    },
    {
      level: 5,
      name: 'Panorama',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Panoramik',
      icon: 'panorama',
      color: '#9c27b0',
      characteristics: ['Tam genişlik', 'Ultra yoğun', 'Ultra hızlı']
    }
  ],

  // Visualization zorluk seviyeleri
  'visualization': [
    {
      level: 1,
      name: 'Basit',
      stars: '⭐',
      description: 'Basit görsel',
      icon: 'image',
      color: '#4caf50',
      characteristics: ['Basit görsel', 'Az detay', 'Uzun süre']
    },
    {
      level: 2,
      name: 'Orta',
      stars: '⭐⭐',
      description: 'Orta karmaşık',
      icon: 'photo',
      color: '#8bc34a',
      characteristics: ['Orta görsel', 'Orta detay', 'Normal süre']
    },
    {
      level: 3,
      name: 'Karmaşık',
      stars: '⭐⭐⭐',
      description: 'Karmaşık görsel',
      icon: 'collections',
      color: '#ff9800',
      characteristics: ['Karmaşık görsel', 'Çok detay', 'Az süre'],
      recommended: true
    },
    {
      level: 4,
      name: 'Çok Karmaşık',
      stars: '⭐⭐⭐⭐',
      description: 'Çok karmaşık',
      icon: 'burst_mode',
      color: '#f44336',
      characteristics: ['Çok karmaşık', 'Maksimum detay', 'Çok az süre']
    },
    {
      level: 5,
      name: 'Ultra',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Ultra karmaşık',
      icon: 'auto_awesome',
      color: '#9c27b0',
      characteristics: ['Ultra karmaşık', 'Soyut', 'Minimum süre']
    }
  ],

  // Subvocalization Reduction zorluk seviyeleri
  'subvocalization-reduction': [
    {
      level: 1,
      name: 'Yavaş',
      stars: '⭐',
      description: 'Yavaş okuma',
      icon: 'volume_off',
      color: '#4caf50',
      characteristics: ['Yavaş hız', 'Az dikkat dağıtıcı', 'Kolay metin']
    },
    {
      level: 2,
      name: 'Orta',
      stars: '⭐⭐',
      description: 'Orta hız',
      icon: 'volume_mute',
      color: '#8bc34a',
      characteristics: ['Orta hız', 'Orta dikkat dağıtıcı', 'Normal metin']
    },
    {
      level: 3,
      name: 'Hızlı',
      stars: '⭐⭐⭐',
      description: 'Hızlı okuma',
      icon: 'voice_over_off',
      color: '#ff9800',
      characteristics: ['Hızlı', 'Çok dikkat dağıtıcı', 'Zor metin'],
      recommended: true
    },
    {
      level: 4,
      name: 'Çok Hızlı',
      stars: '⭐⭐⭐⭐',
      description: 'Çok hızlı',
      icon: 'speaker_notes_off',
      color: '#f44336',
      characteristics: ['Çok hızlı', 'Maksimum dikkat dağıtıcı', 'Çok zor']
    },
    {
      level: 5,
      name: 'Sessiz',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Tam sessiz',
      icon: 'mic_off',
      color: '#9c27b0',
      characteristics: ['Ultra hızlı', 'Kaos seviyesi', 'Ultra zor']
    }
  ],

  // Focus Exercise zorluk seviyeleri
  'focus': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: 'Az dikkat dağıtıcı',
      icon: 'center_focus_weak',
      color: '#4caf50',
      characteristics: ['1 dikkat dağıtıcı', 'Uzun süre', 'Kolay']
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: 'Orta dikkat dağıtıcı',
      icon: 'center_focus_strong',
      color: '#8bc34a',
      characteristics: ['2 dikkat dağıtıcı', 'Orta süre', 'Normal']
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: 'Çok dikkat dağıtıcı',
      icon: 'filter_center_focus',
      color: '#ff9800',
      characteristics: ['3 dikkat dağıtıcı', 'Az süre', 'Zor'],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: 'Maksimum dikkat dağıtıcı',
      icon: 'adjust',
      color: '#f44336',
      characteristics: ['4 dikkat dağıtıcı', 'Çok az süre', 'Çok zor']
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Kaos',
      icon: 'blur_circular',
      color: '#9c27b0',
      characteristics: ['5+ dikkat dağıtıcı', 'Minimum süre', 'Ultra zor']
    }
  ],

  // Default zorluk seviyeleri (diğer egzersizler için)
  'default': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: 'Başlangıç seviyesi',
      icon: 'school',
      color: '#4caf50',
      characteristics: ['Yavaş tempo', 'Basit içerik', 'Tam yardım']
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: 'Temel seviye',
      icon: 'local_library',
      color: '#8bc34a',
      characteristics: ['Orta tempo', 'Standart içerik', 'Normal yardım']
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: 'Orta seviye',
      icon: 'trending_up',
      color: '#ff9800',
      characteristics: ['Hızlı tempo', 'Karmaşık içerik', 'Az yardım'],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: 'İleri seviye',
      icon: 'rocket_launch',
      color: '#f44336',
      characteristics: ['Çok hızlı tempo', 'Zor içerik', 'Minimum yardım']
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Uzman seviye',
      icon: 'military_tech',
      color: '#9c27b0',
      characteristics: ['Ultra hızlı', 'Maksimum zorluk', 'Yardım yok']
    }
  ],

  // Exam Simulation zorluk seviyeleri
  'exam-simulation': [
    {
      level: 1,
      name: 'Başlangıç',
      stars: '⭐',
      description: 'Basit paragraflar',
      icon: 'article',
      color: '#4caf50',
      characteristics: ['Kısa paragraflar', 'Açık sorular', 'Bol süre']
    },
    {
      level: 2,
      name: 'Temel',
      stars: '⭐⭐',
      description: 'Orta paragraflar',
      icon: 'description',
      color: '#8bc34a',
      characteristics: ['Orta uzunluk', 'Normal sorular', 'Yeterli süre']
    },
    {
      level: 3,
      name: 'Orta',
      stars: '⭐⭐⭐',
      description: 'Standart sınav',
      icon: 'quiz',
      color: '#2196f3',
      characteristics: ['Sınav formatı', 'Orta zorluk', 'Normal süre'],
      recommended: true
    },
    {
      level: 4,
      name: 'İleri',
      stars: '⭐⭐⭐⭐',
      description: 'Zor sorular',
      icon: 'school',
      color: '#ff9800',
      characteristics: ['Karmaşık paragraflar', 'Zor sorular', 'Az süre']
    },
    {
      level: 5,
      name: 'Uzman',
      stars: '⭐⭐⭐⭐⭐',
      description: 'Akademik seviye',
      icon: 'military_tech',
      color: '#9c27b0',
      characteristics: ['Akademik metinler', 'Çok zor sorular', 'Zaman baskısı']
    }
  ]
};

/**
 * Get difficulty levels for a specific exercise type
 * @param exerciseTypeName Name of the exercise type (e.g., 'Tachistoscope', 'SchulteTable')
 * @returns Array of difficulty levels for that exercise type
 */
export function getDifficultyLevels(exerciseTypeName: string): DifficultyLevel[] {
  // Convert to lowercase and replace spaces with hyphens
  const key = exerciseTypeName.toLowerCase().replace(/\s+/g, '-');
  return DIFFICULTY_LEVELS[key] || DIFFICULTY_LEVELS['default'];
}

/**
 * Get a specific difficulty level by number
 * @param exerciseTypeName Name of the exercise type
 * @param level Difficulty level number (1-5)
 * @returns The specific difficulty level or undefined
 */
export function getDifficultyLevel(exerciseTypeName: string, level: number): DifficultyLevel | undefined {
  const levels = getDifficultyLevels(exerciseTypeName);
  return levels.find(l => l.level === level);
}

/**
 * Get the recommended difficulty level for an exercise type
 * @param exerciseTypeName Name of the exercise type
 * @returns The recommended difficulty level or level 3 as default
 */
export function getRecommendedLevel(exerciseTypeName: string): DifficultyLevel {
  const levels = getDifficultyLevels(exerciseTypeName);
  return levels.find(l => l.recommended) || levels[2]; // Default to level 3 if no recommended
}
