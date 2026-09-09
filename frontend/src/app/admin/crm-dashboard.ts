import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { Lead, LeadService } from '../lead.service';
import { NotificationBell } from './notification-bell';

interface SearchIntent {
  value: string;
  generation: number;
}

@Component({
  selector: 'app-crm-dashboard',
  standalone: true,
  imports: [NotificationBell],
  templateUrl: './crm-dashboard.html',
  styleUrl: './crm-dashboard.scss'
})
export class CrmDashboard implements OnInit, OnDestroy {
  private readonly leadService = inject(LeadService);
  private readonly searchSubject = new Subject<SearchIntent>();
  private readonly searchSubscription: Subscription;

  private loadRequestId = 0;
  private searchGeneration = 0;

  readonly leads = signal<Lead[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly totalPages = signal(0);
  readonly search = signal('');
  readonly stage = signal('');
  readonly stageCounts = signal<Record<string, number>>({});
  readonly stageCountsError = signal('');
  readonly monthlyWon = signal(0);
  readonly monthlyWonError = signal('');

  readonly stageOptions = [
    { value: 'New', label: 'Novo lead' },
    { value: 'Contacted', label: 'Em contato' },
    { value: 'DataReceived', label: 'Dados recebidos' },
    { value: 'Dimensioning', label: 'Dimensionamento' },
    { value: 'ProposalSent', label: 'Proposta enviada' },
    { value: 'Negotiation', label: 'Negociação' },
    { value: 'Won', label: 'Fechado' },
    { value: 'Lost', label: 'Perdido' }
  ];

  readonly stages = [
    { label: 'Novos leads', stage: 'New', tone: 'yellow' },
    { label: 'Em negociação', stage: 'Negotiation', tone: 'blue' },
    { label: 'Propostas enviadas', stage: 'ProposalSent', tone: 'green' },
    { label: 'Fechados no mês', stage: 'Won', tone: 'dark', monthly: true }
  ];

  constructor() {
    this.searchSubscription = this.searchSubject
      .pipe(debounceTime(300), distinctUntilChanged((a, b) => a.value === b.value))
      .subscribe(intent => {
        if (intent.generation !== this.searchGeneration) return;
        this.search.set(intent.value.trim());
        this.page.set(1);
        this.load();
      });
  }

  ngOnInit() {
    this.load();
    this.loadStageCounts();
    this.loadMonthlyWon();
  }

  ngOnDestroy() {
    this.searchSubscription.unsubscribe();
  }

  onSearchInput(event: Event) {
    const value = (event.target as HTMLInputElement).value;
    this.searchSubject.next({ value, generation: ++this.searchGeneration });
  }

  onStageChange(event: Event) {
    this.stage.set((event.target as HTMLSelectElement).value);
    this.page.set(1);
    this.load();
  }

  clearFilters() {
    this.searchGeneration++;
    this.search.set('');
    this.stage.set('');
    this.page.set(1);
    this.load();
  }

  prevPage() {
    if (this.page() > 1) {
      this.page.update(p => p - 1);
      this.load();
    }
  }

  nextPage() {
    if (this.page() < this.totalPages()) {
      this.page.update(p => p + 1);
      this.load();
    }
  }

  private load() {
    const requestId = ++this.loadRequestId;
    this.loading.set(true);
    this.error.set('');
    this.leadService
      .list({ q: this.search() || undefined, stage: this.stage() || undefined, page: this.page(), pageSize: this.pageSize() })
      .subscribe({
        next: response => {
          if (requestId !== this.loadRequestId) return;
          this.leads.set(response.items);
          this.total.set(response.total);
          this.totalPages.set(response.totalPages);
          this.page.set(response.total === 0 ? 1 : response.page);
          this.loading.set(false);
        },
        error: () => {
          if (requestId !== this.loadRequestId) return;
          this.error.set('Não foi possível carregar os leads.');
          this.loading.set(false);
        }
      });
  }

  private loadStageCounts() {
    this.stageCountsError.set('');
    this.leadService.stageCounts().subscribe({
      next: counts => this.stageCounts.set(counts),
      error: () => this.stageCountsError.set('Não foi possível carregar a contagem por etapa.')
    });
  }

  private loadMonthlyWon() {
    this.monthlyWonError.set('');
    this.leadService.monthlyWonCount().subscribe({
      next: count => this.monthlyWon.set(count),
      error: () => this.monthlyWonError.set('Não foi possível carregar os fechados do mês.')
    });
  }

  metricValue(metric: { stage: string; monthly?: boolean }) {
    return metric.monthly ? this.monthlyWon() : this.stageCount(metric.stage);
  }

  stageCount(stage: string) { return this.stageCounts()[stage] ?? 0; }
  stageName(stage: string) { return ({ New: 'Novo lead', Contacted: 'Em contato', DataReceived: 'Dados recebidos', Dimensioning: 'Dimensionamento', ProposalSent: 'Proposta enviada', Negotiation: 'Negociação', Won: 'Fechado', Lost: 'Perdido' } as Record<string, string>)[stage] ?? stage; }
  initials(name: string) { return name.split(' ').map(part => part[0]).join('').slice(0, 2).toUpperCase(); }
}
