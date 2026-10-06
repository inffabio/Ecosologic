import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RegionalLeadEnrichmentService } from '../regional-lead-enrichment.service';

@Component({
  selector: 'app-regional-lead-enrichment',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './regional-lead-enrichment.html',
  styleUrl: './regional-lead-enrichment.scss',
})
export class RegionalLeadEnrichment {
  private readonly service = inject(RegionalLeadEnrichmentService);
  readonly cities = signal('Maricá\nSaquarema\nRio das Ostras\nMacaé');
  readonly maxLeadsPerCity = signal(10);
  readonly running = signal(false);
  readonly result = signal<{ enrichedLeads: number; message: string } | null>(null);
  readonly error = signal('');

  run() {
    const cities = [...new Set(this.cities().split(/[\n,;]+/).map(city => city.trim()).filter(Boolean))];
    if (!cities.length) {
      this.error.set('Informe ao menos uma cidade.');
      return;
    }

    this.running.set(true);
    this.error.set('');
    this.result.set(null);
    this.service.run({ cities, maxLeadsPerCity: Math.max(1, Math.min(10, this.maxLeadsPerCity())) }).subscribe({
      next: response => {
        this.running.set(false);
        this.result.set(response);
      },
      error: response => {
        this.running.set(false);
        this.error.set(response?.error?.detail ?? 'Não foi possível executar a prospecção regional.');
      },
    });
  }
}
