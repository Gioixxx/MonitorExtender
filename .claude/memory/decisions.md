---
type: decisions
tags: [memory, architecture]
updated: 2026-07-27
---

# Decisioni Architetturali
Registro scelte tecniche con motivazioni.

## Template
### [Titolo breve]
- **Data:** [YYYY-MM-DD]
- **Decisione:** [scelta fatta]
- **Perché:** [motivazione e trade-off]
- **Alternative:** [scartate e perché]
- **Impatto:** [moduli coinvolti] — entità in [[domain]], se formalizzata vedi [[adr]]

---

### Mirroring via MJPEG, non estensione dello schermo
- **Data:** 2026-07-27
- **Decisione:** l'MVP fa *mirroring* dello schermo primario, non un secondo monitor virtuale.
  Trasporto MJPEG su HTTP (`multipart/x-mixed-replace`), PC e telefono sulla stessa LAN.
- **Perché:** MJPEG non richiede encoder, decoder né sincronizzazione: ogni frame è un JPEG
  indipendente, quindi qualsiasi browser lo mostra senza scrivere una riga di client. Questo
  permette di validare tutto il lato server *prima* di aprire Android Studio.
- **Alternative:** H.264 subito — scartato per l'MVP: più fluido e leggero in banda ma aggiunge
  Desktop Duplication API, SPS/PPS e MediaCodec tutti insieme; rimandato a Fase 5 come progetto
  a sé. Estensione con monitor virtuale — richiede un driver display, ordine di grandezza diverso.
- **Impatto:** `src/MonitorExtender.Server/*` — contratto `/stream` in [[domain]].

### Un solo ciclo di cattura per tutti i client
- **Data:** 2026-07-27
- **Decisione:** `FrameBroker` cattura su un thread dedicato e pubblica l'ultimo frame; i client
  HTTP si iscrivono e spediscono quello. Il ciclo si sospende quando non c'è nessun iscritto.
- **Perché:** con la cattura per-client, due telefoni raddoppierebbero il costo CPU per produrre
  la stessa identica immagine. Verificato: 2 client simultanei restano a 20.2/20.4 fps.
- **Alternative:** cattura dentro l'handler della richiesta — più semplice ma non scala e tiene
  la CPU occupata anche senza spettatori.
- **Impatto:** `FrameBroker.cs`, `MjpegServer.cs`.

### Ogni frame è un array nuovo, non un buffer riusato
- **Data:** 2026-07-27
- **Decisione:** `JpegEncoder.Encode` restituisce una copia dei byte invece di esporre il buffer
  interno del `MemoryStream`.
- **Perché:** il frame è condiviso tra più client che scrivono a velocità diverse. Riusare il
  buffer significherebbe sovrascrivere un JPEG mentre un client lento lo sta ancora spedendo.
  A 20 fps × ~57 KB sono ~1.1 MB/s di allocazioni: la gen0 li digerisce senza problemi.
- **Alternative:** buffer pooling con refcount — complessità non giustificata a questi volumi.
- **Impatto:** `JpegEncoder.cs`, `FrameBroker.cs`.

### Cadenza a scadenze assolute + timer di Windows a 1 ms
- **Data:** 2026-07-27
- **Decisione:** il ciclo dorme fino a una scadenza assoluta (`nextDue += intervallo`) e alza la
  risoluzione del timer con `timeBeginPeriod(1)`.
- **Perché:** con la sola `WaitOne(50)` si ottenevano **16.2 fps invece di 20**: il timer di
  Windows si sveglia ogni ~15 ms, quindi ogni attesa da 50 ms ne durava ~62. Con entrambe le
  correzioni: 20.3 fps misurati.
- **Alternative:** busy-wait su `Stopwatch` — preciso ma brucia un core intero.
- **Impatto:** `FrameBroker.cs`.

### Linguaggio dell'app Android: Kotlin
- **Data:** 2026-07-27
- **Decisione:** l'app viewer sarà in Kotlin.
- **Perché:** default di Android Studio, coroutine comode per leggere lo stream in background,
  documentazione recente tutta in Kotlin. Il codice di rete e il parser multipart sono quasi
  identici a Java, quindi il costo del passaggio è minimo.
