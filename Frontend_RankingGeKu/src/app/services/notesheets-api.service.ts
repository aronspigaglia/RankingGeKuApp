import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { RankingRequestDto } from '../models/ranking-request';

@Injectable({ providedIn: 'root' })
export class NotesheetsApiService {
  // adjust if you use environments
  private baseUrl = 'http://127.0.0.1:5157';

  constructor(private http: HttpClient) {}

  /** Sends CSV as file to /api/notesheets/merged and returns the PDF (Blob). */
  uploadCsvAndGetMergedPdf(csvText: string, delimiter = ';') {
    const blob = new Blob([csvText], { type: 'text/csv' });
    const file = new File([blob], 'athleten.csv', { type: 'text/csv' });
    const form = new FormData();
    form.append('file', file);

    return this.http.post(
      `${this.baseUrl}/api/notesheets/merged?delimiter=${encodeURIComponent(delimiter)}`,
      form,
      { responseType: 'blob', observe: 'response' as const }
    );
  }
  generateRankingPdf(payload: RankingRequestDto) {
    return this.http.post(
      `${this.baseUrl}/api/ranking`,   // Endpoint implementieren wir später im Backend
      payload,
      { responseType: 'blob', observe: 'response' as const }
    );
  }
}
