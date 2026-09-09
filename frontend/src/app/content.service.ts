import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { API_URL } from './api.config';

export interface Solution {
  title: string;
  text: string;
}

export interface ProcessStep {
  title: string;
  text: string;
}

export interface Project {
  title: string;
  category: string;
  power: string;
  imageUrl: string;
  alt: string;
}

export interface HomeContent {
  heroTitle: string;
  heroText: string;
  heroImageUrl: string;
  contactEmail: string;
  contactPhone: string;
  solutions: Solution[];
  processSteps: ProcessStep[];
  projects: Project[];
  updatedAt?: string;
}

@Injectable({ providedIn: 'root' })
export class ContentService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${API_URL}/content`;

  getPublicHome() { return this.http.get<HomeContent>(`${this.apiUrl}/home`); }
  updateHome(payload: HomeContent) { return this.http.put<HomeContent>(`${this.apiUrl}/home`, payload); }

  uploadMedia(file: File) {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<{ url: string }>(`${this.apiUrl}/media`, formData);
  }
}
