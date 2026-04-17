import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import buildQuery from 'odata-query';
import { ODataResponse, Product } from './product.model';

const BASE = '/products/api/odata/Products';

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

  create(product: Partial<Product>): Observable<string> {
    return this.http.post<Product>(BASE, product, { observe: 'response' }).pipe(
      map((response: HttpResponse<Product>) => {
        const location = response.headers.get('Location') ?? '';
        const match = location.match(/\(([^)]+)\)$/);
        return match?.[1] ?? '';
      })
    );
  }

  patch(id: string, changes: Partial<Product>): Observable<Product> {
    return this.http.patch<Product>(`${BASE}(${id})`, changes);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${BASE}(${id})`);
  }
}
