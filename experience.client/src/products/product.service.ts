import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import buildQuery from 'odata-query';
import { ODataResponse, Product } from './product.model';

const BASE = '/products/api/odata/Products';

// The Products OData API returns PascalCase property names per its EDM model.
interface ApiProduct {
  Id: string;
  Name: string | null;
  Price: number | null;
  Brand: string | null;
  ModelNumber: string | null;
  SerialNumber: string | null;
  PurchaseDate: string | null;
  Category: string | null;
  Description: string | null;
  ManualUrl: string | null;
  CreatedAt: string;
  UpdatedAt: string | null;
}

function fromApi(raw: ApiProduct): Product {
  return {
    id: raw.Id,
    name: raw.Name,
    price: raw.Price,
    brand: raw.Brand,
    modelNumber: raw.ModelNumber,
    serialNumber: raw.SerialNumber,
    purchaseDate: raw.PurchaseDate,
    category: raw.Category,
    description: raw.Description,
    manualUrl: raw.ManualUrl,
    createdAt: raw.CreatedAt,
    updatedAt: raw.UpdatedAt,
  };
}

@Injectable({ providedIn: 'root' })
export class ProductService {

  private readonly http = inject(HttpClient);

  getAll(search?: string): Observable<Product[]> {
    const filter = search?.trim()
      ? `contains(tolower(Name), tolower('${search.trim()}'))`
      : undefined;
    const qs = buildQuery({ filter, orderBy: 'Name' });
    return this.http
      .get<ODataResponse<ApiProduct>>(`${BASE}${qs}`)
      .pipe(map(r => r.value.map(fromApi)));
  }

  getById(id: string): Observable<Product> {
    return this.http.get<ApiProduct>(`${BASE}(${id})`).pipe(map(fromApi));
  }

  create(product: Partial<Product>): Observable<string> {
    return this.http.post<ApiProduct>(BASE, product, { observe: 'response' }).pipe(
      map((response: HttpResponse<ApiProduct>) => {
        const location = response.headers.get('Location') ?? '';
        const match = location.match(/\(([^)]+)\)$/);
        return match?.[1] ?? '';
      })
    );
  }

  patch(id: string, changes: Partial<Product>): Observable<Product> {
    return this.http.patch<ApiProduct>(`${BASE}(${id})`, changes).pipe(map(fromApi));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${BASE}(${id})`);
  }
}
