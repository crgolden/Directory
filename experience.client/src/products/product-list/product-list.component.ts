import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { ProductService } from '../product.service';
import { Product } from '../product.model';

@Component({
  selector: 'app-product-list',
  imports: [RouterLink],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductListComponent implements OnInit {

  private readonly titleService = inject(Title);
  private readonly productService = inject(ProductService);

  readonly products = signal<Product[]>([]);
  readonly confirmingDeleteId = signal<string | null>(null);

  ngOnInit(): void {
    this.titleService.setTitle('Experience | My Products');
    this.productService.getAll().subscribe(p => this.products.set(p));
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
