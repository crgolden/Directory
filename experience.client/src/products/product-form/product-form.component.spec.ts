import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProductFormComponent } from './product-form.component';
import { ProductService } from '../product.service';
import { By } from '@angular/platform-browser';
import { provideRouter, Routes, ActivatedRoute } from '@angular/router';
import { Component } from '@angular/core';
import { of } from 'rxjs';
import { Product } from '../product.model';

@Component({ template: '' })
class DummyComponent {}

const testRoutes: Routes = [
  { path: 'products', component: DummyComponent },
  { path: 'products/:id', component: DummyComponent },
  { path: 'products/not-found', component: DummyComponent },
];

const mockProduct: Product = {
  id: 'aaaaaaaa-0000-0000-0000-000000000042',
  name: 'Test TV',
  price: 999.99,
  brand: 'Sony',
  modelNumber: 'X90L',
  serialNumber: 'SN-001',
  purchaseDate: '2024-01-15T09:00:00Z',
  category: 'Electronics',
  description: null,
  manualUrl: null,
  createdAt: '2024-01-15T00:00:00Z',
  updatedAt: null,
};

describe('ProductFormComponent — create mode', () => {
  let fixture: ComponentFixture<ProductFormComponent>;
  let mockService: Partial<ProductService>;

  beforeEach(async () => {
    mockService = {
      getById: vi.fn(),
      create: vi.fn(() => of(mockProduct.id)),
      put: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [ProductFormComponent],
      providers: [
        { provide: ProductService, useValue: mockService },
        provideRouter(testRoutes),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductFormComponent);
    fixture.detectChanges();
  });

  it('submit button is disabled when name is empty', () => {
    const btn = fixture.debugElement.query(By.css('button[type="submit"]'));
    expect(btn.nativeElement.disabled).toBe(true);
  });

  it('submit button is enabled when name is filled', () => {
    const nameInput = fixture.debugElement.query(By.css('#name'));
    nameInput.nativeElement.value = 'My Product';
    nameInput.nativeElement.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const btn = fixture.debugElement.query(By.css('button[type="submit"]'));
    expect(btn.nativeElement.disabled).toBe(false);
  });

  it('submit calls ProductService.create in create mode', () => {
    const nameInput = fixture.debugElement.query(By.css('#name'));
    nameInput.nativeElement.value = 'My Product';
    nameInput.nativeElement.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.debugElement.query(By.css('form')).triggerEventHandler('ngSubmit');

    expect(mockService.create).toHaveBeenCalled();
  });

  it('renders the price field', () => {
    const priceInput = fixture.debugElement.query(By.css('#price'));
    expect(priceInput).toBeTruthy();
  });
});

describe('ProductFormComponent — edit mode', () => {
  let fixture: ComponentFixture<ProductFormComponent>;
  let mockService: Partial<ProductService>;

  beforeEach(async () => {
    mockService = {
      getById: vi.fn(),
      create: vi.fn(),
      patch: vi.fn(() => of(mockProduct)),
    };

    await TestBed.configureTestingModule({
      imports: [ProductFormComponent],
      providers: [
        { provide: ProductService, useValue: mockService },
        provideRouter(testRoutes),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: () => 'aaaaaaaa-0000-0000-0000-000000000042' },
              data: { product: mockProduct },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductFormComponent);
    fixture.detectChanges();
  });

  it('pre-populates the name field from the existing product', () => {
    const nameInput: HTMLInputElement = fixture.debugElement.query(By.css('#name')).nativeElement;
    expect(nameInput.value).toBe('Test TV');
  });

  it('pre-populates the price field', () => {
    const priceInput: HTMLInputElement = fixture.debugElement.query(By.css('#price')).nativeElement;
    expect(priceInput.value).toBe('999.99');
  });

  it('submit calls ProductService.patch in edit mode', () => {
    fixture.debugElement.query(By.css('form')).triggerEventHandler('ngSubmit');
    expect(mockService.patch).toHaveBeenCalledWith(
      'aaaaaaaa-0000-0000-0000-000000000042',
      expect.any(Object)
    );
  });
});
