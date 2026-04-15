import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Title } from '@angular/platform-browser';
import { catchError, EMPTY } from 'rxjs';
import { ProductService } from '../product.service';

@Component({
  selector: 'app-product-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './product-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductFormComponent implements OnInit {

  private readonly titleService = inject(Title);
  private readonly productService = inject(ProductService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  readonly editId = signal<string | null>(null);
  readonly isEdit = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    name: [null as string | null, Validators.required],
    price: [null as number | null],
    brand: [null as string | null],
    modelNumber: [null as string | null],
    serialNumber: [null as string | null],
    purchaseDate: [null as string | null],
    category: [null as string | null],
    description: [null as string | null],
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.editId.set(id);
      this.isEdit.set(true);
      this.titleService.setTitle('Experience | Edit Product');
      this.productService.getById(id).pipe(
        catchError((err: HttpErrorResponse) => {
          if (err.status === 404) {
            this.router.navigate(['/products/not-found']);
          } else {
            this.router.navigate(['/products']);
          }

          return EMPTY;
        })
      ).subscribe(product => this.form.patchValue(product));
    } else {
      this.titleService.setTitle('Experience | New Product');
    }
  }

  submit(): void {
    if (this.form.invalid) return;

    const value = this.form.getRawValue();
    const id = this.editId();

    const onError = (err: HttpErrorResponse) => {
      this.error.set(`Save failed (${err.status}). Please try again.`);
      return EMPTY;
    };

    if (id) {
      this.productService.put(id, value).pipe(catchError(onError)).subscribe(() => {
        this.router.navigate(['/products', id]);
      });
    } else {
      this.productService.create(value).pipe(catchError(onError)).subscribe(created => {
        this.router.navigate(['/products', created.id]);
      });
    }
  }
}
