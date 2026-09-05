import { TestBed } from '@angular/core/testing';
import { GoogleIdentityService } from './google-identity.service';

describe('GoogleIdentityService', () => {
  let service: GoogleIdentityService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [GoogleIdentityService] });
    service = TestBed.inject(GoogleIdentityService);
  });

  afterEach(() => {
    delete (window as Window & { google?: unknown }).google;
  });

  it('initializes the Google SDK once while rendering buttons for multiple auth screens', async () => {
    const initialize = jasmine.createSpy('initialize');
    const renderButton = jasmine.createSpy('renderButton');
    const googleApi = {
      accounts: {
        id: { initialize, renderButton }
      }
    };
    Object.defineProperty(window, 'google', { configurable: true, value: googleApi });

    const loginButton = document.createElement('div');
    const registerButton = document.createElement('div');
    const loginCallback = jasmine.createSpy('loginCallback');
    const registerCallback = jasmine.createSpy('registerCallback');

    await service.renderButton(loginButton, 'signin_with', loginCallback);
    await service.renderButton(registerButton, 'signup_with', registerCallback);

    expect(initialize).toHaveBeenCalledTimes(1);
    expect(renderButton).toHaveBeenCalledTimes(2);
    expect(renderButton).toHaveBeenCalledWith(loginButton, jasmine.objectContaining({ text: 'signin_with' }));
    expect(renderButton).toHaveBeenCalledWith(registerButton, jasmine.objectContaining({ text: 'signup_with' }));
  });
});
