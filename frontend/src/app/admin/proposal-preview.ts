import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProposalResponse, SolarSizingService } from '../solar-sizing.service';

@Component({
  selector: 'app-proposal-preview',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './proposal-preview.html',
  styleUrl: './proposal-preview.scss',
})
export class ProposalPreview {
  private readonly service = inject(SolarSizingService);
  quoteId = '';
  templateVersion = 'proposal-1.0';
  title = 'Proposta Ecosologic';
  readonly loading = signal(false);
  readonly error = signal('');
  readonly proposal = signal<ProposalResponse | null>(null);

  generate() {
    this.error.set('');
    this.proposal.set(null);
    if (!this.quoteId.trim()) {
      this.error.set('Informe o ID da cotação aprovada.');
      return;
    }
    this.loading.set(true);
    this.service
      .generateProposal({
        quoteId: this.quoteId.trim(),
        templateVersion: this.templateVersion.trim(),
        payloadJson: JSON.stringify({ title: this.title.trim() || 'Proposta Ecosologic' }),
      })
      .subscribe({
        next: (proposal) => {
          this.proposal.set(proposal);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Não foi possível gerar a proposta. Confirme se a cotação está aprovada.');
          this.loading.set(false);
        },
      });
  }
}
