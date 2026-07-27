---
type: conventions
tags: [memory, conventions]
updated: 2026-07-27
---

# Convenzioni Locali
Pattern e regole specifiche del progetto. Workaround che generano debito → [[tech-debt]].

## Template
### [Nome convenzione]
- **Contesto:** [quando si applica]
- **Regola:** [descrizione precisa]
- **Esempio:** [codice/pattern]
- **Perché diverge:** [motivazione]

---

### Niente Clean Architecture a 4 layer
- **Contesto:** `src/MonitorExtender.Server/`.
- **Regola:** un solo progetto console, un file per responsabilità
  (`ScreenCapturer`, `JpegEncoder`, `FrameBroker`, `MjpegServer`, `StreamOptions`).
  Nessun Domain/Application/Infrastructure/API, nessun MediatR, nessun EF Core.
- **Perché diverge:** i moduli caricati (`arch/clean-arch.md`, `stacks/dotnet.md`) descrivono
  un'applicazione gestionale multi-tenant con DB e CQRS. Qui non c'è dominio, non c'è
  persistenza e non ci sono use case: sono ~400 righe che spostano pixel. Applicare i 4 layer
  produrrebbe solo cartelle vuote. Valgono comunque le regole trasversali: niente logica nel
  punto d'ingresso HTTP, una responsabilità per classe, iterazione a piccoli passi verificabili.

### Le regole di `arch/api-design.md` valgono solo in parte
- **Contesto:** endpoint del server MJPEG.
- **Regola:** le rotte sono `/`, `/stream`, `/snapshot` — nessun `/api/v1/`, nessun plurale,
  nessuna paginazione, nessun error body JSON.
- **Perché diverge:** non è una REST API su risorse ma un trasporto per uno stream continuo.
  Restano validi status code semantici (200/404) e il divieto di esporre dettagli interni.

### Verifica end-to-end prima di allargare
- **Contesto:** tutto il progetto.
- **Regola:** ogni fase si chiude con una misura reale, non con "compila". Fase 1 → frame JPEG
  salvato e guardato; Fase 2 → fps/banda/integrità misurati su stream vero
  (`tools/test-stream.ps1`) e test dal browser del telefono.
- **Perché diverge:** non diverge, è l'applicazione stretta di `workflows/iterative-dev.md` a
  un progetto dove il "test unitario" direbbe poco: quello che conta è il flusso di byte.
