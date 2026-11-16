export function parseTimestamp(value) {
  const text = String(value ?? '').trim();
  if (!text) return null;
  const parts = text.split(':');
  if (parts.length < 2 || parts.length > 3 || parts.some(part => !/^\d+$/.test(part))) return Number.NaN;
  const numbers = parts.map(Number);
  if (numbers.at(-1) > 59 || (numbers.length === 3 && numbers.at(-2) > 59)) return Number.NaN;
  return numbers.reduce((total, part) => total * 60 + part, 0);
}

export function formatTimestamp(totalSeconds) {
  if (!Number.isInteger(totalSeconds) || totalSeconds < 0) return '';
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const two = value => String(value).padStart(2, '0');
  return hours > 0 ? `${hours}:${two(minutes)}:${two(seconds)}` : `${minutes}:${two(seconds)}`;
}

export function formatLocator(citation) {
  if (citation.locatorText) return citation.locatorText;
  if (citation.pageStart) return citation.pageEnd && citation.pageEnd !== citation.pageStart
    ? `p. ${citation.pageStart}–${citation.pageEnd}` : `p. ${citation.pageStart}`;
  if (citation.timeStartSec != null) {
    const start = formatTimestamp(citation.timeStartSec);
    const end = citation.timeEndSec != null ? formatTimestamp(citation.timeEndSec) : '';
    return end ? `${start}–${end}` : start;
  }
  return 'Nota inteira';
}

export function formatBibliographyCitation(citation) {
  const hasLocator = Boolean(citation.locatorText || citation.pageStart || citation.timeStartSec != null);
  return [citation.authors?.join(', '), citation.title, citation.venue, citation.year, hasLocator ? formatLocator(citation) : null]
    .filter(value => value != null && value !== '').join('. ');
}
