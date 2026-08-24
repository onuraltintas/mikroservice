/** @type {import('tailwindcss').Config} */
module.exports = {
  /**
   * Content scanning — ONLY student feature files.
   * Admin/teacher panelleri Tailwind kullanmıyor, gereksiz class
   * üretimini engellemek için scope daraltıldı.
   */
  content: [
    './src/app/features/student/**/*.{html,ts,scss}',
    './src/app/features/student/ui/**/*.{html,ts,scss}',
  ],

  /**
   * preflight: false — Angular Material'ın kendi CSS reset'i var.
   * Tailwind'in base reset'i (normalize) Material bileşenlerini bozar.
   * Utility class'lar eksiksiz çalışır, sadece reset devre dışı.
   */
  corePlugins: {
    preflight: false,
  },

  theme: {
    extend: {
      /**
       * CSS custom property'lere bağlı renkler.
       * Tailwind class'ları (bg-sp-primary, text-sp-text-1 vb.) çalışırken
       * tema değişkenlerinden otomatik beslenir.
       */
      colors: {
        sp: {
          primary:       'var(--sp-primary)',
          'primary-light': 'var(--sp-primary-light)',
          'primary-dark':  'var(--sp-primary-dark)',
          'primary-ghost': 'var(--sp-primary-ghost)',
          success:       'var(--sp-success)',
          'success-light': 'var(--sp-success-light)',
          warning:       'var(--sp-warning)',
          'warning-light': 'var(--sp-warning-light)',
          danger:        'var(--sp-danger)',
          surface:       'var(--sp-surface)',
          'surface-2':   'var(--sp-surface-2)',
          'surface-3':   'var(--sp-surface-3)',
          border:        'var(--sp-border)',
          'border-strong': 'var(--sp-border-strong)',
          'text-1':      'var(--sp-text-1)',
          'text-2':      'var(--sp-text-2)',
          'text-3':      'var(--sp-text-3)',
          'text-inv':    'var(--sp-text-inv)',
          sidebar:       'var(--sp-sidebar-bg)',
          'sidebar-hover': 'var(--sp-sidebar-hover)',
          'sidebar-active': 'var(--sp-sidebar-active)',
          'sidebar-text': 'var(--sp-sidebar-text)',
          'sidebar-text-muted': 'var(--sp-sidebar-text-muted)',
        },
      },

      fontFamily: {
        inter: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
      },

      fontSize: {
        'sp-xs':   ['0.75rem',  { lineHeight: '1rem' }],
        'sp-sm':   ['0.875rem', { lineHeight: '1.25rem' }],
        'sp-base': ['1rem',     { lineHeight: '1.5rem' }],
        'sp-lg':   ['1.125rem', { lineHeight: '1.75rem' }],
        'sp-xl':   ['1.25rem',  { lineHeight: '1.75rem' }],
        'sp-2xl':  ['1.5rem',   { lineHeight: '2rem' }],
        'sp-3xl':  ['1.875rem', { lineHeight: '2.25rem' }],
        'sp-4xl':  ['2.25rem',  { lineHeight: '2.5rem' }],
      },

      borderRadius: {
        'sp-sm':   '8px',
        'sp-md':   '12px',
        'sp-lg':   '16px',
        'sp-xl':   '20px',
        'sp-2xl':  '24px',
        'sp-full': '9999px',
      },

      boxShadow: {
        'sp-sm':    '0 1px 2px 0 rgb(0 0 0 / 0.05)',
        'sp-md':    '0 1px 3px 0 rgb(0 0 0 / 0.07), 0 4px 16px 0 rgb(0 0 0 / 0.07)',
        'sp-lg':    '0 4px 6px -1px rgb(0 0 0 / 0.07), 0 10px 30px -3px rgb(0 0 0 / 0.1)',
        'sp-xl':    '0 20px 40px -5px rgb(0 0 0 / 0.12)',
        'sp-card':  '0 1px 3px rgb(0 0 0 / 0.06), 0 4px 16px rgb(0 0 0 / 0.08)',
        'sp-hover': '0 4px 6px rgb(0 0 0 / 0.06), 0 12px 24px rgb(0 0 0 / 0.1)',
        'sp-glow-primary': '0 0 0 3px var(--sp-primary-ghost)',
        'sp-glow-success': '0 0 0 3px var(--sp-success-light)',
        'sp-inset': 'inset 0 2px 4px 0 rgb(0 0 0 / 0.06)',
      },

      spacing: {
        'sp-xs':  '0.25rem',  // 4px
        'sp-sm':  '0.5rem',   // 8px
        'sp-md':  '1rem',     // 16px
        'sp-lg':  '1.5rem',   // 24px
        'sp-xl':  '2rem',     // 32px
        'sp-2xl': '3rem',     // 48px
        'sp-3xl': '4rem',     // 64px
        '18':     '4.5rem',   // 72px - sidebar icon area
        '68':     '17rem',    // 272px - sidebar width
        '72':     '18rem',    // 288px
        '84':     '21rem',
        '96':     '24rem',
      },

      transitionTimingFunction: {
        'sp-spring': 'cubic-bezier(0.34, 1.56, 0.64, 1)',
        'sp-ease':   'cubic-bezier(0.4, 0, 0.2, 1)',
      },

      transitionDuration: {
        'sp-fast':   '150ms',
        'sp-normal': '250ms',
        'sp-slow':   '400ms',
      },

      keyframes: {
        'sp-shimmer': {
          '0%':   { transform: 'translateX(-100%)' },
          '100%': { transform: 'translateX(100%)' },
        },
        'sp-fade-in': {
          '0%':   { opacity: '0', transform: 'translateY(8px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'sp-scale-in': {
          '0%':   { opacity: '0', transform: 'scale(0.95)' },
          '100%': { opacity: '1', transform: 'scale(1)' },
        },
        'sp-pulse-ring': {
          '0%':   { transform: 'scale(1)',    opacity: '1' },
          '100%': { transform: 'scale(1.4)',  opacity: '0' },
        },
        'sp-streak-bounce': {
          '0%, 100%': { transform: 'translateY(0) rotate(-5deg)' },
          '50%':      { transform: 'translateY(-6px) rotate(5deg)' },
        },
      },

      animation: {
        'sp-shimmer':       'sp-shimmer 2s infinite',
        'sp-fade-in':       'sp-fade-in 0.3s cubic-bezier(0.4, 0, 0.2, 1) both',
        'sp-scale-in':      'sp-scale-in 0.2s cubic-bezier(0.4, 0, 0.2, 1) both',
        'sp-pulse-ring':    'sp-pulse-ring 1.5s cubic-bezier(0.24, 0, 0.38, 1) infinite',
        'sp-streak-bounce': 'sp-streak-bounce 2s ease-in-out infinite',
        'sp-spin-slow':     'spin 3s linear infinite',
      },

      backgroundImage: {
        'sp-gradient-primary': 'linear-gradient(135deg, var(--sp-primary) 0%, var(--sp-primary-light) 100%)',
        'sp-gradient-success': 'linear-gradient(135deg, var(--sp-success) 0%, var(--sp-success-light) 100%)',
        'sp-gradient-sidebar': 'linear-gradient(180deg, var(--sp-sidebar-bg) 0%, var(--sp-sidebar-bg-end) 100%)',
        'sp-gradient-card':    'linear-gradient(145deg, rgba(255,255,255,0.6) 0%, rgba(255,255,255,0.1) 100%)',
        'sp-gradient-hero':    'linear-gradient(135deg, #6366f1 0%, #8b5cf6 50%, #a855f7 100%)',
        'sp-dots':             'radial-gradient(circle, var(--sp-border) 1px, transparent 1px)',
      },

      gridTemplateColumns: {
        'sp-dashboard':  'repeat(auto-fill, minmax(280px, 1fr))',
        'sp-exercises':  'repeat(auto-fill, minmax(240px, 1fr))',
        'sp-stats':      'repeat(auto-fill, minmax(160px, 1fr))',
      },
    },
  },

  plugins: [],
};
