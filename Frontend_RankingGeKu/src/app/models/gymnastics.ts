/** Geräte in fixer Reihenfolge; Index 0..5 entspricht D1..D6 für Gruppe 1. */
export const APPARATUS = ['Boden', 'Pferd', 'Ring', 'Sprung', 'Barren', 'Reck'];

/** Anzahl Geräte bzw. Durchgänge (D1..D6). */
export const NOTE_COUNT = APPARATUS.length;

/** Kategorie EPA (turnt nicht an Pferd und Ring). */
export function isEpa(kat: string | null | undefined): boolean {
  return (kat ?? '').trim().toUpperCase() === 'EPA';
}

/** Geräte, an denen EPA-Athleten nicht turnen. */
export function isEpaExcludedApparatus(apparatusName: string): boolean {
  return apparatusName === 'Pferd' || apparatusName === 'Ring';
}
