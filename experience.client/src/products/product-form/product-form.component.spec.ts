import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProductFormComponent } from './product-form.component';
import { ProductService } from '../product.service';
import { By } from '@angular/platform-browser';
import { provideRouter, Routes, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
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
      patch: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [ProductFormComponent],
      providers: [
        { provide: ProductService, useValue: mockService },
        provideRouter(testRoutes),
        provideHttpClient(),
        provideHttpClientTesting(),
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

  it('embeds the manual-chat panel (collapsed by default)', () => {
    const panel = fixture.debugElement.query(By.css('app-manual-chat-panel'));
    expect(panel).toBeTruthy();
    // Panel starts collapsed → only the toggle button is in the DOM, not the full chat UI.
    expect(fixture.debugElement.query(By.css('button.manual-chat-toggle'))).toBeTruthy();
    expect(fixture.debugElement.query(By.css('.manual-chat-panel'))).toBeNull();
  });

  it('onManualUrlSelected patches the manualUrl control and marks it dirty', () => {
    const component = fixture.componentInstance;
    component.onManualUrlSelected('https://example.com/manual.pdf');
    expect(component.form.controls.manualUrl.value).toBe('https://example.com/manual.pdf');
    expect(component.form.controls.manualUrl.dirty).toBe(true);
  });

  it('productContext() reflects the current form values', () => {
    const component = fixture.componentInstance;
    component.form.patchValue({ name: 'My Laptop', brand: 'Dell', modelNumber: 'XPS-15' });
    fixture.detectChanges();
    const ctx = component.productContext();
    expect(ctx.name).toBe('My Laptop');
    expect(ctx.brand).toBe('Dell');
    expect(ctx.modelNumber).toBe('XPS-15');
    expect(ctx.id).toBeNull();
  });
});

describe('ProductFormComponent — edit mode', () => {
  let fixture: ComponentFixture<ProductFormComponent>;
  let mockService: Partial<ProductService>;

  beforeEach(async () => {
    mockService = {
      getById: vi.fn(),
      create: vi.fn(),
      patch: vi.fn(() => of<void>(undefined)),
    };

    await TestBed.configureTestingModule({
      imports: [ProductFormComponent],
      providers: [
        { provide: ProductService, useValue: mockService },
        provideRouter(testRoutes),
        provideHttpClient(),
        provideHttpClientTesting(),
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

  it('productContext() includes the product id in edit mode', () => {
    const component = fixture.componentInstance;
    const ctx = component.productContext();
    expect(ctx.id).toBe('aaaaaaaa-0000-0000-0000-000000000042');
    expect(ctx.name).toBe('Test TV');
  });
});
