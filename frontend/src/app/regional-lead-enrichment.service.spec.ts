import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { RegionalLeadEnrichmentService } from './regional-lead-enrichment.service';

describe('RegionalLeadEnrichmentService', () => {
  let service: RegionalLeadEnrichmentService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [RegionalLeadEnrichmentService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(RegionalLeadEnrichmentService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the selected cities to the authenticated backend proxy', () => {
    service.run({ cities: ['Maricá', 'Macaé'], maxLeadsPerCity: 10 }).subscribe();

    const request = http.expectOne('http://localhost:5157/api/lead-enrichment/regional');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ cities: ['Maricá', 'Macaé'], maxLeadsPerCity: 10 });
    request.flush({ status: 'completed', enrichedLeads: 2, message: 'ok' });
  });
});
