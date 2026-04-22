import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CatalogListComponent } from './catalog-list.component';
import { CatalogService } from '../catalog.service';
import { By } from '@angular/platform-browser';
import { provideRouter, Routes } from '@angular/router';
import { Component } from '@angular/core';
import { of } from 'rxjs';
import { Product } from '../../products/product.model';

@Component({ template: '' })
class DummyComponent {}

const testRoutes: Routes = [
  { path: 'catalog/:id', component: DummyComponent },
];

const mockProducts: Product[] = [
  {
    id: 'aaaaaaaa-0000-0000-0000-000000000001',
    name: 'Apple TV',
    price: 129.99,
    brand: 'Apple',
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
    name: 'Dyson V15',
    price: 499.99,
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

describe('CatalogListComponent', () => {
  let fixture: ComponentFixture<CatalogListComponent>;
  let mockService: Partial<CatalogService>;

  beforeEach(async () => {
    vi.useFakeTimers();

    mockService = {
      getAll: vi.fn(() => of({ items: mockProducts, total: 2 })),
    };

    await TestBed.configureTestingModule({
      imports: [CatalogListComponent],
      providers: [
        { provide: CatalogService, useValue: mockService },
        provideRouter(testRoutes),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogListComponent);
    fixture.detectChanges();
    await vi.runAllTimersAsync();
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders a row for each catalog item', () => {
    const rows = fixture.debugElement.queryAll(By.css('tbody tr'));
    expect(rows.length).toBe(2);
  });

  it('shows product name in row', () => {
    const firstRow = fixture.debugElement.queryAll(By.css('tbody tr'))[0];
    expect(firstRow.nativeElement.textContent).toContain('Apple TV');
  });

  it('renders the search input', () => {
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    expect(input).toBeTruthy();
  });

  it('shows showing text with total count', () => {
    const text = fixture.debugElement.query(By.css('small.text-muted'));
    expect(text.nativeElement.textContent).toContain('of 2');
  });

  it('Previous Page button is disabled on first page', () => {
    const buttons = fixture.debugElement.queryAll(By.css('button.btn-outline-secondary'));
    const prevBtn = buttons[0];
    expect(prevBtn.nativeElement.disabled).toBe(true);
  });

  it('Next Page button is disabled when total fits on one page', () => {
    const buttons = fixture.debugElement.queryAll(By.css('button.btn-outline-secondary'));
    const nextBtn = buttons[1];
    expect(nextBtn.nativeElement.disabled).toBe(true);
  });

  it('clicking Name column header calls getAll with orderBy Name', () => {
    const headerBtns = fixture.debugElement.queryAll(By.css('thead button'));
    headerBtns[0].nativeElement.click();
    fixture.detectChanges();

    expect(mockService.getAll).toHaveBeenCalledWith(
      expect.objectContaining({ orderBy: 'Name' })
    );
  });

  it('clicking the active Name column toggles orderDir to desc', () => {
    const nameBtn = fixture.debugElement.queryAll(By.css('thead button'))[0];

    // Component starts with Name asc — clicking toggles to desc.
    nameBtn.nativeElement.click();
    fixture.detectChanges();

    expect(mockService.getAll).toHaveBeenCalledWith(
      expect.objectContaining({ orderBy: 'Name', orderDir: 'desc' })
    );
  });

  it('clicking a different column resets direction to asc', () => {
    const brandBtn = fixture.debugElement.queryAll(By.css('thead button'))[1];
    brandBtn.nativeElement.click();
    fixture.detectChanges();

    expect(mockService.getAll).toHaveBeenCalledWith(
      expect.objectContaining({ orderBy: 'Brand', orderDir: 'asc' })
    );
  });

  it('typing in the search input calls getAll with the term after debounce', async () => {
    const input: HTMLInputElement = fixture.debugElement.query(By.css('input[type="search"]')).nativeElement;
    input.value = 'dyson';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    await vi.runAllTimersAsync();
    fixture.detectChanges();

    expect(mockService.getAll).toHaveBeenCalledWith(
      expect.objectContaining({ search: 'dyson' })
    );
  });

  it('shows no-match message when search returns empty list', async () => {
    (mockService.getAll as ReturnType<typeof vi.fn>).mockReturnValue(of({ items: [], total: 0 }));

    const input: HTMLInputElement = fixture.debugElement.query(By.css('input[type="search"]')).nativeElement;
    input.value = 'xyz';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    await vi.runAllTimersAsync();
    fixture.detectChanges();

    const alert = fixture.debugElement.query(By.css('.alert-secondary'));
    expect(alert.nativeElement.textContent).toContain('xyz');
  });

  it('Next Page button is enabled and navigates to page 2 when total exceeds page size', async () => {
    (mockService.getAll as ReturnType<typeof vi.fn>).mockReturnValue(of({ items: mockProducts, total: 25 }));
    fixture.componentInstance.total.set(25);
    fixture.detectChanges();

    const nextBtn = fixture.debugElement.queryAll(By.css('button.btn-outline-secondary'))[1];
    expect(nextBtn.nativeElement.disabled).toBe(false);

    nextBtn.nativeElement.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.page()).toBe(2);
    expect(mockService.getAll).toHaveBeenCalledWith(
      expect.objectContaining({ page: 2 })
    );
  });
});
