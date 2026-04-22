import { TestBed } from '@angular/core/testing';
import {
  provideHttpClient,
  withInterceptorsFromDi,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { CatalogService, CatalogParams } from './catalog.service';
import { Product } from '../products/product.model';

const BASE = '/catalog/api/odata/Products';

const mockApiProduct = {
  Id: 'aaaaaaaa-0000-0000-0000-000000000001',
  Name: 'LG OLED C3',
  Price: 1299.99,
  Brand: 'LG',
  ModelNumber: 'OLED65C3PUA',
  SerialNumber: null,
  PurchaseDate: null,
  Category: 'Electronics',
  Description: null,
  ManualUrl: null,
  CreatedAt: '2024-01-01T00:00:00Z',
  UpdatedAt: null,
};

const mockProduct: Product = {
  id: mockApiProduct.Id,
  name: mockApiProduct.Name,
  price: mockApiProduct.Price,
  brand: mockApiProduct.Brand,
  modelNumber: mockApiProduct.ModelNumber,
  serialNumber: null,
  purchaseDate: null,
  category: mockApiProduct.Category,
  description: null,
  manualUrl: null,
  createdAt: mockApiProduct.CreatedAt,
  updatedAt: null,
};

const defaultParams: CatalogParams = {
  orderBy: 'Name',
  orderDir: 'asc',
  page: 1,
  pageSize: 20,
};

function params(urlWithParams: string): URLSearchParams {
  const idx = urlWithParams.indexOf('?');
  return new URLSearchParams(idx >= 0 ? urlWithParams.slice(idx + 1) : '');
}

describe('CatalogService', () => {
  let service: CatalogService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(CatalogService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('getAll', () => {
    it('sends $count=true', () => {
      service.getAll(defaultParams).subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      expect(params(req.request.urlWithParams).get('$count')).toBe('true');
      req.flush({ '@odata.count': 0, value: [] });
    });

    it('sends $orderby with direction', () => {
      service.getAll(defaultParams).subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      expect(params(req.request.urlWithParams).get('$orderby')).toBe('Name asc');
      req.flush({ '@odata.count': 0, value: [] });
    });

    it('sends $top and $skip for page 1', () => {
      service.getAll({ ...defaultParams, page: 1, pageSize: 20 }).subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      const p = params(req.request.urlWithParams);
      expect(p.get('$top')).toBe('20');
      expect(p.get('$skip')).toBe('0');
      req.flush({ '@odata.count': 0, value: [] });
    });

    it('sends correct $skip for page 2', () => {
      service.getAll({ ...defaultParams, page: 2, pageSize: 20 }).subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      expect(params(req.request.urlWithParams).get('$skip')).toBe('20');
      req.flush({ '@odata.count': 0, value: [] });
    });

    it('applies tolower contains $filter when search is provided', () => {
      service.getAll({ ...defaultParams, search: 'oled' }).subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      const filter = params(req.request.urlWithParams).get('$filter') ?? '';
      expect(filter).toContain("contains(tolower(Name), tolower('oled'))");
      req.flush({ '@odata.count': 0, value: [] });
    });

    it('does not include $filter when search is empty', () => {
      service.getAll({ ...defaultParams, search: '' }).subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      expect(params(req.request.urlWithParams).has('$filter')).toBe(false);
      req.flush({ '@odata.count': 0, value: [] });
    });

    it('unwraps the OData envelope and maps PascalCase response to Product', async () => {
      const promise = firstValueFrom(service.getAll(defaultParams));

      http.expectOne(r => r.urlWithParams.startsWith(BASE)).flush({
        '@odata.count': 1,
        value: [mockApiProduct],
      });

      const page = await promise;
      expect(page.items.length).toBe(1);
      expect(page.items[0]).toEqual(mockProduct);
    });

    it('returns the total count from @odata.count', async () => {
      const promise = firstValueFrom(service.getAll(defaultParams));

      http.expectOne(r => r.urlWithParams.startsWith(BASE)).flush({
        '@odata.count': 42,
        value: [],
      });

      const page = await promise;
      expect(page.total).toBe(42);
    });

    it('defaults total to 0 when @odata.count is absent', async () => {
      const promise = firstValueFrom(service.getAll(defaultParams));

      http.expectOne(r => r.urlWithParams.startsWith(BASE)).flush({ value: [] });

      const page = await promise;
      expect(page.total).toBe(0);
    });

    it('sends desc orderDir correctly', () => {
      service.getAll({ ...defaultParams, orderBy: 'Price', orderDir: 'desc' }).subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      expect(params(req.request.urlWithParams).get('$orderby')).toBe('Price desc');
      req.flush({ '@odata.count': 0, value: [] });
    });
  });

  describe('getById', () => {
    it('requests the keyed OData entity URL', () => {
      service.getById(mockProduct.id).subscribe();

      const req = http.expectOne(`${BASE}(${mockProduct.id})`);
      req.flush(mockApiProduct);
    });

    it('maps PascalCase API response to Product', async () => {
      const promise = firstValueFrom(service.getById(mockProduct.id));

      http.expectOne(`${BASE}(${mockProduct.id})`).flush(mockApiProduct);

      const product = await promise;
      expect(product.id).toBe(mockProduct.id);
      expect(product.name).toBe('LG OLED C3');
    });
  });
});
