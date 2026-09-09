import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { API_URL } from './api.config';

export type NotificationKind = 'TaskOverdue' | 'TaskDueToday' | 'LeadReminder';

export interface Notification {
  id: string;
  kind: NotificationKind;
  leadId: string;
  taskId?: string | null;
  title: string;
  dueAt: string;
  readAt?: string | null;
  createdAt: string;
}

export interface UnreadCount {
  count: number;
}

export interface NotificationPage {
  items: Notification[];
  nextCursor: string | null;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);

  list(cursor: string | null = null, limit = 100): Observable<NotificationPage> {
    let params = new HttpParams().set('limit', String(limit));
    if (cursor) {
      params = params.set('cursor', cursor);
    }
    return this.http.get<NotificationPage>(`${API_URL}/notifications`, { params });
  }

  unreadCount(): Observable<number> {
    return this.http.get<UnreadCount>(`${API_URL}/notifications/unread-count`)
      .pipe(map(response => response.count));
  }

  markRead(id: string): Observable<void> {
    return this.http.patch<void>(`${API_URL}/notifications/${id}/read`, {});
  }

  markAllRead(): Observable<void> {
    return this.http.patch<void>(`${API_URL}/notifications/read-all`, {});
  }
}
