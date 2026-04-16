import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot } from '@angular/router';
import { of, throwError, firstValueFrom, EmptyError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { productResolver } from './product.resolver';
import { ProductService } from './product.service';
import { Product } from './product.model';

@Component({ template: '' })
class DummyComponent {}

const testRoutes = [
  { path: 'products', component: DummyComponent },
  { path: 'products/not-found', component: DummyComponent },
];

const mockProduct: Product = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  name: 'LG TV',
  price: 1299.99,
  brand: 'LG',
  modelNumber: 'OLED65C3',
  serialNumber: null,
  purchaseDate: null,
  category: null,
  description: null,
  manualUrl: null,
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: null,
};

function makeSnapshot(id: string): ActivatedRouteSnapshot {
  return { paramMap: { get: (key: string) => (key === 'id' ? id : null) } } as unknown as ActivatedRouteSnapshot;
}

describe('productResolver', () => {
  it('returns the product when getById succeeds', async () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: ProductService, useValue: { getById: () => of(mockProduct) } },
        provideRouter(testRoutes),
      ],
    });

    const result$ = TestBed.runInInjectionContext(() =>
      productResolver(makeSnapshot(mockProduct.id!), {} as RouterStateSnapshot)
    );

    const product = await firstValueFrom(result$);
    expect(product).toEqual(mockProduct);
  });

  it('navigates to /products/not-found when getById returns 404', async () => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ProductService,
          useValue: {
            getById: () =>
              throwError(() => new HttpErrorResponse({ status: 404, statusText: 'Not Found' })),
          },
        },
        provideRouter(testRoutes),
      ],
    });

    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const result$ = TestBed.runInInjectionContext(() =>
      productResolver(makeSnapshot('missing-id'), {} as RouterStateSnapshot)
    );

    await expect(firstValueFrom(result$)).rejects.toBeInstanceOf(EmptyError);
    expect(navigateSpy).toHaveBeenCalledWith(['/products/not-found']);
  });

  it('navigates to /products when getById returns a non-404 error', async () => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ProductService,
          useValue: {
            getById: () =>
              throwError(() => new HttpErrorResponse({ status: 500, statusText: 'Server Error' })),
          },
        },
        provideRouter(testRoutes),
      ],
    });

    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const result$ = TestBed.runInInjectionContext(() =>
      productResolver(makeSnapshot('any-id'), {} as RouterStateSnapshot)
    );

    await expect(firstValueFrom(result$)).rejects.toBeInstanceOf(EmptyError);
    expect(navigateSpy).toHaveBeenCalledWith(['/products']);
  });
});
