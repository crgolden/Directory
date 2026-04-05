import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { ProductService } from '../product.service';
import { Product } from '../product.model';

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductDetailComponent implements OnInit {

  private readonly titleService = inject(Title);
  private readonly productService = inject(ProductService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly product = signal<Product | undefined>(undefined);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/products']);
      return;
    }
    this.productService.getById(id).subscribe(p => {
      if (!p) {
        this.router.navigate(['/products']);
        return;
      }
      this.titleService.setTitle(`Experience | ${p.name}`);
      this.product.set(p);
    });
  }

  findManualQuery(p: Product): string {
    const parts = ['Help me find the manual for', p.name, p.brand, p.modelNumber]
      .filter(Boolean)
      .join(' ');
    return parts;
  }
}