- **Alternative:** Java — nessun vantaggio tecnico qui, solo familiarità.
- **Impatto:** progetto Android (Fase 3).

### L'interfaccia è il selettore di ingresso di un monitor
- **Data:** 2026-07-28
- **Decisione:** la schermata di scelta è disegnata come il menu OSD di un monitor: fondo scuro
  (`bezel #0F1318`), righe-ingresso con barra di link **ambra** (`signal #F2A33C`) come unico
  colore saturo, etichette in `sans-serif-condensed` maiuscolo spaziato, stati e comandi in
  `monospace`. Nessuna dipendenza aggiunta: entrambi i font sono di sistema.
- **Perché:** il linguaggio visivo viene dal mestiere dell'app invece che da un catalogo di stili.
  Il fondo scuro ha anche una ragione funzionale: il viewer è nero, e prima si passava da una
  schermata bianca al video con uno stacco fastidioso al buio.
- **Elemento firma — la striscia di capacità:** sotto ogni ingresso, `VIDEO · TOCCO · QUALITÀ
  PIENA` contro `VIDEO · SOLA VISIONE`. Rende visibile che i due collegamenti **non sono
  equivalenti**, che è l'informazione mancata all'utente. Struttura che codifica il contenuto,
  non decorazione.
- **Larghezza limitata a 560 dp** da `MainActivity.limitContentWidth()`: in orizzontale il tablet
  è largo oltre 1100 dp e le righe si stiravano da un bordo all'altro.
- **Impatto:** `colors.xml`, `themes.xml`, `activity_main.xml`, `activity_viewer.xml`, drawable
  `input_available`/`input_unavailable`/`code_block`/`button_quiet`/`overlay_panel`.

### La cattura GDI è il vero collo di bottiglia, non la banda
- **Data:** 2026-07-28
- **Constatazione:** a fine sessione `--probe` misurava **150 ms/frame** contro i 21 di partenza,
  stesso codice e stessa risoluzione. Due cause distinte, entrambe verificate:
  1. **metà era un artefatto di misura**: il probe girava mentre il server catturava già per il
     tablet, e due catture concorrenti dello stesso schermo si ostacolano (150 → 69 ms fermando
     il server). *Da ricordare: non misurare la cattura mentre il server è in esecuzione.*
  2. i restanti 69 ms contro 21 sono degrado ambientale: `CopyFromScreen` rallenta al crescere
     del carico di composizione del desktop. L'encode JPEG resta a 1.9 ms, quindi è la cattura.
- **Conseguenza sulla Fase 5:** il motivo per passare a Desktop Duplication API non è più la
  banda (via cavo abbondante) ma **il costo di cattura**, che con GDI è instabile e può triplicare
  senza che il codice cambi. H.264 e Desktop Duplication vanno valutati separatamente: il secondo
  ha più valore del primo.

### Controllo del PC dal touchscreen, solo via cavo
- **Data:** 2026-07-28
- **Decisione:** il tablet comanda il mouse del PC (`SendInput`), ma l'endpoint `/input` accetta
  comandi **solo da loopback**, quindi solo attraverso il cavo. Sulla WiFi resta di sola visione.
- **Perché:** trasmettere lo schermo è passivo; accettare input è un telecomando. Senza
  autenticazione, chiunque sulla stessa rete potrebbe cliccare al posto tuo. Il vincolo su
  loopback elimina il problema invece di gestirlo con un token da custodire.
- **Mappatura scelta — monitor touch, non trackpad:** il tasto sinistro si preme **subito** al
  tocco, senza attendere per distinguere click da trascinamento, perché quell'attesa si
  sentirebbe su ogni tocco. Conseguenza obbligata: il click destro non può essere la pressione
  lunga (il sinistro è già premuto) ed è il tocco a due dita; all'arrivo del secondo dito il
  sinistro viene rilasciato. Due dita in scorrimento = rotella, tre dita = overlay.
- **Limiti di Windows, non aggirabili:** un processo non elevato non inietta input nelle finestre
  di app elevate (UIPI), e sul desktop sicuro (UAC, blocco schermo) l'iniezione non funziona
  affatto.
- **Trappola:** i numeri vanno formattati con `Locale.US`. In italiano `%.4f` produce `0,5123` e
  il parser lato server (InvariantCulture) non lo legge.
