import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { Product } from '../../products/product.model';

@Component({
  selector: 'app-catalog-detail',
  imports: [RouterLink],
  templateUrl: './catalog-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogDetailComponent implements OnInit {

  private readonly titleService = inject(Title);
  private readonly route = inject(ActivatedRoute);

  readonly product = signal<Product | null>(null);

  ngOnInit(): void {
    const product = this.route.snapshot.data['product'] as Product;
    this.titleService.setTitle(`Experience | ${product.name ?? 'Product'}`);
    this.product.set(product);
  }
}
