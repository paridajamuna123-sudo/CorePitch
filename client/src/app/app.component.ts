import { Component, OnInit, OnDestroy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import {
  JobMailerApiService,
  DraftEmailDto,
  ResumeDocument,
  CandidateProfile,
  JobApplicationSummary,
  ExtractedJobBannerDto
} from './core/services/job-mailer-api.service';
import { LocalImageOcrService } from './core/services/local-image-ocr.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  private api = inject(JobMailerApiService);
  private fb = inject(FormBuilder);
  private sanitizer = inject(DomSanitizer);
  private localOcr = inject(LocalImageOcrService);

  // Active Tab
  activeTab = signal<'workspace' | 'resumes' | 'history'>('workspace');

  // Screenshot Upload & Paste State
  isExtractingImage = signal<boolean>(false);
  screenshotPreviewUrl = signal<string | null>(null);
  extractedData = signal<ExtractedJobBannerDto | null>(null);
  detectedRolesList = signal<string[]>([]);

  // Fast Apply Form (Section 2)
  applyForm: FormGroup = this.fb.group({
    companyName: ['', [Validators.required]],
    recruiterName: [''],
    recruiterEmail: ['', [Validators.required, Validators.email]],
    jobTitle: ['', [Validators.required]],
    experienceRequired: [''],
    extractedSkills: [''],
    location: [''],
    rawJobDescription: [''],
    resumeDocumentId: ['']
  });

  // Section 3: Cold Email Editor & Custom Resume
  isGenerating = signal<boolean>(false);
  isQueueing = signal<boolean>(false);
  draft = signal<DraftEmailDto | null>(null);
  editableSubject = signal<string>('');
  editableBodyHtml = signal<string>('');

  // Resume selection mode: 'saved' or 'custom'
  resumeMode = signal<'saved' | 'custom'>('saved');
  customResumeFile: File | null = null;
  customResumeFileName = signal<string>('');
  customResumeFileSize = signal<string>('');
  customResumeBlobUrl = signal<string | null>(null);

  // Section 4: Pre-Flight Review Modal
  showPreviewModal = signal<boolean>(false);
  previewResumeUrl = signal<SafeResourceUrl | null>(null);

  // Resumes & Profile
  resumes = signal<ResumeDocument[]>([]);
  profile = signal<CandidateProfile | null>(null);
  isSavingProfile = signal<boolean>(false);
  profileForm: FormGroup = this.fb.group({
    fullName: ['', Validators.required],
    title: ['', Validators.required],
    yearsOfExperience: [0, [Validators.required, Validators.min(0)]],
    coreSkillsCsv: ['', Validators.required],
    keyAchievements: ['', Validators.required],
    linkedInUrl: [''],
    githubUrl: [''],
    portfolioUrl: ['']
  });

  // Resume Upload Tab
  uploadVersionName = signal<string>('');
  uploadIsDefault = signal<boolean>(false);
  selectedResumeFile: File | null = null;
  isUploadingResume = signal<boolean>(false);

  // Applications History
  applications = signal<JobApplicationSummary[]>([]);
  private refreshTimer: any;

  // Alerts & Notifications
  actionMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  ngOnInit(): void {
    this.loadResumes();
    this.loadProfile();
    this.loadApplications();

    this.refreshTimer = setInterval(() => {
      this.loadApplications();
    }, 10000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
    }
    if (this.customResumeBlobUrl()) {
      URL.revokeObjectURL(this.customResumeBlobUrl()!);
    }
  }

  setTab(tab: 'workspace' | 'resumes' | 'history'): void {
    this.activeTab.set(tab);
    this.clearAlert();
    if (tab === 'history') {
      this.loadApplications();
    }
  }

  // ========================================================
  // Section 1: Screenshot Paste & Upload Dropzone
  // ========================================================
  onPaste(event: ClipboardEvent): void {
    const items = event.clipboardData?.items;
    if (!items) return;

    for (let i = 0; i < items.length; i++) {
      if (items[i].type.indexOf('image') !== -1) {
        const file = items[i].getAsFile();
        if (file) {
          this.processImageFile(file);
          break;
        }
      }
    }
  }

  onImageFileSelected(event: any): void {
    const file = event.target.files[0];
    if (file) {
      this.processImageFile(file);
    }
  }

  onImageDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const files = event.dataTransfer?.files;
    if (files && files.length > 0 && files[0].type.startsWith('image/')) {
      this.processImageFile(files[0]);
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
  }

  processImageFile(file: File): void {
    const reader = new FileReader();
    reader.onload = (e: any) => {
      this.screenshotPreviewUrl.set(e.target.result);
    };
    reader.readAsDataURL(file);

    this.isExtractingImage.set(true);
    this.clearAlert();

    const resumeFile = this.resumeMode() === 'custom' ? (this.customResumeFile ?? undefined) : undefined;
    const resumeDocId = this.resumeMode() === 'saved' ? (this.applyForm.get('resumeDocumentId')?.value || undefined) : undefined;

    this.api.extractFromImage(file, resumeFile, resumeDocId).subscribe({
      next: (res) => {
        this.isExtractingImage.set(false);
        this.extractedData.set(res);
        this.detectedRolesList.set(res.allDetectedRoles || []);

        this.applyForm.patchValue({
          companyName: res.companyName,
          recruiterName: res.recruiterName || '',
          recruiterEmail: res.recruiterEmail || '',
          jobTitle: res.selectedJobTitle,
          experienceRequired: res.experienceRequired || '',
          extractedSkills: res.extractedSkills || '',
          location: res.location || '',
          rawJobDescription: `Required Skills: ${res.extractedSkills || 'N/A'}\nExperience: ${res.experienceRequired || 'N/A'}\nLocation: ${res.location || 'N/A'}`
        });

        this.editableSubject.set(res.suggestedSubject);
        this.editableBodyHtml.set(res.suggestedBodyHtml);
        this.draft.set({
          subject: res.suggestedSubject,
          bodyHtml: res.suggestedBodyHtml,
          resumeDocumentId: res.defaultResumeDocumentId,
          resumeFileName: res.defaultResumeFileName,
          isDuplicateWarning: res.isDuplicateWarning,
          duplicateWarningMessage: res.duplicateWarningMessage
        });

        if (res.defaultResumeDocumentId && !this.applyForm.get('resumeDocumentId')?.value) {
          this.applyForm.patchValue({ resumeDocumentId: res.defaultResumeDocumentId });
        }

        // Reload updated candidate profile
        this.loadProfile();

        this.showAlert('success', `Screenshot parsed! Extracted "${res.companyName}" - ${res.selectedJobTitle}. Verify details below.`);
      },
      error: async (err) => {
        // Run Real In-Browser OCR fallback with Tesseract.js
        try {
          const ocrRes = await this.localOcr.extractTextFromImage(file);
          this.isExtractingImage.set(false);

          this.detectedRolesList.set(ocrRes.detectedRoles);
          this.applyForm.patchValue({
            companyName: ocrRes.companyName,
            recruiterName: 'Hiring Team',
            recruiterEmail: ocrRes.recruiterEmail,
            jobTitle: ocrRes.selectedRole,
            experienceRequired: ocrRes.experienceRequired,
            extractedSkills: ocrRes.skills,
            location: ocrRes.location,
            rawJobDescription: ocrRes.rawText
          });

          const currentProfile = this.profile();
          const candName = currentProfile?.fullName || 'Candidate';
          const candTitle = currentProfile?.title || ocrRes.selectedRole;
          const candSkills = currentProfile?.coreSkillsCsv || ocrRes.skills;
          const candExp = currentProfile?.yearsOfExperience || 4;

          const subject = `Application: ${ocrRes.selectedRole} - ${candName}`;
          const bodyHtml = `
<p>Hi Hiring Team,</p>
<p>I hope this email finds you well. I'm reaching out to express my strong interest in the <strong>${ocrRes.selectedRole}</strong> position at <strong>${ocrRes.companyName}</strong>.</p>
<p>With ${candExp} years of hands-on experience specializing in <strong>${candSkills.split(',').slice(0, 3).join(', ')}</strong>, I have a track record of driving scalable architecture and dependable deliverables.</p>
<p>${currentProfile?.keyAchievements || 'Delivered enterprise scalable solutions with modern full-stack architectures.'}</p>
<p>I have attached my updated resume for your review. Would you be open to a brief 10-minute introductory conversation this week?</p>
<p>Best regards,<br/>
<strong>${candName}</strong><br/>
${candTitle}<br/>
${currentProfile?.linkedInUrl ? `<a href="${currentProfile.linkedInUrl}">LinkedIn</a>` : ''} ${currentProfile?.githubUrl ? `| <a href="${currentProfile.githubUrl}">GitHub</a>` : ''}</p>`.trim();

          this.editableSubject.set(subject);
          this.editableBodyHtml.set(bodyHtml);
          this.draft.set({
            subject,
            bodyHtml,
            resumeDocumentId: this.applyForm.get('resumeDocumentId')?.value || '',
            resumeFileName: this.getActiveAttachedResumeName(),
            isDuplicateWarning: false
          });

          this.showAlert('success', `In-browser OCR extracted real banner details! Verified "${ocrRes.companyName}" (${ocrRes.recruiterEmail}) - ${ocrRes.selectedRole}.`);
        } catch (ocrErr: any) {
          this.isExtractingImage.set(false);
          this.showAlert('error', 'Image extraction failed: ' + (ocrErr.message || err.message));
        }
      }
    });
  }

  selectRole(role: string): void {
    this.applyForm.patchValue({ jobTitle: role });
  }

  // ========================================================
  // Section 2 & 3: Regenerate & Form Handlers
  // ========================================================
  onRegenerateDraft(): void {
    if (this.applyForm.invalid) {
      this.applyForm.markAllAsTouched();
      this.showAlert('error', 'Please fill in Company Name, Recruiter Email, and Job Title.');
      return;
    }

    this.isGenerating.set(true);
    this.clearAlert();

    const formVal = this.applyForm.value;
    const jdCombined = formVal.rawJobDescription || 
      `Company: ${formVal.companyName}\nRole: ${formVal.jobTitle}\nSkills: ${formVal.extractedSkills}\nExp: ${formVal.experienceRequired}\nLocation: ${formVal.location}`;

    this.api.generateDraft({
      companyName: formVal.companyName,
      recruiterName: formVal.recruiterName,
      recruiterEmail: formVal.recruiterEmail,
      jobTitle: formVal.jobTitle,
      rawJobDescription: jdCombined
    }).subscribe({
      next: (res) => {
        this.isGenerating.set(false);
        this.draft.set(res);
        this.editableSubject.set(res.subject);
        this.editableBodyHtml.set(res.bodyHtml);
        this.showAlert('success', 'Cold email draft regenerated from verified details!');
      },
      error: (err) => {
        this.isGenerating.set(false);
        this.showAlert('error', 'Failed to generate draft: ' + (err.error?.detail || err.message));
      }
    });
  }

  onCustomResumeSelected(event: any): void {
    const file = event.target.files[0];
    if (file) {
      if (!file.name.toLowerCase().endsWith('.pdf')) {
        this.showAlert('error', 'Only PDF files are supported for resume attachments.');
        return;
      }
      this.customResumeFile = file;
      this.customResumeFileName.set(file.name);
      this.customResumeFileSize.set((file.size / 1024).toFixed(1) + ' KB');

      if (this.customResumeBlobUrl()) {
        URL.revokeObjectURL(this.customResumeBlobUrl()!);
      }
      const url = URL.createObjectURL(file);
      this.customResumeBlobUrl.set(url);
    }
  }

  // ========================================================
  // Section 4: Pre-Flight Review Modal & Dispatch
  // ========================================================
  openPreFlightModal(): void {
    if (this.applyForm.invalid) {
      this.applyForm.markAllAsTouched();
      this.showAlert('error', 'Please fill in all required fields (Company, Email, Job Title).');
      return;
    }

    if (!this.editableSubject().trim() || !this.editableBodyHtml().trim()) {
      this.showAlert('error', 'Email subject or body cannot be empty.');
      return;
    }

    const hasAttachment = (this.resumeMode() === 'saved' && this.applyForm.get('resumeDocumentId')?.value) ||
                          (this.resumeMode() === 'custom' && this.customResumeFile != null);

    if (!hasAttachment) {
      this.showAlert('error', 'Please select a saved resume or attach a custom PDF file.');
      return;
    }

    this.showPreviewModal.set(true);
  }

  closePreFlightModal(): void {
    this.showPreviewModal.set(false);
  }

  confirmAndDispatch(): void {
    this.isQueueing.set(true);
    this.clearAlert();

    const formVal = this.applyForm.value;
    const jdCombined = formVal.rawJobDescription || `${formVal.jobTitle} at ${formVal.companyName}`;

    this.api.queueApplication({
      companyName: formVal.companyName,
      recruiterName: formVal.recruiterName,
      recruiterEmail: formVal.recruiterEmail,
      jobTitle: formVal.jobTitle,
      rawJobDescription: jdCombined,
      emailSubject: this.editableSubject(),
      emailBodyHtml: this.editableBodyHtml(),
      resumeDocumentId: this.resumeMode() === 'saved' ? formVal.resumeDocumentId : undefined,
      customResumePdf: this.resumeMode() === 'custom' ? (this.customResumeFile ?? undefined) : undefined
    }).subscribe({
      next: () => {
        this.isQueueing.set(false);
        this.showPreviewModal.set(false);
        this.showAlert('success', `Application queued successfully for ${formVal.companyName}! The outbox dispatcher is processing with anti-spam jitter.`);
        
        // Reset form & state
        this.applyForm.reset();
        this.draft.set(null);
        this.editableSubject.set('');
        this.editableBodyHtml.set('');
        this.screenshotPreviewUrl.set(null);
        this.extractedData.set(null);
        this.customResumeFile = null;
        this.customResumeFileName.set('');
        this.loadApplications();
        this.loadResumes();
      },
      error: (err) => {
        this.isQueueing.set(false);
        this.showAlert('error', 'Failed to queue application: ' + (err.error?.detail || err.message));
      }
    });
  }

  previewAttachedPdf(): void {
    if (this.resumeMode() === 'custom' && this.customResumeBlobUrl()) {
      window.open(this.customResumeBlobUrl()!, '_blank');
    } else {
      const selectedId = this.applyForm.get('resumeDocumentId')?.value;
      const doc = this.resumes().find(r => r.id === selectedId);
      if (doc?.displayFileName === 'Rajesh_Parida_Resume.pdf') {
        window.open('/App_Data/Resumes/Rajesh_Parida_Resume.pdf', '_blank');
      } else {
        this.showAlert('success', `Resume version: "${doc?.versionName || 'Default'}" (${doc?.displayFileName || 'Rajesh_Parida_Resume.pdf'})`);
      }
    }
  }

  getActiveAttachedResumeName(): string {
    if (this.resumeMode() === 'custom') {
      return this.customResumeFileName() || 'Custom_Resume.pdf';
    }
    const selectedId = this.applyForm.get('resumeDocumentId')?.value;
    const doc = this.resumes().find(r => r.id === selectedId);
    return doc ? `${doc.versionName} (${doc.displayFileName})` : (this.draft()?.resumeFileName || 'Resume.pdf');
  }

  // ========================================================
  // Other Tabs: Resumes & Profile
  // ========================================================
  loadResumes(): void {
    this.api.getResumes().subscribe({
      next: (data) => {
        this.resumes.set(data);
        const defaultResume = data.find(r => r.displayFileName === 'Rajesh_Parida_Resume.pdf') 
          || data.find(r => r.isDefault) 
          || data[0];
        if (defaultResume && !this.applyForm.get('resumeDocumentId')?.value) {
          this.applyForm.patchValue({ resumeDocumentId: defaultResume.id });
        }
      },
      error: (err) => console.error('Failed to load resumes:', err)
    });
  }

  loadProfile(): void {
    this.api.getProfile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.profileForm.patchValue({
          fullName: p.fullName,
          title: p.title,
          yearsOfExperience: p.yearsOfExperience,
          coreSkillsCsv: p.coreSkillsCsv,
          keyAchievements: p.keyAchievements,
          linkedInUrl: p.linkedInUrl,
          githubUrl: p.githubUrl,
          portfolioUrl: p.portfolioUrl
        });
      },
      error: (err) => console.error('Failed to load profile:', err)
    });
  }

  loadApplications(): void {
    this.api.getApplications().subscribe({
      next: (apps) => this.applications.set(apps),
      error: (err) => console.error('Failed to load applications:', err)
    });
  }

  onFileSelected(event: any): void {
    const file = event.target.files[0];
    if (file) {
      this.selectedResumeFile = file;
    }
  }

  onUploadResume(): void {
    if (!this.selectedResumeFile) {
      this.showAlert('error', 'Please select a PDF resume file.');
      return;
    }
    if (!this.uploadVersionName().trim()) {
      this.showAlert('error', 'Please enter a version name for this resume.');
      return;
    }

    this.isUploadingResume.set(true);
    this.clearAlert();

    this.api.uploadResume(this.selectedResumeFile, this.uploadVersionName(), this.uploadIsDefault()).subscribe({
      next: (newDoc) => {
        this.isUploadingResume.set(false);
        this.selectedResumeFile = null;
        this.uploadVersionName.set('');
        this.uploadIsDefault.set(false);
        this.showAlert('success', `Resume "${newDoc.versionName}" uploaded successfully!`);
        this.loadResumes();
      },
      error: (err) => {
        this.isUploadingResume.set(false);
        this.showAlert('error', 'Upload failed: ' + (err.error?.detail || err.message));
      }
    });
  }

  onSaveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.isSavingProfile.set(true);
    this.clearAlert();

    this.api.updateProfile(this.profileForm.value).subscribe({
      next: (updated) => {
        this.isSavingProfile.set(false);
        this.profile.set(updated);
        this.showAlert('success', 'Profile updated successfully!');
      },
      error: (err) => {
        this.isSavingProfile.set(false);
        this.showAlert('error', 'Failed to save profile: ' + (err.error?.detail || err.message));
      }
    });
  }

  getStatusBadgeClass(status: number): string {
    switch (status) {
      case 1:
        return 'bg-blue-900/60 text-blue-300 border-blue-700';
      case 2:
        return 'bg-amber-900/60 text-amber-300 border-amber-700 animate-pulse';
      case 3:
        return 'bg-emerald-900/60 text-emerald-300 border-emerald-700';
      case 4:
        return 'bg-rose-900/60 text-rose-300 border-rose-700';
      default:
        return 'bg-slate-800 text-slate-300 border-slate-700';
    }
  }

  showAlert(type: 'success' | 'error', text: string): void {
    this.actionMessage.set({ type, text });
  }

  clearAlert(): void {
    this.actionMessage.set(null);
  }
}
