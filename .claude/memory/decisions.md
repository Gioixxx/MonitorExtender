---
type: decisions
tags: [memory, architecture]
updated: 2026-08-10
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

### Preparazione al rilascio (2026-07-28)
- **`applicationId` = `io.github.gioixxx.monitorextender`**, il namespace del codice resta
  `com.monitorextender.viewer`. Scelto un DNS inverso di uno spazio realmente controllato
  (github.com/Gioixxx). **Su Play il nome del pacchetto è definitivo** dopo la prima
  pubblicazione: cambiarlo dopo richiederebbe una scheda nuova, perdendo recensioni e
  installazioni. Per questo è stato fatto prima.
- **Licenza MIT.** ⚠️ Il nome nel file `LICENSE` è "Giuseppe Mantello", preso da
  `.claude/libs/CLAUDE.md`; l'indirizzo git è `mantellogioele@gmail.com`. **Da verificare.**
- **Nome mantenuto "MonitorExtender"** pur facendo mirroring: la descrizione dello store lo
  chiarisce tre volte, perché è il primo motivo di recensioni negative.
- **Chiave di firma mai nel repository:** `keystore.properties` e `*.jks` sono in `.gitignore`,
  e `build.gradle.kts` produce un pacchetto **non firmato** se il file manca, invece di fallire.
  Così la compilazione di verifica (R8, dimensioni) resta possibile a chiunque.
- **Distribuzione del server in due tagli:** autonomo (~68 MB, nessun prerequisito) e leggero
  (~0,8 MB, richiede .NET 8). `dist/` è escluso da git: gli archivi si allegano alla release.
- **Dubbio onesto sul senso della pubblicazione:** la funzione migliore (cavo USB, controllo del
  mouse) richiede il debug USB, che quasi nessuno attiverà. Sullo store arriverebbe di fatto
  solo la modalità Wi-Fi. Annotato in `docs/play-store.md`.

### Il PC senza monitor è la causa comune di entrambi i problemi
- **Data:** 2026-07-28
- **Constatazione:** misurando le due fasi separatamente, i **71.7 ms della cattura sono tutti
  nella copia dallo schermo** (`CopyFromScreen`); il ridimensionamento costa 0.0 ms e l'encode
  JPEG 2.4 ms. Non c'è niente da ottimizzare nel nostro codice.
- **Radice unica:** il PC non ha un display collegato — nessun monitor PnP, nessun dispositivo
  enumerato da `EnumDisplayDevices`, risoluzione di ripiego 1600×900. Senza un percorso video
  hardware attivo, `BitBlt` dallo schermo non è accelerato (≈70 ms invece dei 5-15 attesi) **e**
  la Desktop Duplication viene negata. Due sintomi, una causa.
- **Prova da fare, costo minimo:** collegare un monitor vero, oppure il dock NVIDIA, oppure un
  **tappo HDMI fittizio** da pochi euro che fa credere a Windows che ci sia uno schermo. Poi
  rilanciare `--probe` e `--compare`: se la copia scende e la duplicazione smette di essere
  negata, l'ipotesi è confermata e il tetto passa da 13 fps a molto di più.
- **Nota:** finché resta così, chiedere 30 fps è inutile — il tetto è 13. La qualità però conviene
  alzarla lo stesso, perché non costa fps: l'encode resta 2.4 ms.

### Desktop Duplication non è utilizzabile su questa macchina (risultato negativo)
- **Data:** 2026-07-28
- **Esito della misura:** `--compare` non ha potuto confrontare nulla. `IDXGIOutput1::DuplicateOutput`
  restituisce **E_ACCESSDENIED**, riproducibile, dentro e fuori dall'ambiente ristretto.
- **Causa accertata:** il PC **non ha un monitor collegato**. `Get-PnpDevice -Class Monitor` non
  enumera nulla e `WmiMonitorBasicDisplayParams` risponde "non supportato", mentre la scheda
  riporta 1600×900 di ripiego. La Desktop Duplication pretende un'uscita video reale e attiva;
  GDI funziona lo stesso perché una superficie desktop esiste comunque.
