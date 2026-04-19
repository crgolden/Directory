import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProductDetailComponent } from './product-detail.component';
import { By } from '@angular/platform-browser';
import { provideRouter, Routes, ActivatedRoute } from '@angular/router';
import { Component } from '@angular/core';
import { Product } from '../product.model';

@Component({ template: '' })
class DummyComponent {}

const testRoutes: Routes = [
  { path: 'products', component: DummyComponent },
  { path: 'products/not-found', component: DummyComponent },
  { path: 'products/:id/edit', component: DummyComponent },
];

const mockProduct: Product = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  name: 'LG TV',
  price: 1299.99,
  brand: 'LG',
  modelNumber: 'OLED65C3',
  serialNumber: 'SN-001',
  purchaseDate: '2023-11-24T14:30:00Z',
  category: 'Electronics',
  description: null,
  manualUrl: null,
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: null,
};

describe('ProductDetailComponent', () => {
  let fixture: ComponentFixture<ProductDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductDetailComponent],
      providers: [
        provideRouter(testRoutes),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: () => mockProduct.id },
              data: { product: mockProduct },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductDetailComponent);
    fixture.detectChanges();
  });

  it('renders the product name', () => {
    const h2 = fixture.debugElement.query(By.css('h2'));
    expect(h2.nativeElement.textContent).toContain('LG TV');
  });

  it('renders brand, model number, and serial number', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('LG');
    expect(text).toContain('OLED65C3');
    expect(text).toContain('SN-001');
  });

  it('without manualUrl, shows a "Find Manual" link that routes to the edit form', () => {
    const links = fixture.debugElement.queryAll(By.css('a.btn-outline-primary'));
    const findManual = links.find(l => (l.nativeElement.textContent as string).includes('Find Manual'));
    expect(findManual).toBeTruthy();
    expect(findManual!.nativeElement.getAttribute('href')).toContain(`/products/${mockProduct.id}/edit`);
  });
});

describe('ProductDetailComponent — with manualUrl', () => {
  let fixture: ComponentFixture<ProductDetailComponent>;

  const productWithManual: Product = {
    ...mockProduct,
    manualUrl: 'https://example.com/lg-tv-manual.pdf',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductDetailComponent],
      providers: [
        provideRouter(testRoutes),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: () => productWithManual.id },
              data: { product: productWithManual },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductDetailComponent);
    fixture.detectChanges();
  });

  it('renders "View Manual" button linking to manualUrl', () => {
    const link = fixture.debugElement.query(By.css('a.btn-primary[target="_blank"]'));
    expect(link).toBeTruthy();
    expect(link.nativeElement.getAttribute('href')).toBe(productWithManual.manualUrl);
    expect(link.nativeElement.textContent).toContain('View Manual');
  });

  it('shows a plain "Edit" action instead of "Find Manual"', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Find Manual');
  });
});