- **Impatto:** `InputInjector.cs`, `MjpegServer.HandleInputAsync`, `InputSender.kt`,
  `TouchController.kt`.

### Le due vie di collegamento vanno mostrate separate
- **Data:** 2026-07-28
- **Decisione:** la schermata iniziale espone **due schede distinte** — cavo USB e WiFi — ognuna
  con il proprio stato, invece di un solo pulsante "cerca" che provava il cavo e ripiegava sulla
  rete in silenzio.
- **Perché:** segnalato dall'uso reale. Con il ripiego automatico non c'era modo di sapere che il
  cavo esistesse, né perché non avesse funzionato: si finiva sempre sulla WiFi senza accorgersene.
  Ora la scheda del cavo, quando non è disponibile, dice esattamente quale comando lanciare sul PC.
- **Regola generale che ne esce:** un ripiego automatico silenzioso nasconde all'utente sia
  l'opzione migliore sia il motivo per cui non è attiva.
- **Impatto:** `activity_main.xml`, `MainActivity.kt`, `Discovery.findUsb`/`findOnLan`.

### Collegamento via cavo USB con `adb reverse`
- **Data:** 2026-07-27
- **Decisione:** oltre alla WiFi, il tablet può collegarsi via USB. `adb reverse tcp:8080 tcp:8080`
  (in `tools/usb-link.ps1`) inoltra la porta lungo il cavo; l'app vede il server su `127.0.0.1`.
  `Discovery.findFirst()` prova **prima** il cavo e ripiega sul broadcast UDP.
- **Perché:** misurato sullo stesso stream (900p, 30 fps, q85, 150 KB/frame), un client per volta:
  **WiFi 13.8 Mbit/s → consegna ~11 dei 30 fps prodotti; USB 36.8 Mbit/s → li consegna tutti.**
  Il cavo satura quello che il server produce. Nell'app, via USB: 30.8 fps a risoluzione nativa
  1600×900 con qualità 85, contro 19.3 fps a 720p/q60 via WiFi.
- **Costo:** zero lato server — non passa dalla rete, quindi **niente firewall e niente urlacl**,
  verificato eseguendo il server su una porta senza prenotazione. Perché valesse davvero è servita
  una correzione: il ripiego senza urlacl registrava solo `http://localhost:PORTA/`, e HTTP.sys
  smista confrontando l'hostname **testuale** dell'header `Host`, non l'indirizzo risolto — quindi
  la richiesta del tablet a `http://127.0.0.1:PORTA/` riceveva `400 Invalid Hostname`. Ora vengono
  registrati entrambi i prefissi.
- **Funziona completamente offline:** verificato spegnendo la WiFi del tablet — 30.6 fps invariati,
  mentre lo stesso server via IP di LAN diventava irraggiungibile.
- **Richiede però il debug USB attivo:** è una funzione da sviluppatore, non qualcosa che si
  possa chiedere a un utente qualsiasi.
- **Alternative:** USB tethering inverso — richiede configurazione di rete sul dispositivo,
  molto più invasivo per lo stesso risultato.
- **Impatto:** `tools/usb-link.ps1`, `Discovery.kt`, `MainActivity.kt`.

### Il disegno non scala: `setFixedSize` + copia 1:1
- **Data:** 2026-07-27
- **Decisione:** `MjpegSurfaceView` fissa il buffer della superficie alla dimensione esatta del
  frame (`holder.setFixedSize`) e disegna 1:1; l'ingrandimento allo schermo lo fa il compositore
  hardware. Il letterbox è gestito da `onMeasure`, una volta per frame-size, non a ogni frame.
- **Perché:** misurato sul tablet. `lockCanvas` restituisce un canvas **software**: scalare un
  1280×720 su 1600×2560 a ogni frame costava più della decodifica JPEG e teneva il client a
  **13.1 fps mentre il server ne serviva 19.4**. Con la copia 1:1: **19.3 fps**, intervallo medio
  76 → 51 ms, picco 133 → 97 ms.
- **Alternative:** `lockHardwareCanvas()` — avrebbe risolto anche quello, ma `setFixedSize`
  elimina il lavoro invece di accelerarlo, ed è l'approccio classico per i video su SurfaceView.