- **Configurazione:** Khadas Mind. Intel Arc B390 (iGPU, guida il desktop) + NVIDIA RTX 5060 Ti
  in stato *Unknown*, cioè il dock grafico scollegato. Il processo `Mind` del software di
  sistema consuma stabilmente il 100% di un core: sospetto principale per il degrado di
  `CopyFromScreen` da 21 a 69 ms, ma **non dimostrato**.
- **Conseguenza:** la Fase 5 resta ferma e la cattura resta GDI. Il prototipo e la diagnostica
  restano in `--compare`: costano nulla e la misura diventa immediata il giorno in cui c'è un
  monitor collegato o il dock NVIDIA è attivo.
- **Da rifare quando c'è uno schermo:** `MonitorExtender.Server.exe --compare`.
- **Nota:** se il tablet è davvero l'unico schermo del PC, questo progetto non è un secondo
  monitor ma **il** monitor — il che cambia le priorità (risoluzione headless, avvio senza
  display, robustezza) più di quanto le cambierebbe H.264.

### Avvio automatico: compito pianificato, non servizio Windows
- **Data:** 2026-07-28
- **Decisione:** `tools/autostart.ps1` registra un compito nell'Utilità di pianificazione che
  parte **al login** dell'utente, con 20 s di ritardo, principal `Interactive`, senza privilegi
  di amministratore. Il server è compilato `WinExe` (nessuna finestra) e si riaggancia alla
  console del chiamante con `AttachConsole` quando serve (`--console`, `--probe`).
- **Perché non un servizio Windows:** i servizi girano nella **sessione 0**, isolata dal desktop
  dell'utente. Un servizio partirebbe regolarmente e catturerebbe frame neri. Non è una
  preferenza: è un vincolo dell'architettura di Windows.
- **Perché non la cartella Esecuzione automatica:** niente ritardo configurabile e nessuna
  gestione dello stato. Il compito si installa, si interroga (`-Status`) e si rimuove (`-Remove`)
  con lo stesso script.
- **Impatto:** `tools/autostart.ps1`, `Program.cs`, `MonitorExtender.Server.csproj`, `Log.cs`.

### Windows 11 nasconde le nuove icone di sistema
- **Data:** 2026-07-28
- **Constatazione:** l'icona compare nell'area **nascosta** dietro il chevron `^`, non nella
  barra. È il comportamento predefinito di Windows 11 per ogni nuova icona e non è forzabile via
  API: va trascinata fuori una volta, poi resta.
- **Verificato** aprendo il riquadro delle icone nascoste **usando il controllo del mouse del
  progetto stesso** (`/input`) e guardando il risultato con `/snapshot`.
- **Conseguenza:** al primo avvio l'utente non vede nulla e pensa che il server non sia partito.
  Va detto nella documentazione, non risolto nel codice.

### Il registro va su file, non solo a schermo
- **Data:** 2026-07-28
- **Decisione:** `Log.Write` scrive su `%LOCALAPPDATA%\MonitorExtender\server.log` (rotazione a
  1 MB, una generazione) **e** su console.
- **Perché:** avviato dal compito pianificato il programma non ha console: senza file, qualunque
  diagnosi diventerebbe impossibile. La proprietà si chiama `FilePath` e non `Path` perché
  `Log.Path` nasconderebbe `System.IO.Path` in tutta la classe — errore preso in compilazione.

### La sorveglianza del cavo elimina l'ultimo passaggio manuale
- **Data:** 2026-07-28
- **Decisione:** `UsbLinkWatcher` controlla periodicamente `adb devices` e `adb reverse --list`,
  e ristabilisce l'inoltro appena un dispositivo autorizzato compare. Ritmo adattivo: 15 s con il
  cavo collegato, 4 s in attesa, perché ogni controllo è un avvio di processo.
- **Perché:** `adb reverse` non sopravvive allo scollegamento del cavo né al riavvio del PC.
  Verificato: rimosso l'inoltro a mano e avviato il compito, il log segna
  `[usb] inoltro ristabilito` e **due secondi dopo il tablet si è riconnesso da solo**.
