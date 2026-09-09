import { AfterViewInit, Component, HostListener, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonApp } from '@ionic/angular/ion-app';
import { LeadService } from './lead.service';
import { ContentService, HomeContent } from './content.service';
import { canAnimate } from './motion';

const FALLBACK: HomeContent = {
  heroTitle: 'Seu próximo passo para uma energia mais inteligente.',
  heroText: 'Projetamos sistemas solares com clareza, precisão e acompanhamento próximo, do primeiro cálculo à instalação.',
  heroImageUrl: 'assets/projects/01-solar.jpg',
  contactEmail: 'fabio@ecosologic.com.br',
  contactPhone: '+55 (21) 99542-4027',
  solutions: [
    { title: 'Residencial', text: 'Mais controle sobre a conta e mais liberdade para sua casa.' },
    { title: 'Comercial', text: 'Eficiência que protege a margem e valoriza seu negócio.' },
    { title: 'Industrial', text: 'Performance energética para operações que não podem parar.' },
    { title: 'Agronegócio', text: 'Energia confiável para produzir com visão de longo prazo.' }
  ],
  processSteps: [
    { title: 'Diagnóstico', text: 'Entendemos seu consumo, imóvel e objetivo.' },
    { title: 'Dimensionamento', text: 'Calculamos a solução adequada ao seu perfil.' },
    { title: 'Proposta clara', text: 'Você recebe números, prazos e condições sem letras miúdas.' },
    { title: 'Instalação', text: 'Equipe especializada acompanha tudo até a entrega.' }
  ],
  projects: [
    { title: 'Instalação residencial completa', category: 'Residencial · RJ', power: '5,5 kWp', imageUrl: 'assets/projects/02-solar.jpg', alt: 'Instalação solar residencial Ecosologic' },
    { title: 'Módulos solares instalados', category: 'Residencial · RJ', power: '4,0 kWp', imageUrl: 'assets/projects/03-solar.jpg', alt: 'Detalhe de módulos solares instalados' },
    { title: 'Usina em telhado residencial', category: 'Residencial · RJ', power: '7,0 kWp', imageUrl: 'assets/projects/04-solar.jpg', alt: 'Sistema fotovoltaico em telhado' }
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
  contact = { name: '', phone: '', message: '' };
  private readonly leads = inject(LeadService);
  private readonly contentService = inject(ContentService);
  private motionContext?: { revert: () => void };
  private destroyed = false;
  readonly content = signal<HomeContent>({ ...FALLBACK });
  ngOnInit() {
    this.contentService.getPublicHome().subscribe({
      next: content => this.content.set({
        ...FALLBACK,
        ...content,
        solutions: content.solutions?.length ? content.solutions : FALLBACK.solutions,
        processSteps: content.processSteps?.length ? content.processSteps : FALLBACK.processSteps,
        projects: content.projects?.length ? content.projects : FALLBACK.projects
      })
    });
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

        gsap.utils.toArray<HTMLElement>('.solution-list article, .process-steps article, .project-grid figure').forEach((item) => {
          gsap.from(item, {
            y: 30,
            opacity: 0,
            duration: 0.7,
            ease: 'power3.out',
            scrollTrigger: { trigger: item, start: 'top 86%', once: true }
          });
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
  @HostListener('window:scroll') onScroll() { this.isScrolled.set(window.scrollY > 24); }
  submitContact() {
    this.leads.create(this.contact).subscribe({
      next: () => this.formMessage.set(`Obrigado, ${this.contact.name}. Em breve entraremos em contato.`),
      error: () => this.formMessage.set('Não foi possível enviar agora. Fale conosco pelo WhatsApp.')
    });
  }
}
