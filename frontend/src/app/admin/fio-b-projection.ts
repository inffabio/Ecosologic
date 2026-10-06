import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Distributor, GridRule, SolarSizingService, TariffProfile } from '../solar-sizing.service';

type Mode = 'quick' | 'complete';

@Component({
  selector: 'app-fio-b-projection',
  standalone: true,
  imports: [FormsModule, CurrencyPipe, DecimalPipe],
  templateUrl: './fio-b-projection.html',
  styleUrl: './fio-b-projection.scss',
})
export class FioBProjection {
  private readonly service = inject(SolarSizingService);
  readonly months = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];
  readonly mode = signal<Mode>('quick');
  readonly loading = signal(false);
  readonly error = signal('');
  readonly result = signal<ProjectionResult | null>(null);
  readonly distributors = signal<Distributor[]>([]);
  private selectedProfile?: TariffProfile;
  private selectedRule?: GridRule;

  distributor = 'Light';
  distributorQuery = '';
  selectedDistributorId = '';
  year = new Date().getFullYear();
  consumption = 600;
  generation = 500;
  monthlyConsumption = Array.from({ length: 12 }, () => 600);
  monthlyGeneration = Array.from({ length: 12 }, () => 500);

  private searchTimer?: ReturnType<typeof setTimeout>;

  searchDistributors() {
    clearTimeout(this.searchTimer);
    this.selectedDistributorId = '';
    if (this.distributorQuery.trim().length < 2) {
      this.distributors.set([]);
      return;
    }
    this.searchTimer = setTimeout(() => this.service.listDistributors(this.distributorQuery.trim()).subscribe({
      next: (distributors) => {
        this.distributors.set(distributors);
      },
      error: () => this.error.set('Não foi possível carregar as concessionárias.'),
    }), 250);
  }

  selectDistributor(item: Distributor) {
    this.distributorQuery = item.officialName;
    this.selectedDistributorId = item.id;
    this.distributor = item.aneelId;
    this.distributors.set([]);
  }

  calculate(mode: Mode = this.mode()) {
    this.mode.set(mode);
    this.error.set('');
    this.result.set(null);
    if (!this.selectedDistributorId) {
      this.error.set('Selecione uma concessionária válida nas sugestões.');
      return;
    }
    const consumption = mode === 'quick' ? Array(12).fill(Number(this.consumption)) : this.monthlyConsumption.map(Number);
    const generation = mode === 'quick' ? Array(12).fill(Number(this.generation)) : this.monthlyGeneration.map(Number);
    if (consumption.some((value) => value < 0) || generation.some((value) => value < 0)) {
      this.error.set('Consumo e geração não podem ser negativos.');
      return;
    }
    this.loading.set(true);
    const params = { distributorId: this.selectedDistributorId, group: 'B', subgroup: 'B1', modality: 'Conventional', post: 'Single', date: `${this.year}-01-01`, referenceYear: String(this.year), isComplete: 'true' };
    this.service.listTariffProfiles(params).subscribe({
      next: (profiles) => this.service.listGridRules(params).subscribe({
        next: (rules) => this.finish(profiles[0], rules[0], consumption, generation),
        error: () => this.fail('Não foi possível consultar a regra de Fio B vigente.'),
      }),
      error: () => this.fail('Não foi possível consultar o perfil tarifário vigente.'),
    });
  }

  recalculate() {
    if (this.mode() === 'quick' && (!this.selectedProfile || !this.selectedRule)) return;
    const consumption = this.mode() === 'quick' ? Array(12).fill(Number(this.consumption)) : this.monthlyConsumption.map(Number);
    const generation = this.mode() === 'quick' ? Array(12).fill(Number(this.generation)) : this.monthlyGeneration.map(Number);
    this.finish(this.selectedProfile, this.selectedRule, consumption, generation);
  }

  barHeight(value: number, rows: ProjectionResult['rows'], key: 'compensated' | 'cost') {
    const max = Math.max(...rows.map((row) => row[key]), 1);
    return Math.max(6, (value / max) * 100);
  }

  private finish(profile: TariffProfile | undefined, rule: GridRule | undefined, consumption: number[], generation: number[]) {
    const base = profile?.components.find((component) => component.kind === rule?.baseComponent && component.post === 'Single');
    if (!profile || !rule || !base || !profile.isComplete || !rule.isComplete) {
      this.fail('Não existe uma tarifa completa para essa combinação e data.');
      return;
    }
    this.selectedProfile = profile;
    this.selectedRule = rule;
    const rows = consumption.map((value, index) => {
      const injected = Math.max(generation[index] - Math.min(generation[index], value), 0);
      const compensated = Math.min(value, generation[index]);
      return { label: this.months[index], consumption: value, generation: generation[index], injected, compensated, cost: rule.progressivePercent / 100 * base.value * compensated };
    });
    this.result.set({ rows, percent: rule.progressivePercent, base: base.value, resolution: rule.resolutionCode, source: rule.sourceUrl, annualCost: rows.reduce((sum, row) => sum + row.cost, 0), annualCompensated: rows.reduce((sum, row) => sum + row.compensated, 0) });
    this.loading.set(false);
  }

  private fail(message: string) {
    this.error.set(message);
    this.loading.set(false);
  }
}

interface ProjectionResult {
  rows: { label: string; consumption: number; generation: number; injected: number; compensated: number; cost: number }[];
  percent: number;
  base: number;
  resolution: string;
  source: string;
  annualCost: number;
  annualCompensated: number;
}
