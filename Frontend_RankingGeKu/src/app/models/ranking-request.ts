import { PerDurchgangNotes } from './athlete';

export interface RankingAthleteDto {
  nachname: string;
  vorname: string;
  jg: string;
  verein: string;
  kat: string;          // EPA, EP, P1, P2 ...
  groupIndex: number;   // Gruppe 1..n
  notes: PerDurchgangNotes[]; // Länge 6: D1..D6 (Boden..Reck)
}

export interface RankingRequestDto {
  competitionName?: string;
  apparatus: string[];        // ["Boden","Pferd","Ring","Sprung","Barren","Reck"]
  athletes: RankingAthleteDto[];
}
