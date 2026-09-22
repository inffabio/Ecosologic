import { AfterViewInit, Component, HostListener, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonApp } from '@ionic/angular/ion-app';
import { LeadService } from './lead.service';
import { ContentService, HomeContent } from './content.service';
import { canAnimate } from './motion';

const FALLBACK: HomeContent = {
  heroTitle: 'Energia solar distribuída para reduzir sua conta e proteger seu consumo futuro.',
  heroText: 'Venda consultiva, dimensionamento técnico e soluções híbridas com bateria de lítio para casas e empresas que querem economia, autonomia e previsibilidade.',
  heroImageUrl: 'assets/hero-solar-premium.png',
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
  readonly selectedBillName = signal('Nenhum arquivo selecionado');
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
  onBillSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    this.selectedBillName.set(file ? file.name : 'Nenhum arquivo selecionado');
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
