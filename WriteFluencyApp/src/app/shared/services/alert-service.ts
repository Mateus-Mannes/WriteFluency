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

  alert(message: string, type: string){
    const wrapper = document.createElement('div')
    wrapper.innerHTML = [
      `<div id='alertNotification' class="alert alert-${type} alert-dismissible" role="alert">`,
      `   <div>${message}</div>`,
      '   <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>',
      '</div>'
    ].join('');
    this.alertPlaceholder?.append(wrapper);
    setTimeout(this.disalert, 10000)
  }

  disalert(){
    var notification = document.getElementById('alertNotification');
    if(notification != null){
      notification.outerHTML = "";
    }
  }

  disalertAll(){
    document.querySelectorAll('#alertNotification').forEach(x => x.outerHTML = "");
  }
}