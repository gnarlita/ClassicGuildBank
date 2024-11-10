import { Component, OnInit, Output, EventEmitter, ViewChild, Input } from '@angular/core';
import { UserStore } from './user.store';
import { FormBuilder, FormGroup, Validators, FormControl } from '@angular/forms';

@Component({
  selector: 'cgb-reset-password',
  templateUrl: './reset-password.component.html'
})
export class ResetPasswordComponent implements OnInit {

  @Input() showModal: boolean;
  @Output() closeRequested: EventEmitter<any> = new EventEmitter();
  
  public resetForm: FormGroup;
  public errorText: string;
  public successText: string;
  public formSubmitted: boolean = false;

  constructor(
    private userStore: UserStore,       
    private formBuilder: FormBuilder
  ) { }

  ngOnInit() {
    this.resetForm = this.formBuilder.group({
      username: new FormControl('', [Validators.required]),
      email: new FormControl('', [Validators.required, Validators.email])
    });
  }

  public get f() {
    return this.resetForm.controls;
  }

  public close() {
    this.closeRequested.emit(null);
  }

  public submit() {
    this.errorText = undefined;
    this.successText = undefined;

    if (!this.resetForm.valid) {
      // Mark each form control as dirty to trigger validation messages
      Object.keys(this.resetForm.controls).forEach(controlName => {
        this.resetForm.controls[controlName].markAsDirty();
      });
      return;
    }

    this.userStore.sendResetPasswordEmail(this.resetForm.value).subscribe({
      next: () => {
        this.successText = "An email has been sent to the address provided. Follow the instructions to reset your password";
        this.formSubmitted = true;
      },
      error: (response) => {
        console.error(response.error);
        this.errorText = response.error.errorMessage;
      }
    });
  }
}
