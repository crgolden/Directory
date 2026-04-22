import { Routes } from '@angular/router';
import { authGuard } from '../auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('../home/home.component').then(m => m.HomeComponent),
  },
  {
    path: 'catalog',
    loadChildren: () =>
      import('../catalog/catalog.routes').then(m => m.catalogRoutes),
  },
  {
    path: 'products',
    canActivate: [authGuard],
    loadChildren: () =>
      import('../products/products.routes').then(m => m.productRoutes),
  },
  {
    path: 'user-session',
    loadComponent: () =>
      import('../user-session/user-session.component').then(m => m.UserSessionComponent),
  },
];
