import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { API_URL } from './api.config';

export interface CreateLead {
  name: string;
  phone: string;
  email?: string;
  message?: string;
}

export interface UpdateLead {
  stage?: string;
  notes?: string;
  reminderAt?: string | null;
}

export interface Lead {
  id: string;
  name: string;
  phone: string;
  email: string;
  message: string;
  stage: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
  reminderAt?: string | null;
}

export interface LeadActivity {
  id: string;
  leadId: string;
  type: string;
  description: string;
  createdAt: string;
}

export interface AddActivity {
  type?: string;
  description: string;
}

export interface LeadTask {
  id: string;
  leadId: string;
  title: string;
  description?: string | null;
  dueAt: string;
  completedAt?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateLeadTask {
  title: string;
  description?: string;
  dueAt: string;
}

export interface LeadListParams {
  q?: string;
  stage?: string;
  page?: number;
  pageSize?: number;
}

export interface LeadListResponse {
  items: Lead[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

@Injectable({ providedIn: 'root' })
export class LeadService {
  private readonly http = inject(HttpClient);
  create(payload: CreateLead) {
    return this.http.post<{ id: string }>(`${API_URL}/leads`, payload);
  }

  list(params: LeadListParams = {}) {
    let query = new HttpParams();
    if (params.q) query = query.set('q', params.q);
    if (params.stage) query = query.set('stage', params.stage);
    if (params.page != null) query = query.set('page', String(params.page));
    if (params.pageSize != null) query = query.set('pageSize', String(params.pageSize));
    return this.http.get<LeadListResponse>(`${API_URL}/leads`, { params: query });
  }

  stageCounts() {
    return this.http.get<Record<string, number>>(`${API_URL}/leads/stage-counts`);
  }

  monthlyWonCount() {
    return this.http.get<number>(`${API_URL}/leads/monthly-won-count`);
  }

  get(id: string) {
    return this.http.get<Lead>(`${API_URL}/leads/${id}`);
  }

  update(id: string, payload: UpdateLead) {
    return this.http.patch<Lead>(`${API_URL}/leads/${id}`, payload);
  }

  activities(id: string) {
    return this.http.get<LeadActivity[]>(`${API_URL}/leads/${id}/activities`);
  }

  addActivity(id: string, payload: AddActivity) {
    return this.http.post<LeadActivity>(`${API_URL}/leads/${id}/activities`, payload);
  }

  tasks(id: string, includeCompleted?: boolean) {
    const query = includeCompleted ? '?includeCompleted=true' : '';
    return this.http.get<LeadTask[]>(`${API_URL}/leads/${id}/tasks${query}`);
  }

  createTask(id: string, payload: CreateLeadTask) {
    return this.http.post<LeadTask>(`${API_URL}/leads/${id}/tasks`, payload);
  }

  updateTask(id: string, taskId: string, completed: boolean) {
    return this.http.patch<LeadTask>(`${API_URL}/leads/${id}/tasks/${taskId}`, { completed });
  }

  deleteTask(id: string, taskId: string) {
    return this.http.delete<void>(`${API_URL}/leads/${id}/tasks/${taskId}`);
  }
}
