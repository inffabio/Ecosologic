import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ContentService } from './content.service';

describe('ContentService', () => {
  let service: ContentService;
  let http: HttpTestingController;

  const fullContent = {
    heroTitle: 'Título',
    heroText: 'Texto',
    heroImageUrl: 'image.jpg',
    contactEmail: 'a@b.com',
    contactPhone: '123',
    solutions: [{ title: 'Residencial', text: 'Texto' }],
    processSteps: [{ title: 'Diagnóstico', text: 'Texto' }],
    projects: [{ title: 'Usina', category: 'Residencial · RJ', power: '5,5 kWp', imageUrl: 'p.jpg', alt: 'Alt' }]
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ContentService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ContentService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the public home content', () => {
    service.getPublicHome().subscribe(content => {
      expect(content.heroTitle).toBe('Título');
      expect(content.solutions.length).toBe(1);
      expect(content.projects[0].imageUrl).toBe('p.jpg');
    });

    const request = http.expectOne('http://localhost:5157/api/content/home');
    expect(request.request.method).toBe('GET');
    request.flush(fullContent);
  });

  it('updates home content including lists', () => {
    service.updateHome(fullContent).subscribe(content => expect(content.heroTitle).toBe('Título'));

    const request = http.expectOne('http://localhost:5157/api/content/home');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(fullContent);
    request.flush({ ...fullContent, updatedAt: '2026-09-01T00:00:00Z' });
  });

  it('uploads media as a multipart POST', () => {
    const file = new File(['image'], 'photo.png', { type: 'image/png' });

    service.uploadMedia(file).subscribe(res => expect(res.url).toBe('/uploads/abc.png'));

    const request = http.expectOne('http://localhost:5157/api/content/media');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeInstanceOf(FormData);
    const formData = request.request.body as FormData;
    expect(formData.get('file')).toEqual(file);
    request.flush({ url: '/uploads/abc.png' });
  });
});
