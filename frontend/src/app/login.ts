import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login {
  email = '';
  password = '';
  readonly error = signal('');
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  submit() {
    this.error.set('');
    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigate(['/admin/crm']),
      error: () => this.error.set('E-mail ou senha inválidos.')
    });
  }
}
