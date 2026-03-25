import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { AuthApiService } from '../services/auth-api.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss'],
})
export class RegisterComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authApiService = inject(AuthApiService);

  readonly isBusy = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly registerForm = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]],
  });

  async submit(): Promise<void> {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    const { email, password, confirmPassword } = this.registerForm.getRawValue();

    if (password !== confirmPassword) {
      this.errorMessage.set('Password and confirmation must match.');
      return;
    }

    this.isBusy.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      await firstValueFrom(this.authApiService.register(email, password));
      this.successMessage.set('Registration successful. Please check your email to confirm your account.');
      this.registerForm.reset();
    } catch {
      this.errorMessage.set('Registration failed. Try a stronger password or a different email.');
    } finally {
      this.isBusy.set(false);
    }
  }
}
