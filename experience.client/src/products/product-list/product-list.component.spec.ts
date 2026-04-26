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
    id: 'aaaaaaaa-0000-0000-0000-000000000001',
    name: 'TV',
    price: 999.99,
    brand: 'LG',
    modelNumber: null,
    serialNumber: null,
    purchaseDate: null,
    category: 'Electronics',
    description: null,
    manualUrl: null,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: null,
  },
  {
    id: 'aaaaaaaa-0000-0000-0000-000000000002',
    name: 'Vacuum',
    price: null,
    brand: 'Dyson',
    modelNumber: null,
    serialNumber: null,
    purchaseDate: null,
    category: 'Home',
    description: null,
    manualUrl: null,
    createdAt: '2024-01-02T00:00:00Z',
    updatedAt: null,
  },
];

describe('ProductListComponent', () => {
  let fixture: ComponentFixture<ProductListComponent>;
  let mockService: Partial<ProductService>;

  beforeEach(async () => {
    vi.useFakeTimers();

    mockService = {
      getAll: vi.fn(() => of(mockProducts)),
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

    // detectChanges triggers ngOnInit which emits on search$ → debounceTime(300)
    fixture.detectChanges();

    // Advance fake clock past the initial debounce so the product list is populated
    await vi.runAllTimersAsync();
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders a row for each product', () => {
    const rows = fixture.debugElement.queryAll(By.css('tbody tr'));
    expect(rows.length).toBe(2);
  });

  it('shows product name in row', () => {
    const firstRow = fixture.debugElement.queryAll(By.css('tbody tr'))[0];
    expect(firstRow.nativeElement.textContent).toContain('TV');
  });

  it('renders the search input', () => {
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    expect(input).toBeTruthy();
  });

  it('typing in the search input calls ProductService.getAll with the term', async () => {
    const input: HTMLInputElement = fixture.debugElement.query(By.css('input[type="search"]')).nativeElement;
    input.value = 'vacuum';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    await vi.runAllTimersAsync();
    fixture.detectChanges();

    expect(mockService.getAll).toHaveBeenCalledWith('vacuum');
  });

  it('shows no-match message when search returns empty list', async () => {
    (mockService.getAll as ReturnType<typeof vi.fn>).mockReturnValue(of([]));

    const input: HTMLInputElement = fixture.debugElement.query(By.css('input[type="search"]')).nativeElement;
    input.value = 'xyz';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    await vi.runAllTimersAsync();
    fixture.detectChanges();

    const emptyState = fixture.debugElement.query(By.css('.empty-state'));
    expect(emptyState.nativeElement.textContent).toContain('xyz');
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

    expect(mockService.delete).toHaveBeenCalledWith('aaaaaaaa-0000-0000-0000-000000000001');
  });
});
