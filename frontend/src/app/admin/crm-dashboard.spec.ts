import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CrmDashboard } from './crm-dashboard';

const LEAD = { id: '1', name: 'Fabio', phone: '999', email: '', message: 'Orçamento', stage: 'New', createdAt: '2026-09-01' };
const LIST_URL = 'http://localhost:5157/api/leads?page=1&pageSize=20';
const COUNTS_URL = 'http://localhost:5157/api/leads/stage-counts';
const MONTHLY_WON_URL = 'http://localhost:5157/api/leads/monthly-won-count';
const UNREAD_URL = 'http://localhost:5157/api/notifications/unread-count';

function response(items = [LEAD], total = items.length, page = 1, pageSize = 20, totalPages = 1) {
  return { items, total, page, pageSize, totalPages };
}

function flushInitial(
  http: HttpTestingController,
  list = response(),
  counts: Record<string, number> = { New: 1 },
  monthlyWon = 0
) {
  http.expectOne(LIST_URL).flush(list);
  http.expectOne(COUNTS_URL).flush(counts);
  http.expectOne(MONTHLY_WON_URL).flush(monthlyWon);
  http.expectOne(UNREAD_URL).flush({ count: 0 });
}

describe('CrmDashboard', () => {
  function setup() {
    TestBed.configureTestingModule({ imports: [CrmDashboard], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CrmDashboard);
    const http = TestBed.inject(HttpTestingController);
    return { fixture, http };
  }

  it('shows the CRM overview heading', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('h1')?.textContent).toContain('Visão geral');
    http.verify();
  });

  it('renders leads and total from the list response', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http, response([LEAD], 1));
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Fabio');
    expect(text).toContain('Mostrando 1 de 1 leads');
    http.verify();
  });

  it('shows empty state when there are no leads', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http, response([], 0, 1, 20, 0), {});
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ainda não há leads');
    http.verify();
  });

  it('normalizes page to 1 when the server reports zero total results', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http, response([], 0, 3, 20, 0), {});
    fixture.detectChanges();
    expect(fixture.componentInstance.page()).toBe(1);
    expect(fixture.componentInstance.totalPages()).toBe(0);
    http.verify();
  });

  it('shows an error state when loading fails', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    http.expectOne(LIST_URL).flush(null, { status: 500, statusText: 'Server Error' });
    http.expectOne(COUNTS_URL).flush({});
    http.expectOne(MONTHLY_WON_URL).flush(0);
    http.expectOne(UNREAD_URL).flush({ count: 0 });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Não foi possível carregar os leads');
    http.verify();
  });

  it('debounces the search input and resets to the first page', fakeAsync(() => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http);
    fixture.detectChanges();

    const input = (fixture.nativeElement as HTMLElement).querySelector('.lead-search') as HTMLInputElement;
    input.value = 'ana';
    input.dispatchEvent(new Event('input'));
    tick(100);
    input.value = 'fabio';
    input.dispatchEvent(new Event('input'));
    tick(350);

    const request = http.expectOne('http://localhost:5157/api/leads?q=fabio&page=1&pageSize=20');
    request.flush(response([LEAD], 1));
    fixture.detectChanges();
    http.verify();
  }));

  it('filters by stage', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http);
    fixture.detectChanges();

    const select = (fixture.nativeElement as HTMLElement).querySelector('.lead-stage-select') as HTMLSelectElement;
    select.value = 'New';
    select.dispatchEvent(new Event('change'));

    const request = http.expectOne('http://localhost:5157/api/leads?stage=New&page=1&pageSize=20');
    expect(request.request.method).toBe('GET');
    request.flush(response([LEAD], 1));
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Fabio');
    http.verify();
  });

  it('moves to the next page', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http, response([LEAD], 25, 1, 20, 2), { New: 25 });
    fixture.detectChanges();

    const next = (fixture.nativeElement as HTMLElement).querySelector('button[aria-label="Próxima página"]') as HTMLButtonElement;
    next.click();

    const request = http.expectOne('http://localhost:5157/api/leads?page=2&pageSize=20');
    expect(request.request.method).toBe('GET');
    request.flush(response([{ ...LEAD, name: 'Segunda página' }], 25, 2, 20, 2));
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Página 2 de 2');
    http.verify();
  });

  it('clears filters and reloads without query params', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http);
    fixture.detectChanges();

    const select = (fixture.nativeElement as HTMLElement).querySelector('.lead-stage-select') as HTMLSelectElement;
    select.value = 'New';
    select.dispatchEvent(new Event('change'));
    http.expectOne('http://localhost:5157/api/leads?stage=New&page=1&pageSize=20').flush(response());
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('.clear-filters')!.dispatchEvent(new Event('click'));

    const request = http.expectOne(LIST_URL);
    request.flush(response());
    fixture.detectChanges();
    expect((select as HTMLSelectElement).value).toBe('');
    http.verify();
  });

  it('shows the monthly won count from its own endpoint', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http, response(), { New: 3, Won: 9 }, 2);
    fixture.detectChanges();

    const metrics = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.metric')
    );
    const wonMetric = metrics.find(metric => metric.textContent!.includes('Fechados no mês'))!;
    expect(wonMetric.querySelector('strong')!.textContent).toBe('2');
    http.verify();
  });

  it('shows an error state when stage counts fail', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    http.expectOne(LIST_URL).flush(response());
    http.expectOne(COUNTS_URL).flush(null, { status: 500, statusText: 'Server Error' });
    http.expectOne(MONTHLY_WON_URL).flush(0);
    http.expectOne(UNREAD_URL).flush({ count: 0 });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Não foi possível carregar a contagem por etapa'
    );
    http.verify();
  });

  it('ignores stale list responses that arrive after a newer request', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http);
    fixture.detectChanges();

    const select = (fixture.nativeElement as HTMLElement).querySelector(
      '.lead-stage-select'
    ) as HTMLSelectElement;
    select.value = 'New';
    select.dispatchEvent(new Event('change'));
    const stale = http.expectOne('http://localhost:5157/api/leads?stage=New&page=1&pageSize=20');

    select.value = 'Won';
    select.dispatchEvent(new Event('change'));
    const latest = http.expectOne('http://localhost:5157/api/leads?stage=Won&page=1&pageSize=20');

    latest.flush(response([{ ...LEAD, name: 'Ganhador', stage: 'Won' }], 1));
    fixture.detectChanges();

    stale.flush(response([{ ...LEAD, name: 'Stale', stage: 'New' }], 1));
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Ganhador');
    expect(text).not.toContain('Stale');
    http.verify();
  });

  it('clearFilters cancels a pending debounced search', fakeAsync(() => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http);
    fixture.detectChanges();

    const input = (fixture.nativeElement as HTMLElement).querySelector(
      '.lead-search'
    ) as HTMLInputElement;
    input.value = 'ana';
    input.dispatchEvent(new Event('input'));
    tick(100);

    (fixture.nativeElement as HTMLElement)
      .querySelector('.clear-filters')!
      .dispatchEvent(new Event('click'));
    const clearRequest = http.expectOne(LIST_URL);
    clearRequest.flush(response());
    fixture.detectChanges();

    tick(350);

    http.verify();
    expect(fixture.componentInstance.search()).toBe('');
  }));

  it('syncs the displayed page to the value returned by the server', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    flushInitial(http, response([LEAD], 25, 1, 20, 2), { New: 25 });
    fixture.detectChanges();

    const next = (fixture.nativeElement as HTMLElement).querySelector(
      'button[aria-label="Próxima página"]'
    ) as HTMLButtonElement;
    next.click();

    const request = http.expectOne('http://localhost:5157/api/leads?page=2&pageSize=20');
    request.flush(response([LEAD], 1, 1, 20, 1));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Página 1 de 1');
    http.verify();
  });

  it('shows an error state when the monthly won count fails', () => {
    const { fixture, http } = setup();
    fixture.detectChanges();
    http.expectOne(LIST_URL).flush(response());
    http.expectOne(COUNTS_URL).flush({ New: 1 });
    http.expectOne(MONTHLY_WON_URL).flush(null, { status: 500, statusText: 'Server Error' });
    http.expectOne(UNREAD_URL).flush({ count: 0 });
    fixture.detectChanges();

    const metrics = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.metric')
    );
    const wonMetric = metrics.find(metric => metric.textContent!.includes('Fechados no mês'))!;
    expect(wonMetric.querySelector('strong')!.textContent).toContain(
      'Não foi possível carregar os fechados do mês'
    );
    http.verify();
  });
});
