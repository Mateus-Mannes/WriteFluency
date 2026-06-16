import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { GoogleAdsConversionService } from './google-ads-conversion.service';

describe('GoogleAdsConversionService', () => {
  let service: GoogleAdsConversionService;
  let gtagSpy: jasmine.Spy;

  beforeEach(() => {
    gtagSpy = jasmine.createSpy('gtag');
    (globalThis as typeof globalThis & { gtag?: jasmine.Spy }).gtag = gtagSpy;

    TestBed.configureTestingModule({
      providers: [
        GoogleAdsConversionService,
        { provide: PLATFORM_ID, useValue: 'browser' },
      ],
    });

    service = TestBed.inject(GoogleAdsConversionService);
  });

  afterEach(() => {
    delete (globalThis as typeof globalThis & { gtag?: jasmine.Spy }).gtag;
  });

  it('should track signup conversion with normalized email user data', () => {
    service.trackSignup(' Test.User@gmail.com ');

    expect(gtagSpy).toHaveBeenCalledWith('set', 'user_data', {
      email: 'testuser@gmail.com',
    });
    expect(gtagSpy).toHaveBeenCalledWith('event', 'conversion', {
      send_to: 'AW-17978787910/XTDXCKykj8AcEMaQ-vxC',
      value: 1.0,
      currency: 'BRL',
    });
  });

  it('should not throw when the Google tag is unavailable', () => {
    delete (globalThis as typeof globalThis & { gtag?: jasmine.Spy }).gtag;

    expect(() => service.trackSignup('user@test.com')).not.toThrow();
  });
});
