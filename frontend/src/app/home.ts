import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonApp } from '@ionic/angular/ion-app';
import { LeadService } from './lead.service';
import { ContentService, HomeContent } from './content.service';
import { canAnimate } from './motion';

const FALLBACK: HomeContent = {
  heroTitle: 'Energia solar distribuída para reduzir sua conta e proteger seu consumo futuro.',
  heroText: 'Venda consultiva, dimensionamento técnico e soluções híbridas com bateria de lítio para casas e empresas que querem economia, autonomia e previsibilidade.',
  heroImageUrl: 'assets/hero-solar-garage-system.png',
  contactEmail: 'fabio@ecosologic.com.br',
  contactPhone: '+55 (21) 96584-7684',
  solutions: [
    { title: 'Residencial', text: 'Reduza a conta de luz e prepare sua casa para carregar, armazenar e consumir melhor.' },
    { title: 'Comercial', text: 'Transforme energia em previsibilidade financeira para lojas, clínicas, escritórios e galpões.' },
    { title: 'Industrial', text: 'Projetos de maior porte com análise de demanda, retorno e continuidade operacional.' },
    { title: 'Agronegócio', text: 'Geração distribuída para bombas, refrigeração, irrigação e rotinas intensivas de consumo.' }
  ],
  processSteps: [
    { title: 'Diagnóstico', text: 'Analisamos sua conta de luz, rotina de consumo, telhado e objetivo de economia.' },
    { title: 'Dimensionamento', text: 'Dimensionamos módulos, inversor, retorno estimado e, quando fizer sentido, bateria de lítio.' },
    { title: 'Proposta clara', text: 'Apresentamos investimento, payback, equipamentos e próximos passos com clareza.' },
    { title: 'Instalação', text: 'Cuidamos da implantação e orientamos o acompanhamento da geração depois da entrega.' }
  ],
  projects: [
    { title: 'Instalação solar 1', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/1000093004.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 2', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/1000093007.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 3', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/1000141815.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 4', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/dayse-8kw-1000kwh-mes.jpeg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 5', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/img-20190603-101900150.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 6', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/instalacao-placas-joao-03.jpg', alt: 'Instalação de placas solares' },
    { title: 'Instalação solar 7', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/inversor-instalado-joao-6-5kw.jpg', alt: 'Inversor solar instalado' },
    { title: 'Instalação solar 8', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/jorge-01-7kw.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 9', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/jorge-02.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 10', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/lenilson-gd-02.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 11', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/ricardo-01-5-5kw.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 12', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/ricardo-02.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 13', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/sinclar-01-8kw.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 14', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/sinclar-03.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 15', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/sinclar-06.jpg', alt: 'Instalação de energia solar' },
    { title: 'Instalação solar 16', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/telhado-01.jpg', alt: 'Sistema solar instalado em telhado' },
    { title: 'Instalação solar 17', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/telhado-02.jpg', alt: 'Sistema solar instalado em telhado' },
    { title: 'Instalação solar 18', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/modulos-natalia-02.jpg', alt: 'Módulos de energia solar instalados' },
    { title: 'Instalação solar 19', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/modulos-natalia-03.jpg', alt: 'Módulos de energia solar instalados' },
    { title: 'Instalação solar 20', category: 'Instalação solar', power: 'Projeto fotovoltaico', imageUrl: 'assets/projects/inversor-01.jpeg', alt: 'Inversor de energia solar instalado' },
    { title: 'Gerador Carla', category: 'Frame de vídeo', power: 'Registro de instalação', imageUrl: 'assets/projects/gerador-carla-frame.png', alt: 'Frame do vídeo do gerador Carla' },
    { title: 'Sistema Natalia', category: 'Frame de vídeo', power: 'Registro de instalação', imageUrl: 'assets/projects/filmagem-natalia-sistema-frame.png', alt: 'Frame do vídeo do sistema Natalia' }
  ]
};

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [FormsModule, IonApp],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home implements AfterViewInit, OnDestroy {
  readonly isScrolled = signal(false);
  readonly formMessage = signal('');
  readonly selectedBillName = signal('Nenhum arquivo selecionado');
  readonly selectedProject = signal<number | null>(null);
  @ViewChild('projectRail') private projectRail?: ElementRef<HTMLElement>;
  contact = { name: '', phone: '', email: '', message: '' };
  private readonly leads = inject(LeadService);
  private readonly contentService = inject(ContentService);
  private motionContext?: { revert: () => void };
  private destroyed = false;
  private projectDrag?: { startX: number; scrollLeft: number };
  private suppressProjectClick = false;
  readonly content = signal<HomeContent>({ ...FALLBACK });
  ngOnInit() {
    this.contentService.getPublicHome().subscribe({
      next: content => this.content.set({
        ...FALLBACK,
        ...content,
        solutions: content.solutions?.length ? content.solutions : FALLBACK.solutions,
        processSteps: content.processSteps?.length ? content.processSteps : FALLBACK.processSteps,
       projects: this.uniqueProjects(this.isLegacyProjects(content.projects) ? FALLBACK.projects : content.projects?.length ? content.projects : FALLBACK.projects)
      })
    });
  }
  private uniqueProjects(projects: HomeContent['projects']) {
    const seen = new Set<string>();
    return projects.filter(project => {
      const imageUrl = project.imageUrl?.trim().toLowerCase() ?? '';
      if (seen.has(imageUrl)) return false;
      seen.add(imageUrl);
      return true;
    });
  }
  private isLegacyProjects(projects: HomeContent['projects'] | undefined) {
    return projects?.length === 3 && projects.every(project => [
      'assets/projects/02-solar.jpg',
      'assets/projects/03-solar.jpg',
      'assets/projects/04-solar.jpg'
    ].includes(project.imageUrl));
  }
  whatsappUrl() {
    return `https://wa.me/${this.content().contactPhone.replace(/\D/g, '')}`;
  }
  ngAfterViewInit() {
    if (typeof window === 'undefined' || !canAnimate(window.matchMedia('(prefers-reduced-motion: reduce)').matches)) return;

    Promise.all([import('gsap'), import('gsap/ScrollTrigger')]).then(([{ gsap }, { ScrollTrigger }]) => {
      if (this.destroyed) return;
      gsap.registerPlugin(ScrollTrigger);
      this.motionContext = gsap.context(() => {
        const intro = gsap.timeline({ defaults: { ease: 'power3.out' } });
        intro.from('.hero-copy > *', { y: 26, opacity: 0, duration: 0.65, stagger: 0.07 })
          .from('.hero-visual', { clipPath: 'inset(0 0 0 12%)', opacity: 0, duration: 0.9 }, '<0.1')
          .from('.hero-visual img', { scale: 1.08, duration: 1.1 }, '<');

        gsap.utils.toArray<HTMLElement>('[data-reveal]').forEach((section) => {
          gsap.fromTo(section,
            { y: 34, opacity: 0.15 },
            {
              y: 0,
              opacity: 1,
              duration: 0.55,
              ease: 'power3.out',
              onStart: () => section.classList.remove('is-revealed'),
              onComplete: () => section.classList.add('is-revealed'),
              scrollTrigger: {
                trigger: section,
                start: 'top 84%',
                toggleActions: 'restart none restart none'
              }
            }
          );
        });

        gsap.to('.hero-visual img', {
          yPercent: -5,
          ease: 'none',
          scrollTrigger: { trigger: '.hero', start: 'top top', end: 'bottom top', scrub: true }
        });
        requestAnimationFrame(() => ScrollTrigger.refresh());
      });
    });
  }
  ngOnDestroy() {
    this.destroyed = true;
    this.motionContext?.revert();
  }
  pad(n: number) { return n.toString().padStart(2, '0'); }
  onBillSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    this.selectedBillName.set(file ? file.name : 'Nenhum arquivo selecionado');
  }
  scrollProjects(direction: number) {
    this.projectRail?.nativeElement.scrollBy({ left: direction * 280, behavior: 'smooth' });
  }
  startProjectDrag(event: PointerEvent) {
    const rail = this.projectRail?.nativeElement;
    if (!rail) return;
    this.projectDrag = { startX: event.clientX, scrollLeft: rail.scrollLeft };
  }
  moveProjectDrag(event: PointerEvent) {
    const rail = this.projectRail?.nativeElement;
    if (!rail || !this.projectDrag) return;
    const delta = event.clientX - this.projectDrag.startX;
    if (Math.abs(delta) > 4) this.suppressProjectClick = true;
    rail.scrollLeft = this.projectDrag.scrollLeft - delta;
  }
  endProjectDrag(event: PointerEvent) {
    this.projectDrag = undefined;
    if (this.suppressProjectClick) setTimeout(() => this.suppressProjectClick = false);
  }
  playVideo(event: Event) {
    const video = event.target as HTMLVideoElement;
    video.muted = true;
    void video.play().catch(() => undefined);
  }
  openProject(index: number) { if (!this.suppressProjectClick) this.selectedProject.set(index); }
  closeProject() { this.selectedProject.set(null); }
  @HostListener('document:keydown', ['$event']) onProjectKeydown(event: KeyboardEvent) {
    const current = this.selectedProject();
    if (current === null) return;
    if (event.key === 'Escape') {
      event.preventDefault();
      this.closeProject();
    } else if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
      event.preventDefault();
      const projects = this.content().projects;
      const offset = event.key === 'ArrowRight' ? 1 : -1;
      this.selectedProject.set((current + offset + projects.length) % projects.length);
    }
  }
  @HostListener('window:scroll') onScroll() { this.isScrolled.set(window.scrollY > 24); }
  submitContact() {
    const billMessage = this.selectedBillName() !== 'Nenhum arquivo selecionado'
      ? `${this.contact.message || ''}\nConta de luz selecionada no formulário: ${this.selectedBillName()}`
      : this.contact.message;
    const payload = {
      ...this.contact,
      message: billMessage
    };
    this.leads.create(payload).subscribe({
      next: () => this.formMessage.set(`Obrigado, ${this.contact.name}. Em breve entraremos em contato.`),
      error: () => this.formMessage.set('Não foi possível enviar agora. Fale conosco pelo WhatsApp.')
    });
  }
}
