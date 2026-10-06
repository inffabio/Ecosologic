import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { API_URL } from './api.config';

export interface SolarSizingDraftRequest {
  leadId: string;
  distributor: string;
  distributorId?: string;
  group: string;
  modality: string;
  connection: string;
  monthlyConsumptionKWh: number[];
  monthlyBillAmount: number[];
  moduleMaterialId?: string;
  inverterMaterialId?: string;
  protocolDate: string;
  address: string;
  assumptionsJson: string;
}

export interface Distributor {
  id: string;
  aneelId: string;
  officialName: string;
  cnpj?: string;
  lastSyncedAt: string;
}

export interface SolarSizingCalculationRequest {
  monthlyHsp: number[];
  monthlyKFactor: number[];
  module: { powerWp: number; voc: number; vmp: number; isc: number; imp: number; areaM2: number };
  losses: Record<string, number>;
  orientation: string;
  inclinationDegrees: number;
  availableAreaM2: number;
  oversizingFactor: number;
  monthlyBillWithSolarAmount?: number[];
  investmentAmount?: number;
  deratingFactor?: number;
  inverter?: {
    nominalPowerW: number;
    mpptVoltageMin: number;
    mpptVoltageMax: number;
    maxInputVoltage: number;
    maxInputCurrent: number;
    mpptCount: number;
    maxStringsPerMppt: number;
  };
}

export interface SolarChartSeries {
  key: string;
  unit: string;
  points: { label: string; value: number; unit: string }[];
}

export interface SolarSizingResult {
  annualGenerationKWh: number;
  annualConsumptionKWh: number;
  installedKwp: number;
  moduleCount: number;
  coverage: number;
  worstMonthIndex: number;
  alerts: { code: string; severity: string; message: string }[];
  charts: {
    generationVsConsumption: SolarChartSeries[];
    financialCashFlow: SolarChartSeries[];
    annualBillComparison: SolarChartSeries[];
  };
}

export interface SolarSizingCalculationResponse {
  id: string;
  status: string;
  result: SolarSizingResult;
}

export interface SolarMaterial {
  id: string;
  type: 'Module' | 'Inverter';
  brand: string;
  model: string;
  powerW: number;
  technicalDataJson: string;
  sourceUrl: string;
  currentPrice?: number;
}

export interface ProposalResponse {
  id: string;
  quoteId: string;
  status: string;
  templateVersion: string;
  fileUrl: string;
  fileHash: string;
}

export interface SolarQuoteResponse {
  id: string;
  status: string;
  supplierName: string;
  totalCost: number;
  totalPrice: number;
  itemsJson: string;
}

export interface SolarSupplier {
  id: string;
  name: string;
  website?: string;
  contact?: string;
  contactName?: string;
  phone?: string;
  whatsapp?: string;
  source: string;
  status: string;
}

export interface TariffProfile {
  distributor: string;
  group: string;
  subgroup: string;
  modality: string;
  validityStart: string;
  validityEnd?: string;
  resolutionCode: string;
  sourceUrl: string;
  isComplete: boolean;
  components: { kind: string; unit: string; post: string; value: number; taxIncluded: boolean }[];
}

export interface GridRule {
  distributor: string;
  referenceYear: number;
  validityStart: string;
  validityEnd?: string;
  progressivePercent: number;
  baseComponent: string;
  resolutionCode: string;
  sourceUrl: string;
  isComplete: boolean;
}

@Injectable({ providedIn: 'root' })
export class SolarSizingService {
  private readonly http = inject(HttpClient);

  createDraft(payload: SolarSizingDraftRequest) {
    return this.http.post<{ id: string; status: string }>(
      `${API_URL}/solar-sizing/drafts`,
      payload,
    );
  }

  calculate(id: string, payload: SolarSizingCalculationRequest) {
    return this.http.post<SolarSizingCalculationResponse>(
      `${API_URL}/solar-sizing/${id}/calculate`,
      payload,
    );
  }

  listMaterials(type?: 'Module' | 'Inverter', query?: string) {
    const params: Record<string, string> = {};
    if (type) params['type'] = type;
    if (query) params['query'] = query;
    return this.http.get<SolarMaterial[]>(`${API_URL}/solar-materials`, { params });
  }

  listTariffProfiles(params: Record<string, string>) {
    return this.http.get<TariffProfile[]>(`${API_URL}/tariffs/profiles`, { params });
  }

  listDistributors(query = '') {
    const params: Record<string, string> = {};
    if (query) params['q'] = query;
    return this.http.get<Distributor[]>(`${API_URL}/tariffs/distributors`, { params });
  }

  listGridRules(params: Record<string, string>) {
    return this.http.get<GridRule[]>(`${API_URL}/tariffs/grid-rules`, { params });
  }

  listSuppliers() {
    return this.http.get<SolarSupplier[]>(`${API_URL}/solar-suppliers`);
  }

  listSupplierReview() {
    return this.http.get<SolarSupplier[]>(`${API_URL}/solar-suppliers/review`);
  }
  discoverSuppliers(query: string) {
    return this.http.post<SolarSupplier[]>(`${API_URL}/solar-suppliers/discover`, { query });
  }
  approveSupplier(id: string, payload: { contactName: string; phone: string; whatsapp: string }) {
    return this.http.post<SolarSupplier>(`${API_URL}/solar-suppliers/${id}/approve`, payload);
  }

  generateProposal(payload: { quoteId: string; templateVersion: string; payloadJson: string }) {
    return this.http.post<ProposalResponse>(`${API_URL}/proposals`, payload);
  }

  getProposal(id: string) {
    return this.http.get<ProposalResponse>(`${API_URL}/proposals/${id}`);
  }

  createQuote(payload: {
    sizingId: string;
    supplierName: string;
    items: { materialId: string; quantity: number }[];
    marginPercent: number;
    taxPercent: number;
    conditionsJson: string;
    validUntil: string;
  }) {
    return this.http.post<SolarQuoteResponse>(`${API_URL}/solar-quotes`, payload);
  }

  refreshQuotePrices(id: string) {
    return this.http.post<SolarQuoteResponse>(`${API_URL}/solar-quotes/${id}/refresh-prices`, {});
  }
}
