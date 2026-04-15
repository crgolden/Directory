import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import buildQuery from 'odata-query';
import { ODataResponse, Product } from './product.model';

const BASE = '/products/odata/Products';

@Injectable({ providedIn: 'root' })
export class ProductService {

  private readonly http = inject(HttpClient);

  getAll(search?: string): Observable<Product[]> {
    const filter = search?.trim()
      ? `contains(tolower(Name), tolower('${search.trim()}'))`
      : undefined;
    const qs = buildQuery({ filter, orderBy: 'Name' });
    return this.http
      .get<ODataResponse<Product>>(`${BASE}${qs}`)
      .pipe(map(r => r.value));
  }

  getById(id: string): Observable<Product> {
    return this.http.get<Product>(`${BASE}(${id})`);
  }

  create(product: Partial<Product>): Observable<Product> {
    return this.http.post<Product>(BASE, product);
  }

  put(id: string, product: Partial<Product>): Observable<Product> {
    return this.http.put<Product>(`${BASE}(${id})`, product);
  }

  patch(id: string, changes: Partial<Product>): Observable<Product> {
    return this.http.patch<Product>(`${BASE}(${id})`, changes);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${BASE}(${id})`);
  }
}
