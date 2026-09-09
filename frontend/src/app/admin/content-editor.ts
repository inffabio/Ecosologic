import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ContentService, HomeContent } from '../content.service';

@Component({
  selector: 'app-content-editor',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './content-editor.html',
  styleUrl: './content-editor.scss'
})
export class ContentEditor implements OnInit {
  private readonly contentService = inject(ContentService);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly message = signal('');
  readonly error = signal('');
  readonly uploading = signal(false);
  readonly uploadMessage = signal('');
  readonly uploadError = signal('');
  readonly uploadedUrl = signal('');
  content: HomeContent = { heroTitle: '', heroText: '', heroImageUrl: '', contactEmail: '', contactPhone: '', solutions: [], processSteps: [], projects: [] };
  selectedFile: File | null = null;
  mediaTarget: 'hero' | 'project' = 'hero';
  selectedProjectIndex = 0;

  ngOnInit() {
    this.contentService.getPublicHome().subscribe({
      next: content => { this.content = this.normalize(content); this.loading.set(false); },
      error: () => { this.error.set('Não foi possível carregar o conteúdo.'); this.loading.set(false); }
    });
  }

  addSolution() { if (this.content.solutions.length < 4) this.content.solutions.push({ title: '', text: '' }); }
  removeSolution(index: number) { this.content.solutions.splice(index, 1); }
  addStep() { if (this.content.processSteps.length < 4) this.content.processSteps.push({ title: '', text: '' }); }
  removeStep(index: number) { this.content.processSteps.splice(index, 1); }
  addProject() { if (this.content.projects.length < 4) this.content.projects.push({ title: '', category: '', power: '', imageUrl: '', alt: '' }); }
  removeProject(index: number) { this.content.projects.splice(index, 1); }

  save() {
    this.saving.set(true);
    this.message.set('');
    this.error.set('');
    this.contentService.updateHome(this.content).subscribe({
      next: content => { this.content = this.normalize(content); this.message.set('Conteúdo salvo.'); this.saving.set(false); },
      error: () => { this.error.set('Não foi possível salvar agora.'); this.saving.set(false); }
    });
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.uploadMessage.set('');
    this.uploadError.set('');
  }

  upload() {
    if (!this.selectedFile) {
      this.uploadError.set('Selecione uma imagem antes de enviar.');
      return;
    }
    this.uploading.set(true);
    this.uploadMessage.set('');
    this.uploadError.set('');
    this.uploadedUrl.set('');
    this.contentService.uploadMedia(this.selectedFile).subscribe({
      next: res => {
        this.uploadedUrl.set(res.url);
        this.uploadMessage.set('Imagem enviada.');
        this.uploading.set(false);
      },
      error: () => {
        this.uploadError.set('Não foi possível enviar a imagem.');
        this.uploading.set(false);
      }
    });
  }

  applyUploadedUrl() {
    const url = this.uploadedUrl();
    if (!url) return;
    if (this.mediaTarget === 'hero') {
      this.content.heroImageUrl = url;
    } else {
      const project = this.content.projects[this.selectedProjectIndex];
      if (project) project.imageUrl = url;
    }
    this.uploadMessage.set('URL aplicada.');
  }

  private normalize(content: HomeContent): HomeContent {
    return {
      ...content,
      solutions: content.solutions ? [...content.solutions] : [],
      processSteps: content.processSteps ? [...content.processSteps] : [],
      projects: content.projects ? [...content.projects] : []
    };
  }
}
