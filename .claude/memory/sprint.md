---
type: sprint
tags: [memory, sprint]
updated: 2026-08-05
---

# Sprint Corrente
Stato lavoro in corso. Aggiornato con /sprint. Backlog in [[backlog]], debito in [[tech-debt]].

## Sprint attivo
- **Nome/Numero:** Rilascio 1.0
- **Periodo:** 2026-07-28 → in corso
- **Obiettivo:** pubblicare il server su GitHub e decidere se l'app merita il Play Store

## Task
- [ ] Creare la chiave di firma (`keytool`, comando in `docs/play-store.md`) e produrre il
      bundle firmato. **Da fare a mano: la chiave non deve passare da qui.**
- [ ] Catturare gli screenshot della scheda — `tools/store-assets.ps1 -Screenshots`
- [ ] Attivare GitHub Pages sul repository per dare un URL pubblico a `docs/privacy.md`
      (Settings → Pages → branch `master`, cartella `/docs`)
- [ ] Verificare il nome nel file `LICENSE`: scritto "Giuseppe Mantello" da
      `.claude/libs/CLAUDE.md`, ma l'indirizzo git è `mantellogioele@gmail.com`
- [ ] Pubblicare la release del server su GitHub con l'archivio di `tools/publish-server.ps1`
- [ ] **Decidere se pubblicare su Play o fermarsi al repository.** La funzione migliore (cavo
      USB, controllo del mouse) richiede il debug USB e quasi nessuno lo attiverà: sullo store
      arriverebbe di fatto la sola modalità Wi-Fi. Vedi la nota finale di `docs/play-store.md`.

## Bloccato da fuori
- [nessun blocco esterno al momento]

## Sbloccato — da valutare per il prossimo sprint
- **Fase 5 — Desktop Duplication e H.264.** Non più bloccata: dal 2026-08-04 un monitor HP E202
  (1600×900) è collegato via HDMI. `--compare` (2026-08-04, 40 giri, 1600×900 → 1280×720,
  qualità 60): **DXGI Duplication 4.0× più veloce di GDI** — mediana 7.2 ms contro 28.7 ms,
  tetto teorico 140 fps contro 35. Conferma piena l'ipotesi del 2026-07-28. Resta un progetto a
  sé (integrazione nel loop di `FrameBroker`, poi H.264/MediaCodec lato Android) — non è nello
  scope di "Rilascio 1.0". Dettagli in [[decisions]], gap noto in [[tech-debt]].

## Corretto di recente
- **2026-08-05 — cursore invisibile nello stream (via cavo e via WiFi).** `CopyFromScreen` non
  include mai il cursore: era così fin dall'inizio, notato solo ora che si comanda il mouse dal
  tablet. `ScreenCapturer` ora lo ridisegna a mano (`GetCursorInfo`/`DrawIconEx`). Verificato con
  `--probe` e sul dispositivo. Dettagli in [[decisions]].

## Note operative
- **Dispositivo di test:** HONOR Pad 10 (HEY3-W09EEA), Android 16 / API 36, `192.168.1.60`.
  Via USB compare come `WPD` finché il debug non è attivo davvero: se `adb devices` è vuoto,
  controlla che compaia anche l'`ADB Interface` tra i dispositivi USB di Windows. Tieni attivo
  *Rimani attivo* nelle opzioni sviluppatore, altrimenti allo spegnimento dello schermo
  l'interfaccia ADB sparisce.
- **Pacchetto Android:** `io.github.gioixxx.monitorextender`. Il namespace del codice resta
  `com.monitorextender.viewer` ed è voluto.
- **Firewall:** Windows ha creato da sé due regole `MonitorExtender.Server` (TCP e UDP, tutte le
  porte, profili Private **e Public**) al primo bind del socket UDP. Coprono la discovery, ma
  sono più larghe di quelle mirate di `tools/setup-network.ps1`: valgono anche su reti pubbliche.
  Da restringere se il PC gira su reti non fidate.

## Storico

### MVP — mirroring MJPEG (2026-07-27 → 2026-07-28) — chiuso
Dalla Fase 0 alla Fase 4 del piano originale, più tre cose nate strada facendo.

- Fase 0-1: progetto .NET 8, cattura + downscale + encode JPEG, 22.7 ms/frame misurati
- Fase 2: server MJPEG su `HttpListener`, 20.3 fps, 0 frame corrotti, 2 client simultanei.
  Milestone: schermo del PC visibile dal browser del telefono
- Fase 3: app Kotlin, parser MJPEG validato su byte reali anche a 7 byte per lettura
- Fase 4: verificata su dispositivo — discovery UDP, 19.3 fps, riconnessione automatica,
  rotazione senza perdita di prestazioni. Correzione decisiva: `setFixedSize` per evitare la
  scalatura software, 13.1 → 19.3 fps
- Extra: collegamento via cavo USB (30.8 fps a risoluzione nativa contro 19.3 via Wi-Fi),
  controllo del PC dal touchscreen, avvio automatico al login con icona e sorveglianza del cavo
- Chiusura: README, licenza MIT, materiale per il rilascio
