import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Notification, NotificationService } from '../notification.service';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: './notification-bell.html',
  styleUrl: './notification-bell.scss'
})
export class NotificationBell implements OnInit {
  private readonly service = inject(NotificationService);
  private readonly pageSize = 100;
  private nextCursor: string | null = null;

  readonly notifications = signal<Notification[]>([]);
  readonly unread = signal(0);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly error = signal('');
  readonly open = signal(false);
  readonly markAllBusy = signal(false);
  readonly hasMore = signal(false);

  ngOnInit() {
    this.service.unreadCount().subscribe({ next: count => this.unread.set(count) });
  }

  toggle() {
    const next = !this.open();
    this.open.set(next);
    if (next) this.reload();
  }

  close() {
    this.open.set(false);
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.close();
  }

  reload() {
    this.loading.set(true);
    this.error.set('');
    this.service.list(null, this.pageSize).subscribe({
      next: page => {
        this.notifications.set(page.items);
        this.nextCursor = page.nextCursor;
        this.hasMore.set(page.nextCursor !== null);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as notificações.');
        this.loading.set(false);
      }
    });
  }

  loadMore() {
    if (this.loadingMore()) return;
    const cursor = this.nextCursor;
    if (!cursor) return;
    this.loadingMore.set(true);
    this.error.set('');
    this.service.list(cursor, this.pageSize).subscribe({
      next: page => {
        this.notifications.update(list => [...list, ...page.items]);
        this.nextCursor = page.nextCursor;
        this.hasMore.set(page.nextCursor !== null);
        this.loadingMore.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar mais notificações.');
        this.loadingMore.set(false);
      }
    });
  }

  markRead(item: Notification) {
    if (item.readAt) return;
    this.service.markRead(item.id).subscribe({
      next: () => {
        const now = new Date().toISOString();
        this.notifications.update(list => list.map(n => n.id === item.id ? { ...n, readAt: now } : n));
        this.unread.update(count => Math.max(0, count - 1));
      },
      error: () => this.error.set('Não foi possível marcar como lida.')
    });
  }

  markAllRead() {
    if (this.unread() === 0 || this.markAllBusy()) return;
    this.markAllBusy.set(true);
    this.service.markAllRead().subscribe({
      next: () => {
        const now = new Date().toISOString();
        this.notifications.update(list => list.map(n => n.readAt ? n : { ...n, readAt: now }));
        this.unread.set(0);
        this.markAllBusy.set(false);
      },
      error: () => {
        this.error.set('Não foi possível marcar todas como lidas.');
        this.markAllBusy.set(false);
      }
    });
  }

  kindLabel(kind: Notification['kind']): string {
    return ({ TaskOverdue: 'Tarefa vencida', TaskDueToday: 'Vence hoje', LeadReminder: 'Lembrete' } as Record<string, string>)[kind] ?? kind;
  }
}
