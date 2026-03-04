export const BRAZIL_TIME_ZONE = "America/Sao_Paulo";

function hasTimeZoneOffset(value: string): boolean {
  return /(?:Z|[+\-]\d{2}:?\d{2})$/i.test(value);
}

function normalizeUtcDateTime(value: string): string {
  if (!value.includes("T")) return value;
  return hasTimeZoneOffset(value) ? value : `${value}Z`;
}

export function fmtDateBR(value?: string | null): string {
  if (!value) return "-";

  const datePortion = value.slice(0, 10);
  const match = datePortion.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (match) {
    const [, year, month, day] = match;
    return `${day}/${month}/${year}`;
  }

  const parsed = new Date(normalizeUtcDateTime(value));
  if (Number.isNaN(parsed.getTime())) return value;

  return new Intl.DateTimeFormat("pt-BR", {
    timeZone: BRAZIL_TIME_ZONE,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(parsed);
}

export function fmtDateTimeBR(value?: string | null): string {
  if (!value) return "-";

  const parsed = new Date(normalizeUtcDateTime(value));
  if (Number.isNaN(parsed.getTime())) return value;

  return new Intl.DateTimeFormat("pt-BR", {
    timeZone: BRAZIL_TIME_ZONE,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  }).format(parsed);
}
