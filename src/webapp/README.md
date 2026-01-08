# WriteFluencyApp

This project was generated with [Angular CLI](https://github.com/angular/angular-cli) version 15.2.6.

## Screen Size Support

WriteFluency is optimized for **desktop computers only** (minimum width: 1300px). The application relies on keyboard shortcuts and requires a full desktop experience. An unsupported screen message displays automatically on mobile devices and tablets to inform users they need a desktop computer.

### How it works:
- The app detects screen width on load and resize events
- If width < 1300px, an unsupported screen overlay is shown
- When resized to a larger screen (≥1300px), the app content is displayed automatically
- The component is located at: `src/app/shared/unsupported-screen/`

## Development server

Run `ng serve` for a dev server. Navigate to `http://localhost:4200/`. The application will automatically reload if you change any of the source files.

## Code scaffolding

Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module`.

## Build

Run `ng build` to build the project. The build artifacts will be stored in the `dist/` directory.

## Running unit tests

Run `ng test` to execute the unit tests via [Karma](https://karma-runner.github.io).

## Running end-to-end tests

Run `ng e2e` to execute the end-to-end tests via a platform of your choice. To use this command, you need to first add a package that implements end-to-end testing capabilities.

## Further help

To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI Overview and Command Reference](https://angular.io/cli) page.

## Generate Api Client

```
npx openapi-generator-cli generate -i src/api/listen-and-write/openapi.json -g typescript-angular -o src/api/listen-and-write

```