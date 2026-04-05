import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProductListComponent } from './product-list.component';
import { ProductService } from '../product.service';
import { By } from '@angular/platform-browser';
import { provideRouter, Routes } from '@angular/router';
import { Component } from '@angular/core';
import { of } from 'rxjs';
import { Product } from '../product.model';

@Component({ template: '' })
class DummyComponent {}

const testRoutes: Routes = [
  { path: 'products/new', component: DummyComponent },
  { path: 'products/:id', component: DummyComponent },
  { path: 'products/:id/edit', component: DummyComponent },
];

const mockProducts: Product[] = [
  {
    id: '1', name: 'TV', brand: 'LG', category: 'Electronics',
    createdAt: '', updatedAt: ''
  },
  {
    id: '2', name: 'Vacuum', brand: 'Dyson', category: 'Home',
    createdAt: '', updatedAt: ''
  },
];

describe('ProductListComponent', () => {
  let fixture: ComponentFixture<ProductListComponent>;
  let mockService: Partial<ProductService>;

  beforeEach(async () => {
    mockService = {
      getAll: () => of(mockProducts),
      delete: vi.fn(() => of(void 0)),
    };

    await TestBed.configureTestingModule({
      imports: [ProductListComponent],
      providers: [
        { provide: ProductService, useValue: mockService },
        provideRouter(testRoutes),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductListComponent);
    fixture.detectChanges();
  });

  it('renders a row for each product', () => {
    const rows = fixture.debugElement.queryAll(By.css('tbody tr'));
    expect(rows.length).toBe(2);
  });

  it('shows product name in row', () => {
    const firstRow = fixture.debugElement.queryAll(By.css('tbody tr'))[0];
    expect(firstRow.nativeElement.textContent).toContain('TV');
  });

  it('clicking Delete shows inline confirmation', () => {
    const deleteBtn = fixture.debugElement.queryAll(By.css('button.btn-outline-danger'))[0];
    deleteBtn.nativeElement.click();
    fixture.detectChanges();

    const confirmText = fixture.debugElement.query(By.css('.text-danger'));
    expect(confirmText.nativeElement.textContent).toContain('Delete?');
  });

  it('confirming delete calls ProductService.delete', () => {
    const deleteBtn = fixture.debugElement.queryAll(By.css('button.btn-outline-danger'))[0];
    deleteBtn.nativeElement.click();
    fixture.detectChanges();

    const yesBtn = fixture.debugElement.query(By.css('button.btn-danger'));
    yesBtn.nativeElement.click();

    expect(mockService.delete).toHaveBeenCalledWith('1');
  });
});
