# Changelog

Il formato segue [Keep a Changelog](https://keepachangelog.com/it/1.1.0/) e le versioni
seguono il [versionamento semantico](https://semver.org/lang/it/).

## [1.0.0] — 2026-07-28

Prima versione pubblica.

### Server Windows

- Cattura dello schermo primario, ridimensionamento e codifica JPEG in un solo ciclo
  condiviso da tutti i client collegati, sospeso quando non c'è nessuno.
- Stream `multipart/x-mixed-replace` su `HttpListener`, con `/snapshot`, `/info` e una pagina
  di prova a schermo intero.
- Parametri modificabili a caldo dalla query string: `?scale=&fps=&q=`.
- Ricerca automatica del server sulla rete tramite sonda UDP in broadcast.
- Comando del mouse via `SendInput`, accettato **solo da loopback**, cioè solo attraverso il
  cavo USB.
- Avvio al login tramite Utilità di pianificazione, icona di stato accanto all'orologio,
  registro su file con rotazione.
- Sorveglianza dell'inoltro USB: `adb reverse` viene ristabilito da solo appena il
  dispositivo ricompare.
- Banco di prova `--compare` fra cattura GDI e Desktop Duplication API.

### App Android

- Viewer nativo con OkHttp, parser MJPEG scritto a mano e disegno su `SurfaceView`.
- Viewer alternativo su WebView, tenuto come riferimento diagnostico.
- Schermata di scelta con le due vie di collegamento esplicite e il proprio stato.
- Rilevamento automatico del server: prima il cavo, poi la rete.
- Comando del PC dal touchscreen con la mappatura di un monitor touch.
- Riconnessione automatica con attesa crescente, statistiche di fps e latenza,
  modalità a schermo intero.
- 16 test unitari, fra cui la verifica del parser su byte reali del server.

### Strumenti

- `setup-network.ps1`, `usb-link.ps1`, `autostart.ps1`, `test-stream.ps1`,
  `test-discovery.ps1`.
