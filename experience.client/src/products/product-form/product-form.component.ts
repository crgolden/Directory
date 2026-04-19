import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Title } from '@angular/platform-browser';
import { catchError, EMPTY } from 'rxjs';
import { ProductService } from '../product.service';
import { ManualChatPanelComponent } from '../manual-chat/manual-chat-panel.component';
import { ProductContext } from '../manual-chat/chat.model';

@Component({
  selector: 'app-product-form',
  imports: [ReactiveFormsModule, RouterLink, ManualChatPanelComponent],
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
    manualUrl: [null as string | null],
  });

  /**
   * Snapshot of the form fields the manual-chat panel cares about. Updated
   * whenever the form value changes so the chat always has fresh context
   * (e.g. if the user fills in the brand first, then opens the panel).
   */
  private readonly formSignal = signal(this.form.getRawValue());

  readonly productContext = computed<ProductContext>(() => {
    const v = this.formSignal();
    return {
      id: this.editId(),
      name: v.name,
      brand: v.brand,
      modelNumber: v.modelNumber,
    };
  });

  constructor() {
    this.form.valueChanges.subscribe(() => this.formSignal.set(this.form.getRawValue()));
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.editId.set(id);
      this.isEdit.set(true);
      this.titleService.setTitle('Experience | Edit Product');
      const product = this.route.snapshot.data['product'];
      if (product) {
        this.form.patchValue(product);
      }
    } else {
      this.titleService.setTitle('Experience | New Product');
    }
  }

  /**
   * Called when the user clicks a "Use this URL" chip inside the manual-chat
   * panel. Patches the form control and marks it dirty so save works as usual.
   */
  onManualUrlSelected(url: string): void {
    this.form.controls.manualUrl.setValue(url);
    this.form.controls.manualUrl.markAsDirty();
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
      this.productService.patch(id, value).pipe(catchError(onError)).subscribe(() => {
        this.router.navigate(['/products', id]);
      });
    } else {
      this.productService.create(value).pipe(catchError(onError)).subscribe(id => {
        this.router.navigate(['/products', id]);
      });
    }
  }
}
