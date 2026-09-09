import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Home } from './home';

describe('Home', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should create the home', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush({});
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the Ecosologic hero and fallback solutions', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush({});
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('energia');
    const titles = Array.from(compiled.querySelectorAll('.solution-list h3')).map(el => el.textContent);
    expect(titles).toContain('Residencial');
    expect(titles).toContain('Agronegócio');
  });

  it('renders projects from the content payload', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush({
      heroTitle: 'Título', heroText: 'Texto', heroImageUrl: 'img.jpg', contactEmail: 'a@b.com', contactPhone: '123',
      solutions: [{ title: 'S1', text: 'd' }],
      processSteps: [{ title: 'P1', text: 'd' }],
      projects: [{ title: 'Usina nova', category: 'Comercial · RJ', power: '10 kWp', imageUrl: 'x.jpg', alt: 'Alt novo' }]
    });
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const img = compiled.querySelector('.project-grid img') as HTMLImageElement;
    expect(img?.getAttribute('alt')).toBe('Alt novo');
    expect(compiled.querySelector('.project-title')?.textContent).toContain('Usina nova');
  });
});