- **Impatto:** `MjpegSurfaceView.kt`, `activity_viewer.xml`.

### Il blocco dell'orientamento non funziona sui tablet Android 16
- **Data:** 2026-07-27
- **Constatazione (non una scelta):** `android:screenOrientation="sensorLandscape"` è **ignorato**
  su questo dispositivo. Verificato: `wm get-ignore-orientation-request` → `true for displayId=0`,
  e il display misura 711 dp di larghezza contro la soglia di 600 dp oltre la quale Android 16
  toglie alle app il controllo dell'orientamento.
- **Conseguenza:** l'attributo resta nel manifest perché sui telefoni vale ancora, ma il viewer
  non può contarci: il verticale va gestito comunque, e il letterbox di `onMeasure` lo fa.
- **Impatto:** `AndroidManifest.xml`, `MjpegSurfaceView.onMeasure`.

### I parametri live sono condivisi tra tutti i client
- **Data:** 2026-07-27
- **Decisione:** `/stream?fps=&q=&scale=` riconfigura **lo stream condiviso**: l'ultima richiesta
  vince e vale per chiunque sia collegato.
- **Perché:** la cattura è una sola per scelta (vedi sopra). Dare parametri indipendenti a ogni
  client richiederebbe un ciclo di cattura per profilo, moltiplicando il costo CPU per risolvere
  un problema che in un mirroring personale non si presenta: il client è normalmente uno.
- **Alternative:** ri-encodare per client dal bitmap condiviso — i `Bitmap` di GDI+ non sono
  thread-safe in lettura concorrente, servirebbe una copia per client.
- **Impatto:** `LiveSettings.cs`, `FrameBroker.cs`, `MjpegServer.ApplyQuerySettings`.

### La discovery usa l'indirizzo del mittente, non quello dichiarato
- **Data:** 2026-07-27
- **Decisione:** il server risponde alla sonda UDP con `{"service","name","port"}` **senza il
  proprio IP**; il client ricava l'indirizzo dal mittente del pacchetto.
- **Perché:** verificato sul campo: con WSL e Hyper-V installati il PC ha più interfacce e
  rispondendo in broadcast locale il mittente risultava `172.29.64.1` (adattatore virtuale),
  non `192.168.1.62`. Un server che dichiara il proprio IP sceglierebbe quasi sempre quello
  sbagliato; il mittente del pacchetto è per costruzione l'indirizzo raggiungibile dal client.
- **Impatto:** `DiscoveryResponder.cs`, `Discovery.kt`. Richiede una regola firewall **UDP**
  separata: quella TCP non la copre.

### Vincoli della toolchain Android (AGP 9 + SDK 36)
- **Data:** 2026-07-27
- **Decisione:** niente plugin `org.jetbrains.kotlin.android`, e `androidx.core` fermo a 1.17.0,
  `activity` a 1.12.4, `lifecycle` a 2.9.4.
- **Perché:** due vincoli scoperti compilando, non ipotizzati. 1) Da **AGP 9 il supporto Kotlin è
  integrato** nel plugin Android: applicare anche il plugin Kotlin non è ridondante, è un errore
  che blocca la configurazione. 2) Le versioni androidx più recenti (core 1.19, activity 1.13,
  lifecycle 2.11) **pretendono compileSdk 37**, mentre l'SDK installato si ferma ad android-36.1.
  Aggiornare l'SDK sarebbe l'alternativa, ma per un viewer che usa solo `SurfaceView`, `OkHttp` e
  le coroutine non c'è nulla in quelle versioni che serva.
- **Alternative:** installare le platform 37 dall'SDK Manager — rimandato a quando servirà davvero.
- **Impatto:** `android/gradle/libs.versions.toml`, `android/app/build.gradle.kts`.

### DPI awareness esplicita nel capturer
- **Data:** 2026-07-27
- **Decisione:** `ScreenCapturer` chiama `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)`
  con fallback su `SetProcessDPIAware`.
- **Perché:** senza, su display con scaling ≠ 100% Windows riporta una risoluzione virtuale più
  piccola del vero e si cattura un'immagine sfocata e tagliata.
- **Impatto:** `ScreenCapturer.cs`.
