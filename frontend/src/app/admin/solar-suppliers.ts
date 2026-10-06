import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SolarSizingService, SolarSupplier } from '../solar-sizing.service';

@Component({
  selector: 'app-solar-suppliers',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './solar-suppliers.html',
  styleUrl: './solar-suppliers.scss',
})
export class SolarSuppliers {
  private readonly service = inject(SolarSizingService);
  readonly suppliers = signal<SolarSupplier[]>([]);
  readonly error = signal('');
  query = 'fornecedores de kits solares no Brasil';
  selected: SolarSupplier | null = null;
  contactName = '';
  phone = '';
  whatsapp = '';
  ngOnInit() {
    this.load();
  }
  load() {
    this.service.listSupplierReview().subscribe({
      next: (items) => this.suppliers.set(items),
      error: () => this.error.set('Não foi possível carregar fornecedores.'),
    });
  }
  discover() {
    this.service.discoverSuppliers(this.query).subscribe({
      next: (items) => {
        this.suppliers.set(items);
        this.error.set('Candidatos salvos como pendentes de aprovação.');
      },
      error: () => this.error.set('Configure o serviço de descoberta antes de pesquisar.'),
    });
  }
  approve() {
    if (!this.selected) return;
    this.service
      .approveSupplier(this.selected.id, {
        contactName: this.contactName,
        phone: this.phone,
        whatsapp: this.whatsapp,
      })
      .subscribe({
        next: () => {
          this.selected = null;
          this.load();
        },
        error: () => this.error.set('Preencha nome, telefone e WhatsApp.'),
      });
  }
}
