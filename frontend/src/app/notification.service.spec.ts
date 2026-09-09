import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { NotificationService } from './notification.service';

describe('NotificationService', () => {
  const base = 'http://localhost:5157/api/notifications';

  it('loads the first page without a cursor', () => {
    TestBed.configureTestingModule({ providers: [NotificationService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(NotificationService);
    const http = TestBed.inject(HttpTestingController);
    service.list().subscribe(page => expect(page.items.length).toBe(1));
    const request = http.expectOne(`${base}?limit=100`);
    expect(request.request.method).toBe('GET');
    request.flush({ items: [{ id: 'n1', kind: 'TaskOverdue', leadId: 'l1', title: 'Tarefa', dueAt: '2026-09-01T10:00:00.000Z', readAt: null, createdAt: '2026-09-01T09:00:00.000Z' }], nextCursor: null });
    http.verify();
  });

  it('loads a page after a given cursor', () => {
    TestBed.configureTestingModule({ providers: [NotificationService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(NotificationService);
    const http = TestBed.inject(HttpTestingController);
    service.list('abc-123', 100).subscribe(page => expect(page.items.length).toBe(0));
    const request = http.expectOne(`${base}?limit=100&cursor=abc-123`);
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], nextCursor: null });
    http.verify();
  });

  it('maps the unread count from the count endpoint', () => {
    TestBed.configureTestingModule({ providers: [NotificationService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(NotificationService);
    const http = TestBed.inject(HttpTestingController);
    service.unreadCount().subscribe(count => expect(count).toBe(2));
    const request = http.expectOne(`${base}/unread-count`);
    expect(request.request.method).toBe('GET');
    request.flush({ count: 2 });
    http.verify();
  });

  it('marks a single notification as read', () => {
    TestBed.configureTestingModule({ providers: [NotificationService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(NotificationService);
    const http = TestBed.inject(HttpTestingController);
    service.markRead('n1').subscribe();
    const request = http.expectOne(`${base}/n1/read`);
    expect(request.request.method).toBe('PATCH');
    request.flush(null);
    http.verify();
  });

  it('marks all notifications as read', () => {
    TestBed.configureTestingModule({ providers: [NotificationService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(NotificationService);
    const http = TestBed.inject(HttpTestingController);
    service.markAllRead().subscribe();
    const request = http.expectOne(`${base}/read-all`);
    expect(request.request.method).toBe('PATCH');
    request.flush(null);
    http.verify();
  });
});
