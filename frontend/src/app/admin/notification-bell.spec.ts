import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { NotificationBell } from './notification-bell';
import { Notification } from '../notification.service';

describe('NotificationBell', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotificationBell],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const countUrl = 'http://localhost:5157/api/notifications/unread-count';
  const listUrl = 'http://localhost:5157/api/notifications?limit=100';

  function notification(id: string, overrides: Partial<Notification> = {}): Notification {
    return {
      id,
      kind: 'TaskOverdue',
      leadId: 'lead-1',
      title: 'Tarefa vencida: Ligar',
      dueAt: '2026-09-01T10:00:00.000Z',
      readAt: null,
      createdAt: '2026-09-01T09:00:00.000Z',
      ...overrides
    };
  }

  function flushCount(count = 0) {
    http.expectOne(countUrl).flush({ count });
  }

  function flushList(items: Notification[], nextCursor: string | null = null) {
    http.expectOne(listUrl).flush({ items, nextCursor });
  }

  it('shows the unread count as a badge', () => {
    const fixture = TestBed.createComponent(NotificationBell);
    fixture.detectChanges();
    flushCount(1);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.badge')?.textContent).toBe('1');
    expect(el.querySelector('.bell-button')?.getAttribute('aria-expanded')).toBe('false');
  });

  it('shows the full unread count even above 99', () => {
    const fixture = TestBed.createComponent(NotificationBell);
    fixture.detectChanges();
    flushCount(150);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.badge')?.textContent).toBe('150');
  });

  it('opens the tray and lists notifications', () => {
    const fixture = TestBed.createComponent(NotificationBell);
    fixture.detectChanges();
    flushCount();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('.bell-button')?.dispatchEvent(new Event('click'));
    fixture.detectChanges();

    flushList([notification('n1'), notification('n2', { kind: 'LeadReminder', readAt: '2026-09-01T10:00:00.000Z' })]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.panel')).toBeTruthy();
    expect(el.querySelectorAll('.list li').length).toBe(2);
    expect(el.textContent).toContain('Tarefa vencida: Ligar');
    expect(el.querySelector('.bell-button')?.getAttribute('aria-expanded')).toBe('true');
  });

  it('mentions the tray limit of 100 notifications', () => {
    const fixture = TestBed.createComponent(NotificationBell);
    fixture.detectChanges();
    flushCount();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('.bell-button')?.dispatchEvent(new Event('click'));
    fixture.detectChanges();
    flushList([notification('n1')]);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('100');
  });

  it('shows an empty state message', () => {
    const fixture = TestBed.createComponent(NotificationBell);
    fixture.detectChanges();
    flushCount();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('.bell-button')?.dispatchEvent(new Event('click'));
    fixture.detectChanges();
    flushList([]);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Nenhuma notificação.');
  });

  it('loads more notifications using the stable cursor', () => {
    const fixture = TestBed.createComponent(NotificationBell);
    fixture.detectChanges();
    flushCount();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('.bell-button')?.dispatchEvent(new Event('click'));
    fixture.detectChanges();
    const items = Array.from({ length: 100 }, (_, i) => notification(`n${i}`));
    flushList(items, 'cursor-1');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.load-more')).toBeTruthy();

    el.querySelector('.load-more')?.dispatchEvent(new Event('click'));
    const request = http.expectOne('http://localhost:5157/api/notifications?limit=100&cursor=cursor-1');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [notification('n100')], nextCursor: null });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.list li').length).toBe(101);
  });

  it('marks a single notification as read', () => {
    const fixture = TestBed.createComponent(NotificationBell);
    fixture.detectChanges();
    flushCount();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('.bell-button')?.dispatchEvent(new Event('click'));
    fixture.detectChanges();
    flushList([notification('n1')]);
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('.mark-read')?.dispatchEvent(new Event('click'));
    const request = http.expectOne('http://localhost:5157/api/notifications/n1/read');
    expect(request.request.method).toBe('PATCH');
    request.flush(null);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.badge')).toBeFalsy();
    expect(el.querySelector('.mark-read')).toBeFalsy();
  });

  it('marks all notifications as read', () => {
    const fixture = TestBed.createComponent(NotificationBell);
    fixture.detectChanges();
    flushCount(2);
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('.bell-button')?.dispatchEvent(new Event('click'));
    fixture.detectChanges();
    flushList([notification('n1'), notification('n2')]);
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('.mark-all')?.dispatchEvent(new Event('click'));
    const request = http.expectOne('http://localhost:5157/api/notifications/read-all');
    expect(request.request.method).toBe('PATCH');
    request.flush(null);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.badge')).toBeFalsy();
  });
});
