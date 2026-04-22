import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import buildQuery from 'odata-query';
import { Product } from '../products/product.model';

const BASE = '/catalog/api/odata/Products';

export interface CatalogParams {
  search?: string;
  orderBy: string;
  orderDir: 'asc' | 'desc';
  page: number;
  pageSize: number;
}

export interface CatalogPage {
  items: Product[];
  total: number;
}

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

interface ODataCountResponse<T> {
  '@odata.count'?: number;
  value: T[];
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
export class CatalogService {

  private readonly http = inject(HttpClient);

  getAll(params: CatalogParams): Observable<CatalogPage> {
    const filter = params.search?.trim()
      ? `contains(tolower(Name), tolower('${params.search.trim()}'))`
      : undefined;
    const skip = (params.page - 1) * params.pageSize;
    const qs = buildQuery({
      filter,
      orderBy: `${params.orderBy} ${params.orderDir}`,
      top: params.pageSize,
      skip,
      count: true,
    });
    return this.http
      .get<ODataCountResponse<ApiProduct>>(`${BASE}${qs}`)
      .pipe(map(r => ({ items: r.value.map(fromApi), total: r['@odata.count'] ?? 0 })));
  }

  getById(id: string): Observable<Product> {
    return this.http.get<ApiProduct>(`${BASE}(${id})`).pipe(map(fromApi));
  }
}
