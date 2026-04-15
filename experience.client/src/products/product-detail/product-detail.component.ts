import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { catchError, EMPTY } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { ProductService } from '../product.service';
import { Product } from '../product.model';

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink],
  templateUrl: './product-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductDetailComponent implements OnInit {

  private readonly titleService = inject(Title);
  private readonly productService = inject(ProductService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly product = signal<Product | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/products/not-found']);
      return;
    }

    this.productService.getById(id).pipe(
      catchError((err: HttpErrorResponse) => {
        if (err.status === 404) {
          this.router.navigate(['/products/not-found']);
        } else {
          this.router.navigate(['/products']);
        }

        return EMPTY;
      })
    ).subscribe(p => {
      this.titleService.setTitle(`Experience | ${p.name ?? 'Product'}`);
      this.product.set(p);
    });
  }

  findManualQuery(p: Product): string {
    return ['Help me find the manual for', p.name, p.brand, p.modelNumber]
      .filter(Boolean)
      .join(' ');
  }
}
