import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import { ProductService } from '../product.service';
import { Product } from '../product.model';

@Component({
  selector: 'app-product-list',
  imports: [RouterLink, FormsModule],
  templateUrl: './product-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductListComponent implements OnInit {

  private readonly titleService = inject(Title);
  private readonly productService = inject(ProductService);

  readonly products = signal<Product[]>([]);
  readonly confirmingDeleteId = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly loading = signal(false);

  private readonly search$ = new Subject<string>();

  ngOnInit(): void {
    this.titleService.setTitle('Experience | My Products');

    this.search$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => {
        this.loading.set(true);
        return this.productService.getAll(term);
      })
    ).subscribe(p => {
      this.products.set(p);
      this.loading.set(false);
    });

    this.search$.next('');
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.search$.next(term);
  }

  confirmDelete(id: string): void {
    this.confirmingDeleteId.set(id);
  }

  cancelDelete(): void {
    this.confirmingDeleteId.set(null);
  }

  delete(id: string): void {
    this.productService.delete(id).subscribe(() => {
      this.products.update(list => list.filter(p => p.id !== id));
      this.confirmingDeleteId.set(null);
    });
  }
}
