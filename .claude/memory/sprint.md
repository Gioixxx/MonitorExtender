---
type: sprint
tags: [memory, sprint]
updated: 2026-08-10
---

# Sprint Corrente
Stato lavoro in corso. Aggiornato con /sprint. Backlog in [[backlog]], debito in [[tech-debt]].

## Sprint attivo
- **Nome/Numero:** Rilascio 1.0
- **Periodo:** 2026-07-28 → in corso
- **Obiettivo:** pubblicare il server su GitHub (fatto) e l'app sul Play Store (in corso)

## Task
- [x] Creare la chiave di firma e produrre il bundle firmato — fatto dall'utente, AAB compilato
- [ ] Catturare gli screenshot della scheda — `tools/store-assets.ps1 -Screenshots`
- [x] Attivare GitHub Pages — online, verificato (landing + privacy → 200)
- [x] Verificare il nome nel file `LICENSE` — confermato "Giuseppe Mantello", nessuna modifica
- [x] Pubblicare la release del server su GitHub — v1.0.0, installer + zip allegati
- [ ] Creare l'account Google Play Console, caricare l'AAB, form data-safety, inviare in
      revisione — **solo utente**, nessuna azione possibile da qui

## Bloccato da fuori
- [nessun blocco esterno al momento]

## Sbloccato — da valutare per il prossimo sprint
- **Fase 5, parte 1 (Desktop Duplication) integrata e in produzione dal 2026-08-05** — non più
  "da valutare", è il capture path di default (con GDI come ripiego automatico, e ora anche un
  interruttore manuale "Modalità compatibilità" per i casi in cui DXGI non cattura contenuti
  WPF renderizzati in hardware). Resta aperta solo la parte 2: H.264/MediaCodec lato Android,
  ancora non iniziata. Dettagli in [[decisions]].

## Corretto di recente
- **2026-08-10 — `android/gradlew` mancante dal repo, CI Android sempre rossa su ubuntu-latest.**
  Solo `gradlew.bat` (Windows) era mai stato committato; lo sviluppo è sempre stato solo su
  Windows, quindi il fallimento del job "App Android" in CI era passato inosservato. Rigenerato
  con `gradlew wrapper --gradle-version 9.6.1`, committato con bit eseguibile (100755). CI
  riverificata verde su entrambi i job.
- **2026-08-10 — `.jks`/`.keystore` non erano ignorati alla radice del repo.**
  `android/.gitignore` copre `*.jks` solo al suo interno; il template
  (`keystore.properties.example`) vuole il file un livello sopra, nella radice — dove non c'era
  nessuna regola. Corretto in `.gitignore` prima che il file venisse mai tracciato.
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
