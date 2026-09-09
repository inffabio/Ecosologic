import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { LeadDetail } from './lead-detail';

describe('LeadDetail', () => {
  let http: HttpTestingController;
  const route = { snapshot: { paramMap: { get: () => 'lead-id' } } };
  const lead = {
    id: 'lead-id', name: 'Fabio', phone: '999', email: '', message: 'Orçamento',
    stage: 'New', notes: '', createdAt: '2026-09-01T10:00:00.000Z', reminderAt: null
  };
  const taskUrl = 'http://localhost:5157/api/leads/lead-id/tasks?includeCompleted=true';

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LeadDetail],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: ActivatedRoute, useValue: route }]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function flushLead(activities: unknown[] = []) {
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush(activities);
    http.expectOne(taskUrl).flush([]);
  }

  function task(id: string, title: string, dueAt: string, completedAt: string | null = null) {
    return { id, leadId: 'lead-id', title, description: null, dueAt, completedAt, createdAt: '2026-09-02T00:00:00.000Z', updatedAt: '2026-09-02T00:00:00.000Z' };
  }

  it('renders the activity timeline and accessible reminder/note labels', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([
      { id: 'a1', leadId: 'lead-id', type: 'Note', description: 'Cliente pediu retorno', createdAt: '2026-09-02T10:00:00.000Z' },
      { id: 'a2', leadId: 'lead-id', type: 'StageChanged', description: 'Etapa alterada de New para Contacted', createdAt: '2026-09-02T09:00:00.000Z' }
    ]);
    http.expectOne(taskUrl).flush([]);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('.timeline-item').length).toBe(2);
    expect(el.querySelector('#reminder')).toBeTruthy();
    expect(el.querySelector('label[for="reminder"]')?.textContent).toContain('Lembrete');
    expect(el.querySelector('#new-note')).toBeTruthy();
    expect(el.querySelector('label[for="new-note"]')?.textContent).toContain('Adicionar nota');
  });

  it('posts a note and shows a success message', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([]);
    http.expectOne(taskUrl).flush([]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;
    cmp.newNote.set('Uma nova nota');
    cmp.addNote();
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/activities');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.description).toBe('Uma nova nota');
    request.flush({ id: 'a3', leadId: 'lead-id', type: 'Note', description: 'Uma nova nota', createdAt: '2026-09-02T10:00:00.000Z' });
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.saved')?.textContent).toContain('Nota');
  });

  it('shows an empty state message when there are no activities', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([]);
    http.expectOne(taskUrl).flush([]);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Nenhuma atividade registrada.');
  });

  it('clears the activities error after a successful reload', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush(null, { status: 500, statusText: 'Server error' });
    http.expectOne(taskUrl).flush([]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;
    expect(cmp.activitiesError()).toContain('Não foi possível carregar o histórico');

    cmp.save();
    const patch = http.expectOne('http://localhost:5157/api/leads/lead-id');
    expect(patch.request.method).toBe('PATCH');
    patch.flush({ ...lead, stage: 'New' });
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([]);
    fixture.detectChanges();

    expect(cmp.activitiesError()).toBe('');
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).not.toContain('Não foi possível carregar o histórico');
  });

  it('renders tasks with overdue, today and upcoming badges', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([]);
    const now = Date.now();
    http.expectOne(taskUrl).flush([
      { id: 't1', leadId: 'lead-id', title: 'Vencida', dueAt: new Date(now - 86400000).toISOString(), completedAt: null, createdAt: '2026-09-02', updatedAt: '2026-09-02' },
      { id: 't2', leadId: 'lead-id', title: 'Hoje', dueAt: new Date(now).toISOString(), completedAt: null, createdAt: '2026-09-02', updatedAt: '2026-09-02' },
      { id: 't3', leadId: 'lead-id', title: 'Próxima', dueAt: new Date(now + 86400000).toISOString(), completedAt: null, createdAt: '2026-09-02', updatedAt: '2026-09-02' }
    ]);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const badges = Array.from(el.querySelectorAll('.task-badge'));
    expect(badges.length).toBe(3);
    expect(badges.map(b => b.textContent)).toEqual(['Vencida', 'Hoje', 'Próxima']);
    expect(el.querySelector('input[type="checkbox"]')).toBeTruthy();
    expect(el.querySelector('.task-delete')?.getAttribute('aria-label')).toContain('Excluir tarefa');
  });

  it('creates a task via the form', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([]);
    http.expectOne(taskUrl).flush([]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;
    cmp.newTaskTitle.set('Ligar para o cliente');
    cmp.newTaskDescription.set('Retornar ligação');
    cmp.newTaskDue.set('2026-09-03T10:00');
    cmp.addTask();
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/tasks');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.title).toBe('Ligar para o cliente');
    expect(request.request.body.description).toBe('Retornar ligação');
    expect(request.request.body.dueAt).toBeTruthy();
    request.flush({ id: 't1', leadId: 'lead-id', title: 'Ligar para o cliente', description: 'Retornar ligação', dueAt: '2026-09-03T10:00:00.000Z', completedAt: null, createdAt: '2026-09-02', updatedAt: '2026-09-02' });
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('.task-item').length).toBe(1);
  });

  it('toggles a task to complete', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([]);
    http.expectOne(taskUrl).flush([
      { id: 't1', leadId: 'lead-id', title: 'Ligar', dueAt: new Date(Date.now() + 86400000).toISOString(), completedAt: null, createdAt: '2026-09-02', updatedAt: '2026-09-02' }
    ]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;
    cmp.toggleTask(cmp.tasks()[0]);
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/tasks/t1');
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body.completed).toBe(true);
    request.flush({ id: 't1', leadId: 'lead-id', title: 'Ligar', dueAt: new Date(Date.now() + 86400000).toISOString(), completedAt: '2026-09-02T10:00:00.000Z', createdAt: '2026-09-02', updatedAt: '2026-09-02' });
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.task-badge')?.textContent).toBe('Concluída');
  });

  it('deletes a task', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([]);
    http.expectOne(taskUrl).flush([
      { id: 't1', leadId: 'lead-id', title: 'Ligar', dueAt: new Date(Date.now() + 86400000).toISOString(), completedAt: null, createdAt: '2026-09-02', updatedAt: '2026-09-02' }
    ]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;
    cmp.deleteTask(cmp.tasks()[0]);
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/tasks/t1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('.task-item').length).toBe(0);
    expect(el.textContent).toContain('Nenhuma tarefa registrada.');
  });

  it('shows an empty state message when there are no tasks', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([]);
    http.expectOne(taskUrl).flush([]);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Nenhuma tarefa registrada.');
  });

  it('reorders tasks after toggling one to complete', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([]);
    http.expectOne(taskUrl).flush([
      task('t1', 'Cedo', '2026-09-03T10:00:00.000Z'),
      task('t2', 'Tarde', '2026-09-05T10:00:00.000Z')
    ]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;

    cmp.toggleTask(cmp.tasks()[0]);
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/tasks/t1');
    request.flush({ ...task('t1', 'Cedo', '2026-09-03T10:00:00.000Z'), completedAt: '2026-09-02T10:00:00.000Z' });

    expect(cmp.tasks().map(t => t.id)).toEqual(['t2', 't1']);
  });

  it('reorders tasks after creating one', () => {
    const fixture = TestBed.createComponent(LeadDetail);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/leads/lead-id').flush(lead);
    http.expectOne('http://localhost:5157/api/leads/lead-id/activities').flush([]);
    http.expectOne(taskUrl).flush([
      task('t1', 'Tarde', '2026-09-05T10:00:00.000Z')
    ]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;

    cmp.newTaskTitle.set('Cedo');
    cmp.newTaskDue.set('2026-09-03T10:00');
    cmp.addTask();
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/tasks');
    request.flush(task('t2', 'Cedo', '2026-09-03T10:00:00.000Z'));

    expect(cmp.tasks().map(t => t.id)).toEqual(['t2', 't1']);
  });
});
