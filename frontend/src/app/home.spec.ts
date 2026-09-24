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
    expect(compiled.querySelector('h1')?.textContent?.toLowerCase()).toContain('energia');
    const titles = Array.from(compiled.querySelectorAll('.solution-list h3')).map(
      (el) => el.textContent,
    );
    expect(titles).toContain('Residencial');
    expect(titles).toContain('Agronegócio');
    const video = compiled.querySelector('video.hybrid-video') as HTMLVideoElement;
    expect(video?.getAttribute('src')).toBe('assets/video.mp4');
    expect(video?.hasAttribute('muted')).toBeTrue();
    expect(video?.hasAttribute('loop')).toBeTrue();
    expect(video?.hasAttribute('autoplay')).toBeTrue();
    expect(video?.hasAttribute('controls')).toBeFalse();
    expect(video?.hasAttribute('poster')).toBeFalse();
    expect(compiled.querySelector('.hero-bg')?.getAttribute('src')).toBe(
      'assets/hero-solar-garage-battery.png',
    );
    expect(compiled.querySelector('.hybrid h2')?.textContent).toContain('bateria de lítio');
    expect(compiled.querySelector('.header-cta')?.getAttribute('href')).toContain('5521965847684');
    const floatingWhatsapp = compiled.querySelector('.floating-whatsapp') as HTMLAnchorElement;
    expect(floatingWhatsapp?.getAttribute('title')).toBe('Contato');
    expect(floatingWhatsapp?.getAttribute('href')).toContain('5521965847684');
    expect(compiled.querySelector('.executive-slab')?.textContent).toContain(
      'Diagnóstico comercial',
    );
    expect(compiled.querySelector('.solar-command')?.textContent).toContain(
      'Projeto solar com cara de investimento',
    );
    expect(compiled.querySelector('.upload-card')?.textContent).toContain('Anexar conta de luz');
    expect(compiled.querySelector('#bill')?.getAttribute('type')).toBe('file');
    expect(compiled.querySelector('#email')?.getAttribute('type')).toBe('email');
    expect(compiled.querySelector('.contact-details')).toBeNull();
    expect(compiled.querySelector('.contact-copy')?.textContent).toContain('Solicite um orçamento');
    expect(compiled.querySelector('.contact-copy')?.textContent).toContain(
      'centenas de instalações',
    );
    expect(compiled.querySelector('.hero-wave path')?.getAttribute('fill')).toBe('#061414');
    expect(compiled.querySelectorAll('.section-wave').length).toBe(6);
    expect(compiled.querySelector('.hybrid .section-kicker')).toBeNull();
    expect(compiled.querySelector('.project-feature')).toBeNull();
    expect(compiled.querySelector('.video-frame p')).toBeNull();
  });

  it('renders project thumbnails and opens the lightbox', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush({});
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const thumbnails = compiled.querySelectorAll('.project-thumbnail');
    expect(thumbnails.length).toBe(22);
    expect(
      new Set(
        Array.from(compiled.querySelectorAll('.project-thumbnail img')).map((img) =>
          img.getAttribute('src'),
        ),
      ).size,
    ).toBe(22);
    (thumbnails[1] as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(compiled.querySelector('.project-lightbox')).not.toBeNull();
    (compiled.querySelector('.project-lightbox-close') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(compiled.querySelector('.project-lightbox')).toBeNull();
  });

  it('marks the project rail for native touch scrolling', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush({});
    fixture.detectChanges();

    expect(
      fixture.nativeElement.querySelector('.project-thumbnails')?.getAttribute('data-touch-scroll'),
    ).toBe('true');
  });

  it('renders projects from the content payload', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush({
      heroTitle: 'Título',
      heroText: 'Texto',
      heroImageUrl: 'img.jpg',
      contactEmail: 'a@b.com',
      contactPhone: '123',
      solutions: [{ title: 'S1', text: 'd' }],
      processSteps: [{ title: 'P1', text: 'd' }],
      projects: [
        {
          title: 'Usina nova',
          category: 'Comercial · RJ',
          power: '10 kWp',
          imageUrl: 'x.jpg',
          alt: 'Alt novo',
        },
      ],
    });
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const img = compiled.querySelector('.project-thumbnail img') as HTMLImageElement;
    expect(img?.getAttribute('alt')).toBe('Alt novo');
    expect(compiled.querySelector('.project-feature')).toBeNull();
    expect(compiled.querySelector('.project-thumbnail img')?.getAttribute('alt')).toBe('Alt novo');
  });

  it('uses the local gallery when the API still returns the legacy project seed', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush({
      heroTitle: 'Título',
      heroText: 'Texto',
      heroImageUrl: 'img.jpg',
      contactEmail: 'a@b.com',
      contactPhone: '123',
      solutions: [],
      processSteps: [],
      projects: [
        {
          title: 'Antigo 1',
          category: 'Residencial',
          power: '1 kWp',
          imageUrl: 'assets/projects/02-solar.jpg',
          alt: 'Alt 1',
        },
        {
          title: 'Antigo 2',
          category: 'Residencial',
          power: '1 kWp',
          imageUrl: 'assets/projects/03-solar.jpg',
          alt: 'Alt 2',
        },
        {
          title: 'Antigo 3',
          category: 'Residencial',
          power: '1 kWp',
          imageUrl: 'assets/projects/04-solar.jpg',
          alt: 'Alt 3',
        },
      ],
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.project-thumbnail').length).toBe(22);
  });
  it('renders promotional solar kit carousel', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush({});
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const offers = Array.from(compiled.querySelectorAll('.offer-card'));
    expect(offers.length).toBe(3);
    expect(compiled.querySelector('.offers')?.textContent).toContain('600 kWh/mês');
    expect(compiled.querySelector('.offers')?.textContent).toContain('R$ 10.752,89');
    expect(compiled.querySelector('.offers')?.textContent).toContain('800 kWh/mês');
    expect(compiled.querySelector('.offers')?.textContent).toContain('R$ 14.694,65');
    expect(compiled.querySelector('.offers')?.textContent).toContain('1000 kWh/mês');
    expect(compiled.querySelector('.offers')?.textContent).toContain('R$ 18.354,00');
    expect(compiled.querySelector('.offers')?.textContent).toContain('Ampliação de potência');
    expect(compiled.querySelector('.offers')?.textContent).toContain('Vantagens do microinversor');
    expect(compiled.querySelector('.offers')?.textContent).toContain('1 estrutura para os módulos');
    expect(compiled.querySelector('.offers')?.textContent).toContain('1 stringbox');
    expect(compiled.querySelector('.offers')?.textContent).toContain(
      'Monitoramento de cada módulo',
    );
    expect(compiled.querySelector('.offers')?.textContent).toContain('SUPER OFERTA');
    expect(compiled.querySelector('.offers')?.textContent).toContain('Além do equipamento');
    expect(compiled.querySelectorAll('.offer-cta').length).toBe(3);
    expect(compiled.querySelector('.offer-carousel')?.getAttribute('tabindex')).toBe('0');
    expect(compiled.querySelector('.offer-carousel')?.getAttribute('aria-label')).toContain('Ofertas');
    expect(compiled.querySelectorAll('.offer-chevron').length).toBe(2);
    expect(Array.from(compiled.querySelectorAll('.offer-card img')).every(image => image.getAttribute('src'))).toBeTrue();
    expect(compiled.querySelector('.offer-panel-img')?.getAttribute('src')).toBe(
      'assets/offers/modulo-solar.png',
    );
    expect(compiled.querySelector('.microinverter-photo')?.getAttribute('src')).toBe(
      'assets/offers/microinversor-deye.png',
    );
  });

  it('formats Brazilian phone numbers for the contact form', () => {
    const fixture = TestBed.createComponent(Home);
    const home = fixture.componentInstance;

    expect(home.formatPhone('21999999999')).toBe('(21) 99999-9999');
    expect(home.formatPhone('2133334444')).toBe('(21) 3333-4444');
    expect(home.formatPhone('(21) 99999-9999 ext. 2')).toBe('(21) 99999-9999');
  });

  it('prepares the contact form for the selected promotional kit', () => {
    const fixture = TestBed.createComponent(Home);
    const home = fixture.componentInstance;

    home.selectOffer('1000', 'Kit 1000 kWh/mês com 13 módulos solares 620 Wp');

    expect(home.contact.message).toBe(
      'Olá, gostaria de solicitar uma avaliação do Kit 1000 kWh/mês com 13 módulos solares 620 Wp.',
    );
  });

  it('keeps the 13-module title together in the offer card', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush({});
    fixture.detectChanges();

    const titles = Array.from(
      fixture.nativeElement.querySelectorAll('.offer-copy h3'),
    ) as HTMLElement[];
    expect(titles[2].querySelector('span')?.textContent).toBe('13 módulos solares 620 Wp');
  });

  it('moves the yellow highlight to the promotional kit card that is clicked', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    http.expectOne('http://localhost:5157/api/content/home').flush({});
    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelectorAll('.offer-card') as NodeListOf<HTMLElement>;
    cards[0].click();
    fixture.detectChanges();

    expect(fixture.componentInstance.contact.message).toBe('');
    expect(cards[0].classList.contains('is-featured')).toBeTrue();
    expect(cards[1].classList.contains('is-featured')).toBeFalse();
    expect(cards[2].classList.contains('is-featured')).toBeFalse();

    cards[2].click();
    fixture.detectChanges();

    expect(cards[0].classList.contains('is-featured')).toBeFalse();
    expect(cards[2].classList.contains('is-featured')).toBeTrue();
  });
});
