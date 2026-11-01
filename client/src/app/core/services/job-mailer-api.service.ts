import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface DraftEmailDto {
  subject: string;
  bodyHtml: string;
  resumeDocumentId: string;
  resumeFileName: string;
  isDuplicateWarning: boolean;
  duplicateWarningMessage?: string;
}

export interface ExtractedJobBannerDto {
  companyName: string;
  recruiterName?: string;
  recruiterEmail: string;
  selectedJobTitle: string;
  allDetectedRoles: string[];
  experienceRequired?: string;
  extractedSkills?: string;
  location?: string;
  suggestedSubject: string;
  suggestedBodyHtml: string;
  isDuplicateWarning: boolean;
  duplicateWarningMessage?: string;
  defaultResumeDocumentId: string;
  defaultResumeFileName: string;
}

export interface GenerateDraftRequest {
  companyName: string;
  recruiterName?: string;
  recruiterEmail: string;
  jobTitle: string;
  rawJobDescription: string;
}

export interface QueueApplicationRequest {
  companyName: string;
  recruiterName?: string;
  recruiterEmail: string;
  jobTitle: string;
  rawJobDescription: string;
  emailSubject: string;
  emailBodyHtml: string;
  resumeDocumentId?: string;
  scheduledSendUtc?: string;
  customResumePdf?: File;
}

export interface JobApplicationSummary {
  id: string;
  companyName: string;
  recruiterName?: string;
  recruiterEmail: string;
  jobTitle: string;
  emailSubject: string;
  status: number;
  statusText: string;
  resumeDocumentId: string;
  resumeFileName: string;
  retryCount: number;
  lastErrorMessage?: string;
  smtpMessageId?: string;
  createdAtUtc: string;
  scheduledSendUtc?: string;
  sentAtUtc?: string;
}

export interface ResumeDocument {
  id: string;
  versionName: string;
  displayFileName: string;
  storagePath: string;
  isDefault: boolean;
  createdAtUtc: string;
}

export interface CandidateProfile {
  id: string;
  fullName: string;
  title: string;
  yearsOfExperience: number;
  coreSkillsCsv: string;
  keyAchievements: string;
  linkedInUrl?: string;
  githubUrl?: string;
  portfolioUrl?: string;
  updatedAtUtc: string;
}

export interface UpdateCandidateProfileRequest {
  fullName: string;
  title: string;
  yearsOfExperience: number;
  coreSkillsCsv: string;
  keyAchievements: string;
  linkedInUrl?: string;
  githubUrl?: string;
  portfolioUrl?: string;
}

@Injectable({
  providedIn: 'root'
})
export class JobMailerApiService {
  private http = inject(HttpClient);
  private base = environment.apiUrl;

  extractFromImage(imageFile: File, resumeFile?: File, resumeDocumentId?: string): Observable<ExtractedJobBannerDto> {
    const formData = new FormData();
    formData.append('imageFile', imageFile);
    if (resumeFile) {
      formData.append('resumePdf', resumeFile);
    }
    if (resumeDocumentId) {
      formData.append('resumeDocumentId', resumeDocumentId);
    }
    return this.http.post<ExtractedJobBannerDto>(`${this.base}/applications/extract-from-image`, formData);
  }

  generateDraft(req: GenerateDraftRequest): Observable<DraftEmailDto> {
    return this.http.post<DraftEmailDto>(`${this.base}/applications/generate-draft`, req);
  }

  queueApplication(req: QueueApplicationRequest): Observable<{ id: string }> {
    const formData = new FormData();
    formData.append('companyName', req.companyName);
    if (req.recruiterName) formData.append('recruiterName', req.recruiterName);
    formData.append('recruiterEmail', req.recruiterEmail);
    formData.append('jobTitle', req.jobTitle);
    formData.append('rawJobDescription', req.rawJobDescription || '');
    formData.append('emailSubject', req.emailSubject);
    formData.append('emailBodyHtml', req.emailBodyHtml);
    if (req.resumeDocumentId) formData.append('resumeDocumentId', req.resumeDocumentId);
    if (req.scheduledSendUtc) formData.append('scheduledSendUtc', req.scheduledSendUtc);
    if (req.customResumePdf) formData.append('customResumePdf', req.customResumePdf);

    return this.http.post<{ id: string }>(`${this.base}/applications/queue`, formData);
  }

  getApplications(): Observable<JobApplicationSummary[]> {
    return this.http.get<JobApplicationSummary[]>(`${this.base}/applications`);
  }

  getResumes(): Observable<ResumeDocument[]> {
    return this.http.get<ResumeDocument[]>(`${this.base}/resumes`);
  }

  uploadResume(file: File, versionName: string, isDefault: boolean): Observable<ResumeDocument> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('versionName', versionName);
    formData.append('isDefault', String(isDefault));
    return this.http.post<ResumeDocument>(`${this.base}/resumes/upload`, formData);
  }

  getProfile(): Observable<CandidateProfile> {
    return this.http.get<CandidateProfile>(`${this.base}/profile`);
  }

  updateProfile(req: UpdateCandidateProfileRequest): Observable<CandidateProfile> {
    return this.http.put<CandidateProfile>(`${this.base}/profile`, req);
  }
}
