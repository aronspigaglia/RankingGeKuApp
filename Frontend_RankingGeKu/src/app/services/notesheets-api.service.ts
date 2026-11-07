import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RankingRequestDto } from '../models/ranking-request';

/** HTTP-Client für das lokale Backend (PDF-Erzeugung). */
@Injectable({ providedIn: 'root' })
export class NotesheetsApiService {
  private readonly baseUrl = 'http://127.0.0.1:5157';

  constructor(private http: HttpClient) {}

  /** Schickt die Athleten-CSV an /api/notesheets/merged und liefert die PDF als Blob. */
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

  /** Schickt die Noten an /api/ranking und liefert die Ranglisten-PDF als Blob. */
  generateRankingPdf(payload: RankingRequestDto) {
    return this.http.post(`${this.baseUrl}/api/ranking`, payload, {
      responseType: 'blob',
      observe: 'response' as const,
    });
  }
}
