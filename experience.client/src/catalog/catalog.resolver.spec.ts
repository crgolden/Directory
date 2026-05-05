import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot } from '@angular/router';
import { of, throwError, firstValueFrom, EmptyError, Observable } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { catalogResolver } from './catalog.resolver';
import { CatalogService } from './catalog.service';
import { Product } from '../products/product.model';

@Component({ template: '' })
class DummyComponent {}

const testRoutes = [
  { path: 'catalog', component: DummyComponent },
  { path: 'catalog/not-found', component: DummyComponent },
];

const mockProduct: Product = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  name: 'LG TV',
  price: 1299.99,
  brand: 'LG',
  modelNumber: null,
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

describe('catalogResolver', () => {
  it('returns the product when getById succeeds', async () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: CatalogService, useValue: { getById: () => of(mockProduct) } },
        provideRouter(testRoutes),
      ],
    });

    const result$ = TestBed.runInInjectionContext(() =>
      catalogResolver(makeSnapshot(mockProduct.id), {} as RouterStateSnapshot)
    ) as Observable<Product>;

    const product = await firstValueFrom(result$);
    expect(product).toEqual(mockProduct);
  });

  it('navigates to /catalog/not-found when getById returns 404', async () => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: CatalogService,
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
      catalogResolver(makeSnapshot('missing-id'), {} as RouterStateSnapshot)
    ) as Observable<Product>;

    await expect(firstValueFrom(result$)).rejects.toBeInstanceOf(EmptyError);
    expect(navigateSpy).toHaveBeenCalledWith(['/catalog/not-found']);
  });

  it('navigates to /catalog when getById returns a non-404 error', async () => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: CatalogService,
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
      catalogResolver(makeSnapshot('any-id'), {} as RouterStateSnapshot)
    ) as Observable<Product>;

    await expect(firstValueFrom(result$)).rejects.toBeInstanceOf(EmptyError);
    expect(navigateSpy).toHaveBeenCalledWith(['/catalog']);
  });
});
