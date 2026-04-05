import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { ProductService } from './product.service';
import { firstValueFrom } from 'rxjs';

describe('ProductService', () => {
  let service: ProductService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient()] });
    service = TestBed.inject(ProductService);
  });

  it('getAll returns the mock product array', async () => {
    const products = await firstValueFrom(service.getAll());
    expect(products.length).toBeGreaterThan(0);
  });

  it('create returns a new product with a generated id', async () => {
    const product = await firstValueFrom(service.create({ name: 'Test Item', brand: 'ACME' }));
    expect(product.id).toBeTruthy();
    expect(product.name).toBe('Test Item');
  });

  it('update returns the product with the given id', async () => {
    const product = await firstValueFrom(service.update('42', { name: 'Updated' }));
    expect(product.id).toBe('42');
    expect(product.name).toBe('Updated');
  });

  it('delete completes without error', async () => {
    await expect(firstValueFrom(service.delete('1'))).resolves.toBeUndefined();
  });
});
