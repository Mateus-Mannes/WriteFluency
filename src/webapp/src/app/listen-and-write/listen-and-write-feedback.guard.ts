import { CanDeactivateFn } from '@angular/router';
import { ListenAndWriteComponent } from './listen-and-write.component';

export const listenAndWriteFeedbackGuard: CanDeactivateFn<ListenAndWriteComponent> = (
  component
) => component.canDeactivateFromRoute();