- **Se adb non è installato** la sorveglianza non parte e il resto funziona lo stesso.

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

### Desktop Duplication funziona: era davvero solo il monitor mancante
- **Data:** 2026-08-04
- **Esito:** con l'HP E202 collegato via HDMI, `IDXGIOutput1::DuplicateOutput` non restituisce
  più `E_ACCESSDENIED`. `--compare` (40 giri, 1600×900 → 1280×720, qualità 60): **GDI mediana
  28.7 ms, DXGI Duplication mediana 7.2 ms — 4.0× più veloce**, tetto teorico 140 fps contro 35.
  Conferma piena l'ipotesi del 2026-07-28 ([[decisions]] "Desktop Duplication non è
  utilizzabile su questa macchina"): non era il dock NVIDIA né il processo `Mind`, era
  semplicemente l'assenza di un'uscita video reale.
- **Contesto della misura:** rifatta subito dopo aver scoperto e risolto il bug di
  `ScreenCapturer` che cachava la risoluzione a 1024×768 (vedi [[tech-debt]],
  "ScreenCapturer non si accorge se il monitor cambia a runtime") — il monitor era collegato da
  prima ma il processo server andava riavviato per accorgersene. La cattura GDI dal vivo nel
  log del server (17-22 ms/frame a 1600×900, 1 client) è coerente con la mediana di 28.7 ms
  misurata qui a 1280×720 con un client in più attivo.
- **Nota metodologica:** misurato con il server MJPEG **fermo** (stessa cautela della sessione
  precedente: due catture concorrenti dello stesso schermo si ostacolano a vicenda).
- **Conseguenza:** la Fase 5 (Desktop Duplication + H.264) non è più bloccata da un vincolo
  hardware esterno. Resta un progetto a sé (SPS/PPS, MediaCodec lato Android), ma la parte
  server ha ora un guadagno di velocità dimostrato e misurato, non solo teorico.
- **Impatto:** `DesktopDuplicationCapturer.cs` (già esistente per `--compare`), `Program.cs`.

### Il cursore va disegnato a mano: GDI non lo cattura mai
- **Data:** 2026-08-05
- **Decisione:** `ScreenCapturer.Capture()` chiama `GetCursorInfo` + `GetIconInfo` +
  `DrawIconEx` su `_fullGraphics.GetHdc()` subito dopo `CopyFromScreen`, prima del ridimensiona-
  mento, così il cursore scala insieme al resto del frame invece di essere disegnato a parte.
