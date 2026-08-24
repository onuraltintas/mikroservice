import { Routes } from '@angular/router';

export const publicRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./home/home.component').then(m => m.HomeComponent)
  },
  {
    path: 'hakkimizda',
    loadComponent: () => import('./about/about.component').then(m => m.AboutComponent)
  },
  {
    path: 'iletisim',
    loadComponent: () => import('./contact/contact.component').then(m => m.ContactComponent)
  },
  {
    path: 'odeme',
    loadComponent: () => import('./payment/payment.component').then(m => m.PaymentComponent)
  },
  {
    path: 'odeme-sonuc',
    loadComponent: () => import('./payment-success/payment-success.component').then(m => m.PaymentSuccessComponent)
  },
  {
    path: 'payment/success',
    redirectTo: 'odeme-sonuc',
    pathMatch: 'full'
  },
  {
    path: 'blog',
    children: [
      {
        path: '',
        loadComponent: () => import('./blog/blog-list/blog-list.component').then(m => m.BlogListComponent)
      },
      {
        path: ':slug',
        loadComponent: () => import('./blog/blog-detail/blog-detail.component').then(m => m.BlogDetailComponent)
      }
    ]
  },
  {
    path: 'sss',
    loadComponent: () => import('./faq/faq-page').then(m => m.FaqPageComponent)
  },
  {
    path: ':slug',
    loadComponent: () => import('./pages/dynamic-page/dynamic-page.component').then(m => m.DynamicPageComponent)
  }
];
