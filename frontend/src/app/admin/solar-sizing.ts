import { Component, OnInit, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  SolarMaterial,
  Distributor,
  SolarSizingCalculationRequest,
  SolarSizingResult,
  SolarSizingService,
} from '../solar-sizing.service';
import { saoPauloDateInputValue } from '../date-time';

@Component({
  selector: 'app-solar-sizing',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  templateUrl: './solar-sizing.html',
  styleUrl: './solar-sizing.scss',
})
export class SolarSizing implements OnInit {
  private readonly service = inject(SolarSizingService);
  readonly months = [
    'Jan',
    'Fev',
    'Mar',
    'Abr',
    'Mai',
    'Jun',
    'Jul',
    'Ago',
    'Set',
    'Out',
    'Nov',
    'Dez',
  ];
  readonly consumption = Array.from({ length: 12 }, () => 600);
  readonly bills = Array.from({ length: 12 }, () => 580);
  readonly billsWithSolar = Array.from({ length: 12 }, () => 120);
  readonly hsp = Array.from({ length: 12 }, () => 5.5);
  readonly kFactor = Array.from({ length: 12 }, () => 1);
  readonly loading = signal(false);
  readonly message = signal('');
  readonly error = signal('');
  readonly draftId = signal('');
  readonly result = signal<SolarSizingResult | null>(null);
  readonly materials = signal<SolarMaterial[]>([]);
  readonly distributors = signal<Distributor[]>([]);

  leadId = '';
  distributor = 'Light RJ';
  distributorQuery = '';
  selectedDistributorId = '';
  private distributorSearchTimer?: ReturnType<typeof setTimeout>;
  group = 'B';
  modality = 'Convencional';
  connection = 'Monofasica';
  protocolDate = saoPauloDateInputValue();
  address = '';
  orientation = 'Norte';
  inclination = 10;
  availableArea = 50;
  modulePower = 450;
  moduleVoc = 49;
  moduleVmp = 41;
  moduleIsc = 12;
  moduleImp = 11;
  moduleArea = 2.1;
  moduleMaterialId = '';
  investmentAmount = 75000;

  ngOnInit() {
    this.service
      .listMaterials('Module')
      .subscribe({ next: (materials) => this.materials.set(materials) });
  }

  searchDistributors() {
    clearTimeout(this.distributorSearchTimer);
    if (this.distributorQuery.trim().length < 2) {
      this.distributors.set([]);
      return;
    }
    this.distributorSearchTimer = setTimeout(() => {
      this.service.listDistributors(this.distributorQuery.trim()).subscribe({
        next: (items) => this.distributors.set(items),
        error: () => this.error.set('Não foi possível carregar as concessionárias.'),
      });
    }, 250);
  }

  selectDistributor(item: Distributor) {
    this.distributorQuery = item.officialName;
    this.selectedDistributorId = item.id;
    this.distributor = item.aneelId === 'ENEL_RJ' ? 'EnelRio' : 'Light';
    this.distributors.set([]);
  }

  saveAndCalculate() {
    this.message.set('');
    this.error.set('');
    if (!this.leadId.trim() || !this.address.trim()) {
      this.error.set('Informe o ID do lead e o endereço antes de continuar.');
      return;
    }
    if (!this.selectedDistributorId) {
      this.error.set('Selecione uma concessionária válida nas sugestões.');
      return;
    }
    this.loading.set(true);
    this.service
      .createDraft({
        leadId: this.leadId.trim(),
        distributor: this.distributor,
        distributorId: this.selectedDistributorId,
        group: this.group,
        modality: this.modality,
        connection: this.connection,
        monthlyConsumptionKWh: this.consumption,
        monthlyBillAmount: this.bills,
        moduleMaterialId: this.moduleMaterialId || undefined,
        protocolDate: this.protocolDate,
        address: this.address.trim(),
        assumptionsJson: '{}',
      })
      .subscribe({
        next: (draft) => {
          this.draftId.set(draft.id);
          this.service.calculate(draft.id, this.calculation()).subscribe({
            next: (response) => {
              this.result.set(response.result);
              this.message.set('Dimensionamento calculado pelo motor técnico.');
              this.loading.set(false);
            },
            error: () => {
              this.error.set('O rascunho foi salvo, mas o cálculo não pôde ser concluído.');
              this.loading.set(false);
            },
          });
        },
        error: () => {
          this.error.set('Não foi possível salvar o dimensionamento.');
          this.loading.set(false);
        },
      });
  }

  barHeight(value: number, series: { points: { value: number }[] }) {
    const max = Math.max(...series.points.map((point) => point.value), 1);
    return Math.max(5, (value / max) * 100);
  }

  private calculation(): SolarSizingCalculationRequest {
    return {
      monthlyHsp: this.hsp,
      monthlyKFactor: this.kFactor,
      module: {
        powerWp: this.modulePower,
        voc: this.moduleVoc,
        vmp: this.moduleVmp,
        isc: this.moduleIsc,
        imp: this.moduleImp,
        areaM2: this.moduleArea,
      },
      losses: {
        sombreamento: 0,
        sujeira: 0.02,
        tolerancia: 0,
        mismatch: 0,
        temperatura: 0.112,
        cc: 0.01,
        mppt: 0.02,
        inversor: 0.04,
        ca: 0.01,
      },
      orientation: this.orientation,
      inclinationDegrees: this.inclination,
      availableAreaM2: this.availableArea,
      oversizingFactor: 1,
      monthlyBillWithSolarAmount: this.billsWithSolar,
      investmentAmount: this.investmentAmount,
    };
  }
}