- **Perché:** segnalato dall'utente — "non si vede il puntatore, sia via cavo che WiFi". Non è
  mai stato un bug introdotto di recente: `CopyFromScreen` **non ha mai incluso il cursore**, è
  il compositore di Windows a disegnarlo separatamente sopra il framebuffer. Passato
  inosservato finché non si è iniziato a comandare il mouse dal tablet ([[decisions]] "Controllo
  del PC dal touchscreen") — senza vederlo, sapere dove si sta per cliccare è impossibile.
- **Dettaglio tecnico:** la posizione di `GetCursorInfo` è l'angolo dell'icona, non il punto
  cliccabile — va corretta con l'hotspot restituito da `GetIconInfo` (`xHotspot`/`yHotspot`),
  altrimenti il cursore disegnato risulta spostato rispetto al vero punto di click.
  `GetIconInfo` alloca due bitmap GDI (`hbmMask`/`hbmColor`) che vanno liberate con
  `DeleteObject` a ogni frame, altrimenti perdita di handle GDI a 20-30 fps.
- **Verificato** con `--probe`: cursore spostato via `Cursor.Position` a una coordinata nota,
  ritrovato nel JPEG catturato esattamente lì (scalato). Confermato anche sul tablet.
- **Alternative:** disegnare il cursore lato Android leggendo `m x y` dal proprio stato locale —
  scartato: funzionerebbe solo per gli input generati dal tablet stesso, non per il cursore
  reale del PC quando qualcun altro lo muove (mouse fisico, altra sessione remota).
- **Impatto:** `ScreenCapturer.cs`. Non si applica a `DuplicationCapturer.cs` (DXGI Desktop
  Duplication esclude il cursore hardware allo stesso modo e richiederebbe la propria API,
  `IDXGIOutputDuplication::GetFramePointerShape` — da fare se/quando la Fase 5 sostituisce GDI).

### DPI awareness esplicita nel capturer
- **Data:** 2026-07-27
- **Decisione:** `ScreenCapturer` chiama `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)`
  con fallback su `SetProcessDPIAware`.
- **Perché:** senza, su display con scaling ≠ 100% Windows riporta una risoluzione virtuale più
  piccola del vero e si cattura un'immagine sfocata e tagliata.
- **Impatto:** `ScreenCapturer.cs`.

### Interruttore manuale GDI/DXGI invece di automatizzare la chiave di registro
- **Data:** 2026-08-10
- **Decisione:** `LiveSettings.PreferGdi` (bool, non persistito) forza `ScreenSourceFactory` a
  usare GDI anche quando Desktop Duplication sarebbe disponibile. Esposto come voce di menu
  checkable "Modalità compatibilità" nell'icona di stato (`TrayIcon`), non da riga di comando.
- **Perché:** segnalato dall'utente — un programma WPF renderizzato in hardware a volte non
  compare nello stream mirrorato da Desktop Duplication (finestra invisibile o nera), finora
  risolto a mano con `reg add HKCU\Software\Microsoft\Avalon.Graphics DisableHWAcceleration`.
  Quel workaround disattiva l'accelerazione hardware per *tutti* i programmi WPF del sistema ed
  è invisibile finché non lo si va a cercare. Un interruttore nel programma che già gestisce la
  cattura risolve lo stesso problema senza toccare impostazioni di sistema estranee al progetto.
- **Alternative:** rilevare automaticamente il caso e passare a GDI da soli — scartato: non c'è
  un segnale affidabile per "questo frame dovrebbe contenere una finestra WPF invisibile" senza
  ispezionare il contenuto catturato, molto più complesso del problema che risolve.
- **Impatto:** `LiveSettings.cs`, `ScreenSourceFactory.cs`, `FrameBroker.cs`, `TrayIcon.cs`,
  `Program.cs`. Non persiste tra riavvii, stesso comportamento di `Scale`/`Fps`/`Quality`.

### Installer Windows senza privilegi di amministratore
- **Data:** 2026-08-10
- **Decisione:** `installer/MonitorExtender.iss` (Inno Setup) installa in `{localappdata}`
  con `PrivilegesRequired=lowest`, non in Program Files. La configurazione di rete e l'avvio
  automatico sono caselle opzionali nel wizard, non passi obbligati; "considera questa rete
  come attendibile" (`-TrustNetwork`) è l'unica non spuntata di default.
- **Perché:** `tools/autostart.ps1` gira già senza privilegi per scelta architetturale (un
  servizio Windows non può catturare lo schermo, vedi sopra), e `tools/setup-network.ps1` si
  autoeleva da sé con un proprio prompt UAC solo quando serve. Se l'installer stesso chiedesse
  admin per installare in Program Files, l'utente vedrebbe due prompt UAC scollegati per la
  stessa installazione. Rendere `-TrustNetwork` opt-in rispecchia lo stesso script: cambia la
  categoria di rete di Windows, una scelta che l'utente deve fare consapevolmente.
- **Alternative:** installer elevato in Program Files — scartato per il doppio UAC. Rete e
  autostart obbligatori senza checkbox — scartato: un installer che configura il firewall senza
  chiedere è esattamente il tipo di comportamento silenzioso che il progetto ha evitato altrove
  (vedi "Le due vie di collegamento vanno mostrate separate").
- **Impatto:** `installer/MonitorExtender.iss`, `tools/build-installer.ps1`. Provato con
  install/uninstall reali: file, task pianificato, urlacl e regole firewall tutti verificati
  presenti dopo l'installazione e assenti dopo la disinstallazione.

### GitHub Pages come landing page, non un secondo repository
- **Data:** 2026-08-10
- **Decisione:** `docs/index.md` + `docs/_config.yml` (tema Jekyll incluso, `cayman`) servono
  sia da pagina di atterraggio con link di download sia da host per `docs/privacy.md`, sullo
  stesso repository.
- **Perché:** GitHub Pages serviva comunque per dare un URL pubblico alla privacy policy
  (richiesta dal form data-safety di Play Console). Usarla anche come landing page costa un
  file in più; un repository separato costerebbe una seconda fonte da tenere allineata a ogni
  release, esattamente il problema che si voleva evitare. Il link di download punta a
  `/releases/latest`, mai a un asset versionato, così non invecchia a ogni nuova release.
- **Alternative:** repository dedicato solo al link di download, proposto dall'utente — scartato
  per il costo di manutenzione doppia a fronte di nessun vantaggio reale (GitHub Release dà già
  hosting binario versionato e gratuito).
- **Impatto:** `docs/index.md`, `docs/_config.yml`.

### Icona dell'exe generata da codice, non importata da un file
- **Data:** 2026-08-10
- **Decisione:** `tools/build-icon.ps1` disegna lo stesso monitor stilizzato di
  `TrayIcon.BuildIcon` (cornice `#171D24`, schermo `#F2A33C`) a 16/32/48/256px e lo impacchetta
  in un `.ico` vero (voci PNG, non DIB — l'unico modo di includere la taglia 256 nel formato
  classico). `MonitorExtender.Server.csproj` la incorpora con `<ApplicationIcon>`.
- **Perché:** stessa logica già in uso per gli asset del Play Store (`tools/store-assets.ps1`):
  un'icona disegnata da codice resta coerente con i colori dell'app e riproducibile, invece di
  un binario da rigenerare a mano ogni volta che la palette cambia. `<ApplicationIcon>` la
  incorpora davvero nell'exe (Explorer, taskbar, scorciatoie), non solo nel wizard
  dell'installer — verificato con `Icon.ExtractAssociatedIcon` dopo la build.
- **Alternative:** icona statica esportata una volta da un editor grafico — scartato per lo
  stesso motivo degli altri asset generati da codice nel progetto.
- **Impatto:** `tools/build-icon.ps1`, `MonitorExtender.Server.csproj`,
  `installer/MonitorExtender.iss` (`SetupIconFile`). La release v1.0.0 già pubblicata è stata
  ricompilata e i suoi asset sostituiti per includerla.

### Rigenerare il wrapper Gradle mancante invece di aggirare il problema in CI
- **Data:** 2026-08-10
- **Decisione:** `android/gradlew` (script Unix, mai committato — solo `gradlew.bat` esisteva)
  rigenerato con `gradlew.bat wrapper --gradle-version 9.6.1`, stessa versione e checksum già
  in uso, committato con bit eseguibile (`100755`).
- **Perché:** il job "App Android" in CI (`ubuntu-latest`) falliva con "No such file or
  directory" perché lo sviluppo è sempre stato solo su Windows: nessuno aveva mai avuto bisogno
  dello script Unix in locale, e il fallimento in CI era passato inosservato. Rigenerarlo con lo
  stesso strumento che genera `gradlew.bat` garantisce che i due script restino sincronizzati.
- **Alternative:** far girare il job Android su `windows-latest` invece di `ubuntu-latest` —
  scartato: sposta il problema (non testa più l'ambiente Unix in cui gira davvero la maggior
  parte delle CI Android) invece di risolverlo.
- **Impatto:** `android/gradlew` (nuovo), `android/gradlew.bat`,
  `android/gradle/wrapper/gradle-wrapper.properties`. Verificato con lo stesso comando della
  pipeline (`./gradlew testDebugUnitTest assembleDebug`) in locale prima del push, poi CI
  riverificata verde su GitHub Actions.
