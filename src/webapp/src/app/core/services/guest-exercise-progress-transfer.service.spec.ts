import { TestBed } from '@angular/core/testing';
import { GuestExerciseProgressTransferService } from './guest-exercise-progress-transfer.service';

describe('GuestExerciseProgressTransferService', () => {
  let service: GuestExerciseProgressTransferService;

  beforeEach(() => {
    window.sessionStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(GuestExerciseProgressTransferService);
  });

  it('should authorize a guest result only for a newly created account from the results CTA', () => {
    service.request(17);
    service.resolveSuccessfulLogin('results_save_cta', true, 'new-user');

    expect(service.consumeAuthorizedTransfer(17, 'new-user')).toBeTrue();
    expect(service.consumeAuthorizedTransfer(17, 'new-user')).toBeFalse();
  });

  it('should reject an existing-account login', () => {
    service.request(17);
    service.resolveSuccessfulLogin('results_save_cta', false, 'existing-user');

    expect(service.consumeAuthorizedTransfer(17, 'existing-user')).toBeFalse();
  });

  it('should preserve account creation across the email-confirmation step', () => {
    service.request(17);
    service.markAccountCreationStarted('results_save_cta');
    service.resolveSuccessfulLogin('results_save_cta', false, 'confirmed-user');

    expect(service.consumeAuthorizedTransfer(17, 'confirmed-user')).toBeTrue();
  });

  it('should reject a transfer for another user or exercise', () => {
    service.request(17);
    service.resolveSuccessfulLogin('results_save_cta', true, 'new-user');

    expect(service.consumeAuthorizedTransfer(18, 'new-user')).toBeFalse();
    expect(service.consumeAuthorizedTransfer(17, 'other-user')).toBeFalse();
  });

  it('should reject a new account created outside the results save flow', () => {
    service.request(17);
    service.resolveSuccessfulLogin('direct', true, 'new-user');

    expect(service.consumeAuthorizedTransfer(17, 'new-user')).toBeFalse();
  });
});
