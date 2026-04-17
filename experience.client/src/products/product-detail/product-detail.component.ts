import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { Product } from '../product.model';

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink],
  templateUrl: './product-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductDetailComponent implements OnInit {

  private readonly titleService = inject(Title);
  private readonly route = inject(ActivatedRoute);

  readonly product = signal<Product | null>(null);

  ngOnInit(): void {
    const product = this.route.snapshot.data['product'] as Product;
    this.titleService.setTitle(`Experience | ${product.name ?? 'Product'}`);
    this.product.set(product);
  }

  findManualQuery(p: Product): string {
    return ['Help me find the manual for', p.name, p.brand, p.modelNumber]
      .filter(Boolean)
      .join(' ');
  }
}
