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

const testRoutes: Routes = [{ path: 'products', component: DummyComponent }];

const mockProduct: Product = {
  id: '42', name: 'Test TV', brand: 'Sony', modelNumber: 'X90L',
  serialNumber: 'SN-001', category: 'Electronics',
  createdAt: '', updatedAt: ''
};

describe('ProductFormComponent — create mode', () => {
  let fixture: ComponentFixture<ProductFormComponent>;
  let mockService: Partial<ProductService>;

  beforeEach(async () => {
    mockService = {
      getById: vi.fn(),
      create: vi.fn(() => of(mockProduct)),
      update: vi.fn(),
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

  it('submit button is enabled when name is filled', async () => {
    const nameInput = fixture.debugElement.query(By.css('#name'));
    nameInput.nativeElement.value = 'My Product';
    nameInput.nativeElement.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const btn = fixture.debugElement.query(By.css('button[type="submit"]'));
    expect(btn.nativeElement.disabled).toBe(false);
  });

  it('submit calls ProductService.create in create mode', async () => {
    const nameInput = fixture.debugElement.query(By.css('#name'));
    nameInput.nativeElement.value = 'My Product';
    nameInput.nativeElement.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.debugElement.query(By.css('form')).triggerEventHandler('ngSubmit');

    expect(mockService.create).toHaveBeenCalled();
  });
});

describe('ProductFormComponent — edit mode', () => {
  let fixture: ComponentFixture<ProductFormComponent>;
  let mockService: Partial<ProductService>;

  beforeEach(async () => {
    mockService = {
      getById: vi.fn(() => of(mockProduct)),
      create: vi.fn(),
      update: vi.fn(() => of(mockProduct)),
    };

    await TestBed.configureTestingModule({
      imports: [ProductFormComponent],
      providers: [
        { provide: ProductService, useValue: mockService },
        provideRouter(testRoutes),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '42' } } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductFormComponent);
    fixture.detectChanges();
  });

  it('pre-populates the name field from the existing product', () => {
    const nameInput: HTMLInputElement = fixture.debugElement.query(By.css('#name')).nativeElement;
    expect(nameInput.value).toBe('Test TV');
  });

  it('submit calls ProductService.update in edit mode', () => {
    fixture.debugElement.query(By.css('form')).triggerEventHandler('ngSubmit');
    expect(mockService.update).toHaveBeenCalledWith('42', expect.any(Object));
  });
});
