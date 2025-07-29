import { HttpErrorResponse } from "@angular/common/http";
import { Injectable, ViewChild } from "@angular/core";


@Injectable()
export class AlertService  {

  notifyErros(error: HttpErrorResponse){
    if(Array.isArray(error.error)){
      error.error.forEach(x => this.alert(x, 'danger'));
    } else {
      var msg = String(error.error);
      this.alert(msg, 'danger');
    }
  }

  alertPlaceholder = document.getElementById('liveAlertPlaceholder')

  alert(message: string, type: string, timeout: number = 10000){
    const wrapper = document.createElement('div')
    wrapper.innerHTML = [
      `<div id='alertNotification' class="alert alert-${type} alert-dismissible fadeIn" role="alert">`,
      `   <div>${message}</div>`,
      '   <button type="button" class="btn-close" aria-label="Close"></button>',
      '</div>',
    ].join('');
    this.alertPlaceholder?.append(wrapper);
    
    const timeoutId = setTimeout(() => {
      this.disalert(wrapper.childNodes[0] as HTMLElement);
    }, timeout);

    wrapper.querySelector('.btn-close')!
      .addEventListener('click', () => {
        clearTimeout(timeoutId);
        this.disalert(wrapper.childNodes[0] as HTMLElement);
      });
  }

  disalert(notification: HTMLElement){
    notification.className = notification.className.replace("fadeIn", "fadeOut");
    setTimeout(() => { notification!.outerHTML = ""; }, 500); 
  }

  disalertAll(){
    document.querySelectorAll('#alertNotification').forEach(x => x.outerHTML = "");
  }
}