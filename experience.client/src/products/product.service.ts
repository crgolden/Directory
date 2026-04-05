import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { Product } from './product.model';

// TODO: Replace mock `of(...)` implementations with real HttpClient calls once
// the Products API is available at /api/products.
const MOCK_PRODUCTS: Product[] = [
  {
    id: '1',
    name: 'LG OLED C3 65"',
    brand: 'LG',
    modelNumber: 'OLED65C3PUA',
    serialNumber: 'SN-LG-001',
    purchaseDate: '2023-11-24',
    category: 'Electronics',
    description: '65-inch 4K OLED smart TV',
    manualUrl: '',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
  {
    id: '2',
    name: 'Honda Civic',
    brand: 'Honda',
    modelNumber: 'Civic Sport',
    serialNumber: '2HGFE2F59PH000001',
    purchaseDate: '2023-03-15',
    category: 'Vehicle',
    description: '2023 Honda Civic Sport sedan',
    manualUrl: '',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
  {
    id: '3',
    name: 'Dyson V15 Detect',
    brand: 'Dyson',
    modelNumber: 'V15 Detect Absolute',
    serialNumber: 'SN-DY-003',
    purchaseDate: '2024-01-10',
    category: 'Home Appliance',
    description: 'Cordless vacuum cleaner',
    manualUrl: '',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
];

@Injectable({ providedIn: 'root' })
export class ProductService {

  private readonly http = inject(HttpClient);

  getAll(): Observable<Product[]> {
    return of(MOCK_PRODUCTS);
  }

  getById(id: string): Observable<Product | undefined> {
    return of(MOCK_PRODUCTS.find(p => p.id === id));
  }

  create(product: Partial<Product>): Observable<Product> {
    const created: Product = {
      ...product as Product,
      id: crypto.randomUUID(),
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    return of(created);
  }

  update(id: string, product: Partial<Product>): Observable<Product> {
    return of({ ...product, id, updatedAt: new Date().toISOString() } as Product);
  }

  delete(_id: string): Observable<void> {
    return of(void 0);
  }
}
