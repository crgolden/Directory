import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { ProductService } from '../product.service';

@Component({
  selector: 'app-product-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.css',
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

  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    brand: [''],
    modelNumber: [''],
    serialNumber: [''],
    purchaseDate: [''],
    category: [''],
    description: [''],
    manualUrl: [''],
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.editId.set(id);
      this.isEdit.set(true);
      this.titleService.setTitle('Experience | Edit Product');
      this.productService.getById(id).subscribe(product => {
        if (product) {
          this.form.patchValue(product);
        }
      });
    } else {
      this.titleService.setTitle('Experience | New Product');
    }
  }

  submit(): void {
    if (this.form.invalid) return;

    const value = this.form.getRawValue();
    const id = this.editId();

    if (id) {
      this.productService.update(id, value).subscribe(() => {
        this.router.navigate(['/products']);
      });
    } else {
      this.productService.create(value).subscribe(() => {
        this.router.navigate(['/products']);
      });
    }
  }
}
