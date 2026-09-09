import { Routes } from '@angular/router';
import { CrmDashboard } from './admin/crm-dashboard';
import { Home } from './home';
import { Login } from './login';
import { authGuard } from './auth.guard';
import { LeadDetail } from './admin/lead-detail';
import { ContentEditor } from './admin/content-editor';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'login', component: Login },
  { path: 'admin/crm', component: CrmDashboard, canActivate: [authGuard] },
  { path: 'admin/leads/:id', component: LeadDetail, canActivate: [authGuard] },
  { path: 'admin/conteudo', component: ContentEditor, canActivate: [authGuard] },
  { path: '**', redirectTo: '' }
];
