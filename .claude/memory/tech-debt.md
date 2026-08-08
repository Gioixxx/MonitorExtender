---
type: tech-debt
tags: [memory, tech-debt]
updated: 2026-08-04
---

# Tech Debt
Registro debito tecnico con priorità. Aggiornato da /session-end. Origine spesso in [[conventions]].

## Template
### [Titolo breve]
- **Priorità:** Alta / Media / Bassa
- **Area:** [modulo/layer/feature]
- **Data:** [YYYY-MM-DD]
- **Descrizione:** [problema: duplicazione, workaround, ecc.]
- **Perché rimandato:** [motivo]
- **Impatto:** [rallenta sviluppo / rischio bug]
- **Risoluzione:** [piano suggerito]

---

### `ScreenCapturer` non si accorge se il monitor cambia a runtime
- **Priorità:** Media
- **Area:** `src/MonitorExtender.Server/ScreenCapturer.cs`
- **Data:** 2026-08-04
- **Descrizione:** `SourceWidth`/`SourceHeight` vengono letti una sola volta nel costruttore
  (`GetSystemMetrics`) e mai più aggiornati. Riprodotto: il server era partito con un monitor
  HDMI (HP E202, 1600×900) collegato al PC ma dal login precedente la risoluzione cache era
  rimasta a un fallback di 1024×768. Risultato: `CopyFromScreen` continuava a copiare solo
  l'angolo in alto a sinistra da 1024×768 del desktop reale — sul tablet si vedeva "mezzo
  schermo" (in realtà un ritaglio, non un letterbox). Confermato via `/info`
  (`source":"1024x768"` invece di `1600x900`) e risolto con un semplice riavvio del processo.
- **Perché rimandato:** il caso (monitor collegato/scollegato mentre il server già gira, o
  cambio di risoluzione desktop a runtime) non era mai stato considerato: finora il PC era
  sempre stato senza monitor. Ora che un monitor reale è collegato è un caso concreto, non solo
  teorico.
- **Impatto:** silenzioso — nessun errore in log, nessun crash, solo un frame croppato che
  sembra un bug di rendering lato Android finché non si controlla `/info`.
- **Risoluzione:** far ricontrollare a `ScreenCapturer`/`FrameBroker` la risoluzione a ogni
  giro (o almeno periodicamente) e ricreare `_full`/`_scaled` se cambia, invece di richiedere
  un riavvio manuale del processo.

## Priorità
- **Alta:** [item bloccanti]
- **Media:** `ScreenCapturer` non si accorge se il monitor cambia a runtime — vedi sopra.
- **Bassa:** [miglioramenti non urgenti]

## Archiviato
- [item risolti]
