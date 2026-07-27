---
type: memory
tags: [memory, index]
updated: 2026-07-27
---

# MonitorExtender — .NET 8 (server) + Android/Kotlin (viewer)
> Contesto persistente. Aggiornato da /remember. Vault Obsidian: vedi workflows/obsidian-vault.md.

**Stack:** .NET 8 console Windows · GDI+ · HttpListener · Android Kotlin
**Sprint:** MVP mirroring MJPEG PC → telefono  **Aggiornamento:** 2026-07-27

## Contesto
Mirroring (non estensione) dello schermo del PC su un telefono Android sulla stessa LAN, via
MJPEG su HTTP. Il server .NET cattura, ridimensiona, codifica in JPEG e serve uno stream
`multipart/x-mixed-replace`. Fasi 1-2 (lato PC) completate e misurate; Fase 3 (app Android)
da fare. H.264 esplicitamente parcheggiato in Fase 5.

## File memoria (carica su richiesta)
> `@file.md` = import Claude · `[[file]]` = wikilink Obsidian (graph). Tieni entrambi.
- @decisions.md — [[decisions]] — scelte tecniche con motivazioni
- @domain.md — [[domain]] — glossario, entità, regole di business
- @sprint.md — [[sprint]] — task correnti e obiettivi
- @conventions.md — [[conventions]] — pattern specifici del progetto
- @tech-debt.md — [[tech-debt]] — debito tecnico con priorità
- @backlog.md — [[backlog]] — funzionalità e idee lungo termine
- @adr.md — [[adr]] — ADR formali

## Segnalibri critici
- **Contratto `/stream`:** `multipart/x-mixed-replace; boundary=frame`, ogni parte con
  `Content-Type: image/jpeg` + `Content-Length`, terminata da CRLF. È il contratto che il
  parser Android deve rispettare byte per byte.
- **Parametri banda:** scale 720p · 20 fps · qualità JPEG 60 → ~9.5 Mbit/s, ~57 KB/frame.
- **Costo misurato:** 22.7 ms/frame (21.4 cattura + 1.2 encode) su schermo 1600×900 → tetto
  teorico ~44 fps. La cattura domina, l'encode JPEG è trascurabile.
- **Perché il telefono non si collega — tre cause distinte, tutte silenziose:** 1) `HttpListener`
  senza `netsh http add urlacl` ripiega su localhost; 2) manca la regola firewall in ingresso
  (HTTP.sys gira nel kernel, PID 4, quindi Windows non mostra mai il popup "consenti l'accesso");
  3) la rete è classificata *Public* mentre la regola vale su *Private*. Nei casi 2 e 3 il browser
  resta appeso senza errore: i pacchetti vengono scartati, non rifiutati. Tutto risolto in un colpo
  da `tools/setup-network.ps1 -TrustNetwork`.
- **Misura su WiFi reale (telefono collegato):** ~20 fps sostenuti, 60 KB/frame, ~10 Mbit/s.
  Il costo per frame sale da 22.7 a ~31 ms quando la rete lavora, comunque sotto i 50 ms di budget.
- **Clean Architecture a 4 layer deliberatamente disattesa** su questo progetto: vedi
  [[conventions]].
