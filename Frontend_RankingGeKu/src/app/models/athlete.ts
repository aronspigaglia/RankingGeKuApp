export interface PerDurchgangNotes {
  dNote?: string;   // e.g. "9.35"
  endNote?: string; // e.g. "9.10"
}

export interface Athlete {
  nachname: string;
  vorname: string;
  jg: string;
  verein: string;
  kat: string;

  // one entry per D1..D6 (index 0..5)
  notes: PerDurchgangNotes[];
}
