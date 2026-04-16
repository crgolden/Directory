import { inject } from '@angular/core';
import { ResolveFn, Router } from '@angular/router';
import { catchError, EMPTY } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { Product } from './product.model';
import { ProductService } from './product.service';

export const productResolver: ResolveFn<Product> = route => {
  const productService = inject(ProductService);
  const router = inject(Router);
  const id = route.paramMap.get('id')!;

  return productService.getById(id).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 404) {
        router.navigate(['/products/not-found']);
      } else {
        router.navigate(['/products']);
      }

      return EMPTY;
    })
  );
};
