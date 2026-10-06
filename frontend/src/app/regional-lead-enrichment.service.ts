import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { API_URL } from './api.config';

export interface RegionalLeadEnrichmentRequest {
  cities: string[];
  maxLeadsPerCity: number;
}

export interface RegionalLeadEnrichmentResponse {
  status: string;
  enrichedLeads: number;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class RegionalLeadEnrichmentService {
  private readonly http = inject(HttpClient);

  run(request: RegionalLeadEnrichmentRequest) {
    return this.http.post<RegionalLeadEnrichmentResponse>(
      `${API_URL}/lead-enrichment/regional`,
      request,
    );
  }
}
