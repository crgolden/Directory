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
import { ProductService } from './product.service';
import { ODataResponse, Product } from './product.model';

const BASE = '/api/products/odata/Products';

const mockProduct: Product = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  name: 'LG OLED C3',
  price: 1299.99,
  brand: 'LG',
  modelNumber: 'OLED65C3PUA',
  serialNumber: 'SN-LG-001',
  purchaseDate: '2023-11-24T14:30:00Z',
  category: 'Electronics',
  description: '65-inch 4K OLED smart TV',
  manualUrl: null,
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: null,
};

describe('ProductService', () => {
  let service: ProductService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(ProductService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  // Helper: parse query params from a URL string the way odata-query appends them.
  function params(urlWithParams: string): URLSearchParams {
    const idx = urlWithParams.indexOf('?');
    return new URLSearchParams(idx >= 0 ? urlWithParams.slice(idx + 1) : '');
  }

  describe('getAll', () => {
    it('requests the OData Products collection ordered by Name', () => {
      const envelope: ODataResponse<Product> = { value: [mockProduct] };

      service.getAll().subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      expect(params(req.request.urlWithParams).get('$orderby')).toBe('Name');
      req.flush(envelope);
    });

    it('unwraps the OData value envelope and returns the array', async () => {
      const envelope: ODataResponse<Product> = { value: [mockProduct] };
      const promise = firstValueFrom(service.getAll());

      http.expectOne(r => r.urlWithParams.startsWith(BASE)).flush(envelope);

      const products = await promise;
      expect(products.length).toBe(1);
      expect(products[0].id).toBe(mockProduct.id);
    });

    it('applies a tolower contains $filter when search is provided', () => {
      const envelope: ODataResponse<Product> = { value: [] };

      service.getAll('oled').subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      const filter = params(req.request.urlWithParams).get('$filter') ?? '';
      expect(filter).toContain("contains(tolower(Name), tolower('oled'))");
      req.flush(envelope);
    });

    it('does not include $filter when search is empty', () => {
      const envelope: ODataResponse<Product> = { value: [] };

      service.getAll('').subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      expect(params(req.request.urlWithParams).has('$filter')).toBe(false);
      req.flush(envelope);
    });

    it('trims whitespace from the search term', () => {
      const envelope: ODataResponse<Product> = { value: [] };

      service.getAll('  dyson  ').subscribe();

      const req = http.expectOne(r => r.urlWithParams.startsWith(BASE));
      const filter = params(req.request.urlWithParams).get('$filter') ?? '';
      expect(filter).toContain("tolower('dyson')");
      req.flush(envelope);
    });
  });

  describe('getById', () => {
    it('requests the keyed OData entity URL', () => {
      service.getById(mockProduct.id).subscribe();

      const req = http.expectOne(`${BASE}(${mockProduct.id})`);
      req.flush(mockProduct);
    });

    it('returns the product', async () => {
      const promise = firstValueFrom(service.getById(mockProduct.id));

      http.expectOne(`${BASE}(${mockProduct.id})`).flush(mockProduct);

      const product = await promise;
      expect(product.name).toBe('LG OLED C3');
    });
  });

  describe('create', () => {
    it('POSTs to the collection URL', () => {
      service.create({ name: 'New Item' }).subscribe();

      const req = http.expectOne(BASE);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ name: 'New Item' });
      req.flush(mockProduct);
    });

    it('returns the created product from the server', async () => {
      const promise = firstValueFrom(service.create({ name: 'New Item' }));

      http.expectOne(BASE).flush(mockProduct);

      const product = await promise;
      expect(product.id).toBe(mockProduct.id);
    });
  });

  describe('put', () => {
    it('PUTs to the keyed entity URL with the full product', () => {
      service.put(mockProduct.id, mockProduct).subscribe();

      const req = http.expectOne(`${BASE}(${mockProduct.id})`);
      expect(req.request.method).toBe('PUT');
      req.flush(mockProduct);
    });
  });

  describe('patch', () => {
    it('PATCHes to the keyed entity URL with only the changed fields', () => {
      service.patch(mockProduct.id, { name: 'Updated Name' }).subscribe();

      const req = http.expectOne(`${BASE}(${mockProduct.id})`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ name: 'Updated Name' });
      req.flush(mockProduct);
    });
  });

  describe('delete', () => {
    it('sends DELETE to the keyed entity URL', () => {
      service.delete(mockProduct.id).subscribe();

      const req = http.expectOne(`${BASE}(${mockProduct.id})`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('completes without error on 204', async () => {
      const promise = firstValueFrom(service.delete(mockProduct.id));

      http.expectOne(`${BASE}(${mockProduct.id})`).flush(
        null,
        { status: 204, statusText: 'No Content' }
      );

      await expect(promise).resolves.toBeNull();
    });
  });
});
