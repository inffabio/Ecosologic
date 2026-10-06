import { Routes } from '@angular/router';
import { CrmDashboard } from './admin/crm-dashboard';
import { Home } from './home';
import { Login } from './login';
import { authGuard } from './auth.guard';
import { LeadDetail } from './admin/lead-detail';
import { ContentEditor } from './admin/content-editor';
import { SolarSizing } from './admin/solar-sizing';
import { ProposalPreview } from './admin/proposal-preview';
import { SolarQuote } from './admin/solar-quote';
import { SolarSuppliers } from './admin/solar-suppliers';
import { RegionalLeadEnrichment } from './admin/regional-lead-enrichment';
import { FioBProjection } from './admin/fio-b-projection';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'login', component: Login },
  { path: 'admin/crm', component: CrmDashboard, canActivate: [authGuard] },
  { path: 'admin/leads/:id', component: LeadDetail, canActivate: [authGuard] },
  { path: 'admin/conteudo', component: ContentEditor, canActivate: [authGuard] },
  { path: 'admin/dimensionamento', component: SolarSizing, canActivate: [authGuard] },
  { path: 'admin/propostas', component: ProposalPreview, canActivate: [authGuard] },
  { path: 'admin/cotacao', component: SolarQuote, canActivate: [authGuard] },
  { path: 'admin/fornecedores', component: SolarSuppliers, canActivate: [authGuard] },
  { path: 'admin/prospeccao-regional', component: RegionalLeadEnrichment, canActivate: [authGuard] },
  { path: 'admin/fio-b', component: FioBProjection, canActivate: [authGuard] },
  { path: '**', redirectTo: '' },
];
