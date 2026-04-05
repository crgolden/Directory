import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProductDetailComponent } from './product-detail.component';
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
  { path: 'chat', component: DummyComponent },
  { path: 'products/:id/edit', component: DummyComponent },
];

const mockProduct: Product = {
  id: '1',
  name: 'LG TV',
  brand: 'LG',
  modelNumber: 'OLED65C3',
  serialNumber: 'SN-001',
  category: 'Electronics',
  createdAt: '',
  updatedAt: '',
};

describe('ProductDetailComponent', () => {
  let fixture: ComponentFixture<ProductDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductDetailComponent],
      providers: [
        {
          provide: ProductService,
          useValue: { getById: () => of(mockProduct) },
        },
        provideRouter(testRoutes),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => '1' } } },
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
    const link: HTMLAnchorElement = fixture.debugElement.query(By.css('a[routerLink="/chat"]'))?.nativeElement
      ?? fixture.debugElement.queryAll(By.css('a')).find(a => a.nativeElement.textContent.includes('Find Manual'))?.nativeElement;
    expect(link).toBeTruthy();

    const component = fixture.componentInstance;
    const query = component.findManualQuery(mockProduct);
    expect(query).toContain('LG TV');
    expect(query).toContain('LG');
    expect(query).toContain('OLED65C3');
  });
});
