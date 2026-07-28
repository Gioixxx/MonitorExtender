---
type: sprint
tags: [memory, sprint]
updated: 2026-07-27
---

# Sprint Corrente
Stato lavoro in corso. Aggiornato con /sprint. Backlog in [[backlog]], debito in [[tech-debt]].

## Sprint attivo
- **Nome/Numero:** MVP — mirroring MJPEG
- **Periodo:** 2026-07-27 → in corso
- **Obiettivo:** vedere lo schermo del PC a schermo pieno su un telefono Android sulla stessa WiFi

## Task
- [x] Fase 0 — progetto console .NET 8, parametri banda (720p / 20 fps / q60), contratto HTTP
- [x] Fase 1 — cattura + downscale + encode JPEG in loop, con misura del costo per frame
- [x] Fase 2a — server MJPEG su HttpListener, validato in locale: 20.3 fps, 0 frame corrotti,
      2 client simultanei, 404 sulle rotte ignote, disconnessioni gestite
- [x] Fase 2b — `tools/setup-network.ps1` (urlacl + firewall + rete Private). **Milestone:
      lo schermo del PC si vede dal browser del telefono**, ~20 fps sostenuti su WiFi
- [x] Fase 3 — app Kotlin completa. Parser MJPEG validato da unit test su byte reali del server,
      incluse le letture parziali a 7 byte per volta.
- [x] Fase 4 — **verificata su HONOR Pad 10 (Android 16, 1600×2560, IP 192.168.1.60)**:
      discovery UDP, viewer nativo a 19.3 fps, overlay, riconnessione automatica dopo il kill
      del server, immersive. Parametri live misurati lato server (20.3 → 10.3 fps).
      Rotazione dello schermo verificata: 19.2 fps invariati (quindi il disegno resta sul
      percorso 1:1) e nessuna disconnessione, perché `configChanges` evita di ricreare l'activity.
- [x] Extra — collegamento via cavo USB (`adb reverse`), rilevato automaticamente e preferito
      alla WiFi: 30.8 fps a 1600×900 nativi con qualità 85, contro 19.3 fps a 720p/q60 via WiFi.
- [x] Extra — controllo del PC dal touchscreen del tablet (solo via cavo) e schermata iniziale
      con le due vie di collegamento esplicite. Provato sul dispositivo: cavo e touch funzionano.
- [x] Prossimo — avvio autonomo del server sul PC, senza terminale.
- [ ] Fase 5 — upgrade H.264 (**non iniziata**, progetto a sé). Nota: con l'USB il collo di
      bottiglia della banda sparisce, quindi la spinta verso H.264 vale soprattutto per la WiFi.

## Note operative
- **Dispositivo di test:** HONOR Pad 10 (HEY3-W09EEA), Android 16 / API 36, `192.168.1.60`.
  Via USB compare come `WPD` finché il debug non è attivo davvero: se `adb devices` è vuoto,
  controlla che compaia anche l'`ADB Interface` tra i dispositivi USB di Windows.
- **Firewall:** Windows ha creato da sé due regole `MonitorExtender.Server` (TCP e UDP, tutte le
  porte, profili Private **e Public**) al primo bind del socket UDP. Coprono la discovery, ma
  sono più larghe di quelle mirate di `tools/setup-network.ps1`: valgono anche su reti pubbliche.
  Da restringere se il PC gira su reti non fidate.

## Storico
- [nessuno sprint chiuso]
