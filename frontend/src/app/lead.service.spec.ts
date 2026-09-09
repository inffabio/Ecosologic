import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { LeadService } from './lead.service';

describe('LeadService', () => {
  it('posts a contact as a lead', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.create({ name: 'Fabio', phone: '+5521995424027', email: 'fabio@ecosologic.com.br', message: 'Orçamento' }).subscribe();
    const request = http.expectOne('http://localhost:5157/api/leads');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.name).toBe('Fabio');
    request.flush({ id: 'lead-id' });
    http.verify();
  });

  it('loads leads for the authenticated dashboard', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.list().subscribe(response => expect(response.items.length).toBe(1));
    const request = http.expectOne('http://localhost:5157/api/leads');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [{ id: '1', name: 'Fabio', phone: '999', email: '', message: '', stage: 'New', createdAt: '2026-09-01' }], total: 1, page: 1, pageSize: 20, totalPages: 1 });
    http.verify();
  });

  it('sends query params for search, stage and pagination', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.list({ q: 'ana', stage: 'New', page: 2, pageSize: 10 }).subscribe(response => expect(response.total).toBe(0));
    const request = http.expectOne('http://localhost:5157/api/leads?q=ana&stage=New&page=2&pageSize=10');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], total: 0, page: 2, pageSize: 10, totalPages: 0 });
    http.verify();
  });

  it('omits empty query params when listing', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.list({}).subscribe();
    const request = http.expectOne('http://localhost:5157/api/leads');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 });
    http.verify();
  });

  it('loads stage counts for the metrics cards', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.stageCounts().subscribe(counts => expect(counts['New']).toBe(3));
    const request = http.expectOne('http://localhost:5157/api/leads/stage-counts');
    expect(request.request.method).toBe('GET');
    request.flush({ New: 3, Won: 1 });
    http.verify();
  });

  it('patches the stage and notes of a lead', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.update('lead-id', { stage: 'Contacted', notes: 'Ligado' }).subscribe(lead => expect(lead.stage).toBe('Contacted'));
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id');
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body.stage).toBe('Contacted');
    expect(request.request.body.notes).toBe('Ligado');
    request.flush({ id: 'lead-id', name: 'Fabio', phone: '999', email: '', message: '', stage: 'Contacted', notes: 'Ligado', createdAt: '2026-09-01', updatedAt: '2026-09-02' });
    http.verify();
  });

  it('patches the reminderAt of a lead', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.update('lead-id', { reminderAt: '2026-09-03T10:00:00.000Z' }).subscribe(lead => expect(lead.reminderAt).toBe('2026-09-03T10:00:00.000Z'));
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id');
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body.reminderAt).toBe('2026-09-03T10:00:00.000Z');
    request.flush({ id: 'lead-id', name: 'Fabio', phone: '999', email: '', message: '', stage: 'New', createdAt: '2026-09-01', reminderAt: '2026-09-03T10:00:00.000Z' });
    http.verify();
  });

  it('loads the activities of a lead', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.activities('lead-id').subscribe(items => expect(items.length).toBe(1));
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/activities');
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 'a1', leadId: 'lead-id', type: 'Note', description: 'Nota', createdAt: '2026-09-02' }]);
    http.verify();
  });

  it('posts an activity for a lead', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.addActivity('lead-id', { type: 'Note', description: 'Nota' }).subscribe(activity => expect(activity.description).toBe('Nota'));
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/activities');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.type).toBe('Note');
    expect(request.request.body.description).toBe('Nota');
    request.flush({ id: 'a1', leadId: 'lead-id', type: 'Note', description: 'Nota', createdAt: '2026-09-02' });
    http.verify();
  });

  it('loads the tasks of a lead', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.tasks('lead-id').subscribe(items => expect(items.length).toBe(1));
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/tasks');
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 't1', leadId: 'lead-id', title: 'Ligar', dueAt: '2026-09-03T10:00:00.000Z', completedAt: null, createdAt: '2026-09-02', updatedAt: '2026-09-02' }]);
    http.verify();
  });

  it('includes completed tasks when requested', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.tasks('lead-id', true).subscribe(items => expect(items.length).toBe(0));
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/tasks?includeCompleted=true');
    expect(request.request.method).toBe('GET');
    request.flush([]);
    http.verify();
  });

  it('creates a task for a lead', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.createTask('lead-id', { title: 'Ligar', description: 'Retornar', dueAt: '2026-09-03T10:00:00.000Z' }).subscribe(task => expect(task.title).toBe('Ligar'));
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/tasks');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.title).toBe('Ligar');
    expect(request.request.body.description).toBe('Retornar');
    expect(request.request.body.dueAt).toBe('2026-09-03T10:00:00.000Z');
    request.flush({ id: 't1', leadId: 'lead-id', title: 'Ligar', description: 'Retornar', dueAt: '2026-09-03T10:00:00.000Z', completedAt: null, createdAt: '2026-09-02', updatedAt: '2026-09-02' });
    http.verify();
  });

  it('patches the completed flag of a task', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.updateTask('lead-id', 'task-id', true).subscribe(task => expect(task.completedAt).toBeTruthy());
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/tasks/task-id');
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body.completed).toBe(true);
    request.flush({ id: 'task-id', leadId: 'lead-id', title: 'Ligar', dueAt: '2026-09-03T10:00:00.000Z', completedAt: '2026-09-02T10:00:00.000Z', createdAt: '2026-09-02', updatedAt: '2026-09-02' });
    http.verify();
  });

  it('deletes a task', () => {
    TestBed.configureTestingModule({ providers: [LeadService, provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LeadService);
    const http = TestBed.inject(HttpTestingController);
    service.deleteTask('lead-id', 'task-id').subscribe();
    const request = http.expectOne('http://localhost:5157/api/leads/lead-id/tasks/task-id');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    http.verify();
  });
});
