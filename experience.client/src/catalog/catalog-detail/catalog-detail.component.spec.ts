import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CatalogDetailComponent } from './catalog-detail.component';
import { By } from '@angular/platform-browser';
import { provideRouter, Routes, ActivatedRoute } from '@angular/router';
import { Component } from '@angular/core';
import { Product } from '../../products/product.model';

@Component({ template: '' })
class DummyComponent {}

const testRoutes: Routes = [
  { path: 'catalog', component: DummyComponent },
];

const mockProduct: Product = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  name: 'Sony TV',
  price: 999.99,
  brand: 'Sony',
  modelNumber: 'XR55A80K',
  serialNumber: 'SN-SONY-001',
  purchaseDate: '2023-11-24T14:30:00Z',
  category: 'Electronics',
  description: null,
  manualUrl: null,
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: null,
};

describe('CatalogDetailComponent', () => {
  let fixture: ComponentFixture<CatalogDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CatalogDetailComponent],
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

    fixture = TestBed.createComponent(CatalogDetailComponent);
    fixture.detectChanges();
  });

  it('renders the product name', () => {
    const h2 = fixture.debugElement.query(By.css('h2'));
    expect(h2.nativeElement.textContent).toContain('Sony TV');
  });

  it('renders brand, model number, and serial number', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Sony');
    expect(text).toContain('XR55A80K');
    expect(text).toContain('SN-SONY-001');
  });

  it('shows a Back to Catalog link', () => {
    const backLink = fixture.debugElement.query(By.css('a.btn-outline-secondary'));
    expect(backLink).toBeTruthy();
    expect(backLink.nativeElement.textContent).toContain('Back to Catalog');
  });

  it('does not show Edit or Delete buttons', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Edit');
    expect(text).not.toContain('Delete');
  });

  it('does not show View Manual button when manualUrl is null', () => {
    const manualBtn = fixture.debugElement.query(By.css('a.btn-primary'));
    expect(manualBtn).toBeNull();
  });
});

describe('CatalogDetailComponent — with manualUrl', () => {
  let fixture: ComponentFixture<CatalogDetailComponent>;

  const productWithManual: Product = {
    ...mockProduct,
    manualUrl: 'https://example.com/sony-tv-manual.pdf',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CatalogDetailComponent],
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

    fixture = TestBed.createComponent(CatalogDetailComponent);
    fixture.detectChanges();
  });

  it('renders the View Manual button linking to manualUrl', () => {
    const link = fixture.debugElement.query(By.css('a.btn-primary[target="_blank"]'));
    expect(link).toBeTruthy();
    expect(link.nativeElement.getAttribute('href')).toBe(productWithManual.manualUrl);
    expect(link.nativeElement.textContent).toContain('View Manual');
  });
});
