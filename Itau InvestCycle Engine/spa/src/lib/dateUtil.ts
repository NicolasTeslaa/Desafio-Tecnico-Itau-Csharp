export function fmtDateBR(value?: string) {
    if (!value) return "—";

    // Aceita: "2025-12-30", "2025-12-30T00:00:00", "2025-12-30T00:00:00Z"
    const isoDateOnly = value.length >= 10 ? value.slice(0, 10) : value;

    // Força interpretação como UTC pra não “voltar 1 dia” por timezone
    const d = new Date(`${isoDateOnly}T00:00:00Z`);
    if (Number.isNaN(d.getTime())) return value;

    return d.toLocaleDateString("pt-BR", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
    });
}