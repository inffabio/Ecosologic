import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ContentEditor } from './content-editor';

describe('ContentEditor', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ContentEditor],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const base = {
    heroTitle: 'T', heroText: 'X', heroImageUrl: 'img.jpg', contactEmail: 'a@b.com', contactPhone: '1',
    solutions: [{ title: 'Residencial', text: 'Texto' }],
    processSteps: [{ title: 'Diagnóstico', text: 'Texto' }],
    projects: [{ title: 'Usina', category: 'Residencial · RJ', power: '5 kWp', imageUrl: 'p.jpg', alt: 'alt' }]
  };

  it('loads content and renders solutions, steps and projects', () => {
    const fixture = TestBed.createComponent(ContentEditor);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush(base);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')?.textContent).toContain('Conteúdo da Home');
    expect(el.querySelectorAll('.item').length).toBe(3);
  });

  it('caps solutions at four', () => {
    const fixture = TestBed.createComponent(ContentEditor);
    const cmp = fixture.componentInstance;
    cmp.content.solutions = [];
    for (let i = 0; i < 5; i++) cmp.addSolution();
    expect(cmp.content.solutions.length).toBe(4);
  });

  it('adds and removes projects up to fifty', () => {
    const fixture = TestBed.createComponent(ContentEditor);
    const cmp = fixture.componentInstance;
    cmp.content.projects = [];
    for (let i = 0; i < 50; i++) cmp.addProject();
    expect(cmp.content.projects.length).toBe(50);
    cmp.addProject();
    expect(cmp.content.projects.length).toBe(50);
    cmp.removeProject(1);
    expect(cmp.content.projects.length).toBe(49);
  });
});
