import { Routes } from '@angular/router';

export const productRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./product-list/product-list.component').then(m => m.ProductListComponent),
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./product-form/product-form.component').then(m => m.ProductFormComponent),
  },
  {
    path: 'not-found',
    loadComponent: () =>
      import('./product-not-found/product-not-found.component').then(m => m.ProductNotFoundComponent),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./product-detail/product-detail.component').then(m => m.ProductDetailComponent),
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./product-form/product-form.component').then(m => m.ProductFormComponent),
  },
];
