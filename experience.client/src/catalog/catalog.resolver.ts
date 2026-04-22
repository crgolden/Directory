import { inject } from '@angular/core';
import { ResolveFn, Router } from '@angular/router';
import { catchError, EMPTY } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { Product } from '../products/product.model';
import { CatalogService } from './catalog.service';

export const catalogResolver: ResolveFn<Product> = route => {
  const catalogService = inject(CatalogService);
  const router = inject(Router);
  const id = route.paramMap.get('id')!;

  return catalogService.getById(id).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 404) {
        router.navigate(['/catalog/not-found']);
      } else {
        router.navigate(['/catalog']);
      }

      return EMPTY;
    })
  );
};
