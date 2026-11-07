/** Liest den Dateinamen aus einem Content-Disposition-Header, sonst Fallback. */
export function filenameFromContentDisposition(header: string | null, fallback: string): string {
  const match = /filename\*?=(?:UTF-8'')?["']?([^"';]+)["']?/i.exec(header ?? '');
  return match?.[1] ?? fallback;
}

/** Startet einen Browser-Download für den Blob unter dem angegebenen Dateinamen. */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
