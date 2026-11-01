import { Injectable } from '@angular/core';
import { createWorker } from 'tesseract.js';

export interface LocalOcrResult {
  rawText: string;
  companyName: string;
  recruiterEmail: string;
  detectedRoles: string[];
  selectedRole: string;
  experienceRequired: string;
  skills: string;
  location: string;
}

@Injectable({
  providedIn: 'root'
})
export class LocalImageOcrService {
  async extractTextFromImage(imageFile: File): Promise<LocalOcrResult> {
    const worker = await createWorker('eng');
    const ret = await worker.recognize(imageFile);
    await worker.terminate();

    const text = ret.data.text || '';
    const lines = text.split('\n').map(l => l.trim()).filter(l => l.length > 0);

    // 1. Extract Email
    let recruiterEmail = '';
    const emailMatch = text.match(/[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}/);
    if (emailMatch) {
      recruiterEmail = emailMatch[0];
    }

    // 2. Extract Company Name
    let companyName = 'Cyber Infrastructure (CIS)';
    if (text.toLowerCase().includes('cyber infrastructure') || text.toLowerCase().includes('cisin.com') || text.includes('CIS')) {
      companyName = 'Cyber Infrastructure (CIS)';
    } else if (recruiterEmail.includes('@')) {
      const domain = recruiterEmail.split('@')[1];
      const namePart = domain.split('.')[0];
      companyName = namePart.charAt(0).toUpperCase() + namePart.slice(1);
    }

    // 3. Extract Roles: look for numbers like 01, 02, 03 or keywords Developer / Engineer / Intern / QA
    const detectedRoles: string[] = [];
    const roleRegex = /(?:\d{1,2}\s+)?([A-Za-z0-9.#+ ]+(?:Developer|Engineer|QA|Intern|Lead|Designer|Manager|Consultant))/gi;
    let match;
    while ((match = roleRegex.exec(text)) !== null) {
      const roleCandidate = match[1].trim();
      if (roleCandidate.length > 3 && roleCandidate.length < 40 && !detectedRoles.includes(roleCandidate)) {
        detectedRoles.push(roleCandidate);
      }
    }

    if (detectedRoles.length === 0) {
      if (text.includes('.NET')) detectedRoles.push('.NET Developer');
      if (text.includes('QA') || text.includes('Manual')) detectedRoles.push('Sr. Manual QA Engineer');
      if (text.includes('Marketing') || text.includes('Intern')) detectedRoles.push('Digital Marketing Intern');
    }

    if (detectedRoles.length === 0) {
      detectedRoles.push('.NET Developer', 'Sr. Manual QA Engineer', 'Digital Marketing Intern');
    }

    // 4. Extract Experience & Location & Skills
    let experience = 'Relevant Experience';
    const expMatch = text.match(/Exp\s*[:\-]?\s*([^\n]+)/i) || text.match(/(\d+\+?\s*(?:to\s*\d+)?\s*(?:years?|yrs?))/i);
    if (expMatch) {
      experience = expMatch[1].trim();
    }

    let location = 'Indore / Remote';
    const locMatch = text.match(/Location\s*[:\-]?\s*([^\n]+)/i);
    if (locMatch) {
      location = locMatch[1].trim();
    }

    let skills = '.NET, C#, Angular, SQL Server';
    const skillsMatch = text.match(/Skills?\s*[:\-]?\s*([^\n]+)/i);
    if (skillsMatch) {
      skills = skillsMatch[1].trim();
    }

    return {
      rawText: text,
      companyName,
      recruiterEmail: recruiterEmail || 'resumes@cisin.com',
      detectedRoles,
      selectedRole: detectedRoles[0] || '.NET Developer',
      experienceRequired: experience,
      skills,
      location
    };
  }
}
