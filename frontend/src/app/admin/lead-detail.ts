import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Lead, LeadActivity, LeadService, LeadTask } from '../lead.service';

@Component({ selector: 'app-lead-detail', standalone: true, imports: [RouterLink, DatePipe], templateUrl: './lead-detail.html', styleUrl: './lead-detail.scss' })
export class LeadDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(LeadService);
  readonly lead = signal<Lead | null>(null);
  readonly error = signal('');
  readonly stage = signal('');
  readonly note = signal('');
  readonly reminder = signal('');
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly saveError = signal('');
  readonly activities = signal<LeadActivity[]>([]);
  readonly activitiesLoading = signal(true);
  readonly activitiesError = signal('');
  readonly newNote = signal('');
  readonly addingNote = signal(false);
  readonly noteSaved = signal(false);
  readonly addNoteError = signal('');
  readonly tasks = signal<LeadTask[]>([]);
  readonly tasksLoading = signal(true);
  readonly tasksError = signal('');
  readonly newTaskTitle = signal('');
  readonly newTaskDescription = signal('');
  readonly newTaskDue = signal('');
  readonly addingTask = signal(false);
  readonly taskAddError = signal('');
  readonly stageOptions = [
    { value: 'New', label: 'Novo lead' },
    { value: 'Contacted', label: 'Em contato' },
    { value: 'DataReceived', label: 'Dados recebidos' },
    { value: 'Dimensioning', label: 'Dimensionamento' },
    { value: 'ProposalSent', label: 'Proposta enviada' },
    { value: 'Negotiation', label: 'Negociação' },
    { value: 'Won', label: 'Fechado' },
    { value: 'Lost', label: 'Perdido' }
  ];

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.error.set('Lead não encontrado.'); return; }
    this.service.get(id).subscribe({
      next: lead => {
        this.lead.set(lead);
        this.stage.set(lead.stage);
        this.note.set(lead.notes ?? '');
        this.reminder.set(this.toDateTimeLocal(lead.reminderAt));
        this.reloadActivities();
        this.reloadTasks();
      },
      error: () => this.error.set('Não foi possível carregar este lead.')
    });
  }

  save() {
    const lead = this.lead();
    if (!lead) return;
    this.saving.set(true);
    this.saved.set(false);
    this.saveError.set('');
    this.service.update(lead.id, { stage: this.stage(), notes: this.note(), reminderAt: this.fromDateTimeLocal(this.reminder()) }).subscribe({
      next: updated => {
        this.lead.set(updated);
        this.stage.set(updated.stage);
        this.note.set(updated.notes ?? '');
        this.reminder.set(this.toDateTimeLocal(updated.reminderAt));
        this.saving.set(false);
        this.saved.set(true);
        this.reloadActivities();
      },
      error: () => { this.saving.set(false); this.saveError.set('Não foi possível salvar as alterações.'); }
    });
  }

  addNote(event?: Event) {
    event?.preventDefault();
    const lead = this.lead();
    if (!lead || this.addingNote()) return;
    const description = this.newNote().trim();
    if (!description) return;
    this.addingNote.set(true);
    this.noteSaved.set(false);
    this.addNoteError.set('');
    this.service.addActivity(lead.id, { type: 'Note', description }).subscribe({
      next: activity => {
        this.activities.set([activity, ...this.activities()]);
        this.newNote.set('');
        this.addingNote.set(false);
        this.noteSaved.set(true);
      },
      error: () => { this.addingNote.set(false); this.addNoteError.set('Não foi possível adicionar a nota.'); }
    });
  }

  private reloadActivities() {
    const lead = this.lead();
    if (!lead) return;
    this.activitiesLoading.set(true);
    this.service.activities(lead.id).subscribe({
      next: items => { this.activities.set(items); this.activitiesLoading.set(false); this.activitiesError.set(''); },
      error: () => { this.activitiesError.set('Não foi possível carregar o histórico.'); this.activitiesLoading.set(false); }
    });
  }

  private reloadTasks() {
    const lead = this.lead();
    if (!lead) return;
    this.tasksLoading.set(true);
    this.service.tasks(lead.id, true).subscribe({
      next: items => { this.tasks.set(items); this.tasksLoading.set(false); this.tasksError.set(''); },
      error: () => { this.tasksError.set('Não foi possível carregar as tarefas.'); this.tasksLoading.set(false); }
    });
  }

  private compareTasks(a: LeadTask, b: LeadTask): number {
    const aCompleted = a.completedAt ?? null;
    const bCompleted = b.completedAt ?? null;
    if (aCompleted && bCompleted) {
      const byCompleted = Date.parse(bCompleted) - Date.parse(aCompleted);
      if (byCompleted !== 0) return byCompleted;
      const byCreated = Date.parse(b.createdAt) - Date.parse(a.createdAt);
      if (byCreated !== 0) return byCreated;
      return b.id.localeCompare(a.id);
    }
    if (aCompleted) return 1;
    if (bCompleted) return -1;
    const byDue = Date.parse(a.dueAt) - Date.parse(b.dueAt);
    if (byDue !== 0) return byDue;
    const byCreated = Date.parse(a.createdAt) - Date.parse(b.createdAt);
    if (byCreated !== 0) return byCreated;
    return a.id.localeCompare(b.id);
  }

  private sortTasks() {
    this.tasks.set([...this.tasks()].sort((a, b) => this.compareTasks(a, b)));
  }

  addTask(event?: Event) {
    event?.preventDefault();
    const lead = this.lead();
    if (!lead || this.addingTask()) return;
    const title = this.newTaskTitle().trim();
    const dueAt = this.fromDateTimeLocal(this.newTaskDue());
    if (!title || !dueAt) return;
    this.addingTask.set(true);
    this.taskAddError.set('');
    this.service.createTask(lead.id, { title, description: this.newTaskDescription().trim() || undefined, dueAt }).subscribe({
      next: task => {
        this.tasks.update(list => [task, ...list]);
        this.newTaskTitle.set('');
        this.newTaskDescription.set('');
        this.newTaskDue.set('');
        this.addingTask.set(false);
        this.sortTasks();
      },
      error: () => { this.addingTask.set(false); this.taskAddError.set('Não foi possível adicionar a tarefa.'); }
    });
  }

  toggleTask(task: LeadTask) {
    const lead = this.lead();
    if (!lead) return;
    this.service.updateTask(lead.id, task.id, !task.completedAt).subscribe({
      next: updated => {
        this.tasks.update(list => list.map(item => item.id === updated.id ? updated : item));
        this.tasksError.set('');
        this.sortTasks();
      },
      error: () => { this.tasksError.set('Não foi possível atualizar a tarefa.'); }
    });
  }

  deleteTask(task: LeadTask) {
    const lead = this.lead();
    if (!lead) return;
    this.service.deleteTask(lead.id, task.id).subscribe({
      next: () => {
        this.tasks.update(list => list.filter(item => item.id !== task.id));
        this.tasksError.set('');
        this.sortTasks();
      },
      error: () => { this.tasksError.set('Não foi possível excluir a tarefa.'); }
    });
  }

  dueLabel(task: LeadTask): string {
    if (task.completedAt) return 'Concluída';
    const due = new Date(task.dueAt);
    if (isNaN(due.getTime())) return '';
    const now = new Date();
    const start = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const end = new Date(start.getTime() + 86400000);
    if (due.getTime() < start.getTime()) return 'Vencida';
    if (due.getTime() < end.getTime()) return 'Hoje';
    return 'Próxima';
  }

  dueClass(task: LeadTask): string {
    if (task.completedAt) return 'completed';
    const due = new Date(task.dueAt);
    if (isNaN(due.getTime())) return 'upcoming';
    const now = new Date();
    const start = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const end = new Date(start.getTime() + 86400000);
    if (due.getTime() < start.getTime()) return 'overdue';
    if (due.getTime() < end.getTime()) return 'today';
    return 'upcoming';
  }

  private toDateTimeLocal(value?: string | null): string {
    if (!value) return '';
    const date = new Date(value);
    if (isNaN(date.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  private fromDateTimeLocal(value: string): string | null {
    if (!value) return null;
    const date = new Date(value);
    if (isNaN(date.getTime())) return null;
    return date.toISOString();
  }
}
