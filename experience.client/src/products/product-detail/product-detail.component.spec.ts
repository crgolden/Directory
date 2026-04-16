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
  { path: 'chat', component: DummyComponent },
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

  it('"Find Manual" link encodes product name, brand, and model into ?q=', () => {
    const component = fixture.componentInstance;
    const query = component.findManualQuery(mockProduct);
    expect(query).toContain('LG TV');
    expect(query).toContain('LG');
    expect(query).toContain('OLED65C3');
  });
});
