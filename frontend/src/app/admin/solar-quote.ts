import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import {
  SolarMaterial,
  SolarQuoteResponse,
  SolarSizingService,
  SolarSupplier,
} from '../solar-sizing.service';
import { saoPauloDateInputValue } from '../date-time';

@Component({
  selector: 'app-solar-quote',
  standalone: true,
  imports: [FormsModule, CurrencyPipe],
  templateUrl: './solar-quote.html',
  styleUrl: './solar-quote.scss',
})
export class SolarQuote {
  private readonly service = inject(SolarSizingService);
  private readonly route = inject(ActivatedRoute);
  readonly materials = signal<SolarMaterial[]>([]);
  readonly suppliers = signal<SolarSupplier[]>([]);
  readonly quote = signal<SolarQuoteResponse | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  sizingId = '';
  supplierName = '';
  moduleId = '';
  quantity = 1;
  marginPercent = 20;
  taxPercent = 0;
  validUntil = saoPauloDateInputValue(new Date(Date.now() + 30 * 86400000));

  ngOnInit() {
    this.sizingId = this.route.snapshot.queryParamMap.get('sizingId') ?? '';
    this.service.listMaterials('Module').subscribe({ next: (items) => this.materials.set(items) });
    this.service.listSuppliers().subscribe({ next: (items) => this.suppliers.set(items) });
  }

  create() {
    this.error.set('');
    if (
      !this.sizingId.trim() ||
      !this.supplierName.trim() ||
      !this.moduleId ||
      this.quantity <= 0
    ) {
      this.error.set('Informe fornecedor, ID do dimensionamento, material e quantidade.');
      return;
    }
    this.loading.set(true);
    this.service
      .createQuote({
        sizingId: this.sizingId.trim(),
        supplierName: this.supplierName,
        items: [{ materialId: this.moduleId, quantity: this.quantity }],
        marginPercent: this.marginPercent,
        taxPercent: this.taxPercent,
        conditionsJson: '{}',
        validUntil: `${this.validUntil}T23:59:59-03:00`,
      })
      .subscribe({
        next: (quote) => {
          this.quote.set(quote);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(
            'Não foi possível criar a cotação. O dimensionamento precisa estar calculado.',
          );
          this.loading.set(false);
        },
      });
  }

  refreshPrices() {
    const quote = this.quote();
    if (!quote) return;
    this.loading.set(true);
    this.service.refreshQuotePrices(quote.id).subscribe({
      next: (updated) => {
        this.quote.set(updated);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível atualizar os preços.');
        this.loading.set(false);
      },
    });
  }
}
