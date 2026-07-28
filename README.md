# MonitorExtender

Trasforma un tablet Android nello schermo di un PC Windows, via cavo USB o via Wi-Fi.
Dal tablet puoi anche comandare il mouse del PC.

> **Duplica lo schermo, non lo aggiunge.** Nonostante il nome, il tablet mostra lo stesso
> desktop del PC: non diventa un secondo monitor indipendente. Un vero secondo schermo
> richiederebbe un driver di display virtuale, che è un progetto di tutt'altro ordine.

Nato per un caso d'uso preciso: un mini PC senza monitor, con il tablet come unico schermo.

---

## Come funziona

Il PC cattura lo schermo, lo ridimensiona, lo codifica in JPEG e lo serve come stream
`multipart/x-mixed-replace` su HTTP. Il tablet lo legge e lo disegna. Nessun codec video,
nessuna negoziazione: ogni frame è un'immagine indipendente, quindi lo si può guardare anche
da un browser qualsiasi.

```
┌─────────────────── PC Windows ───────────────────┐        ┌──── Tablet Android ────┐
│  cattura schermo → ridimensiona → JPEG           │        │                        │
│         │                                        │        │   parser multipart     │
│         └──→ HTTP  /stream ──────────────────────┼───────→│   decodifica JPEG      │
│              UDP   ricerca automatica            │  cavo  │   disegno su Surface   │
│              HTTP  /input  ←─────────────────────┼────────┤   gesti → mouse        │
│                            (solo da loopback)    │  o LAN │                        │
└──────────────────────────────────────────────────┘        └────────────────────────┘
```

## Due collegamenti, non equivalenti

| | Cavo USB | Wi-Fi |
|---|---|---|
| Banda misurata | **36,8 Mbit/s** | 13,8 Mbit/s |
| Qualità richiesta dall'app | nativa · 30 fps · q85 | 720p · 20 fps · q60 |
| Comando del mouse | ✅ | ❌ |
| Configurazione richiesta | debug USB attivo | `tools/setup-network.ps1` |
| Funziona senza rete | ✅ | ❌ |

Il cavo satura quello che il server riesce a produrre; il Wi-Fi consegna circa un terzo dei
frame quando la qualità è alta. Per questo l'app chiede parametri diversi a seconda di dove
passa, e per questo il comando del mouse è **accettato solo da loopback**: trasmettere lo
schermo è passivo, accettare input è un telecomando, e senza autenticazione chiunque sulla
rete potrebbe cliccare al posto tuo.

## Avvio rapido

### Sul PC

```powershell
dotnet build src/MonitorExtender.Server
tools\setup-network.ps1        # solo per il Wi-Fi: urlacl, firewall, rete privata
tools\autostart.ps1            # opzionale: parte da solo al login
```

Il server compare come icona accanto all'orologio. **Windows 11 la nasconde dietro il chevron
`^`**: trascinala fuori una volta e ci resta, altrimenti sembrerà che non sia partito.

### Sul tablet

Installa l'app, aprila e tocca una delle due schede. Se il cavo non risulta pronto, la scheda
stessa dice quale comando eseguire sul PC:

```powershell
tools\usb-link.ps1             # inoltra la porta lungo il cavo (adb reverse)
```

Serve il **debug USB** attivo sul tablet. Conviene attivare anche *Rimani attivo* nelle opzioni
sviluppatore: senza, lo schermo si spegne e l'interfaccia ADB sparisce con lui.

### Senza installare niente

Il server è guardabile da qualsiasi browser: `http://<ip-del-pc>:8080/`

## Gesti sul tablet

Valgono quando sei collegato via cavo, dove il tocco comanda il PC.

| Gesto | Effetto |
|---|---|
| Un dito | premi, muovi, rilascia — click e trascinamento |
| Due dita, scorrimento | rotella |
| Due dita, tocco | tasto destro |
| Tre dita, tocco | mostra o nasconde le statistiche |

La mappatura è quella di un monitor touch, non di un trackpad: tocchi dove vuoi cliccare. Il
tasto sinistro si preme subito, senza attendere di capire se sarà un click o un trascinamento —
quell'attesa si sentirebbe su ogni tocco. Ne consegue che il tasto destro non può essere la
pressione lunga, ed è il tocco a due dita.

## Endpoint del server

| Rotta | Cosa fa |
|---|---|
| `/` | pagina di prova a schermo intero |
| `/stream` | lo stream MJPEG |
| `/snapshot` | un singolo frame JPEG |
| `/info` | parametri correnti in JSON |
| `/input` | comandi del mouse — **solo da loopback** |

I parametri si cambiano a caldo: `/stream?scale=1080&fps=30&q=85`. Sono condivisi da tutti i
client collegati, perché la cattura è una sola.

## Strumenti

| Script | A cosa serve |
|---|---|
| `tools/setup-network.ps1` | prenotazione URL, regole firewall, categoria di rete. `-Remove` per disfare |
| `tools/usb-link.ps1` | inoltra la porta lungo il cavo. `-Remove` per togliere |
| `tools/autostart.ps1` | avvio al login. `-Status`, `-Remove` |
| `tools/test-stream.ps1` | verifica il contratto MJPEG byte per byte e misura fps e banda |
| `tools/test-discovery.ps1` | manda la sonda UDP e stampa chi risponde |

Il server ha due modalità di diagnosi:

```powershell
MonitorExtender.Server.exe --console    # in primo piano, con il registro a schermo
MonitorExtender.Server.exe --probe      # misura il costo di un singolo frame
MonitorExtender.Server.exe --compare    # confronta cattura GDI e Desktop Duplication
```

Il registro sta in `%LOCALAPPDATA%\MonitorExtender\server.log`.

## Requisiti

- **PC**: Windows 10 o 11, .NET 8
- **Tablet o telefono**: Android 8.0 (API 26) o successivo
- **Per il cavo**: debug USB attivo e `adb` installato (arriva con Android Studio)
- **Per il Wi-Fi**: PC e tablet sulla stessa rete

## Limiti noti

Nessuno di questi è un difetto da correggere: sono vincoli del sistema operativo o
conseguenze di scelte consapevoli.

- **Il comando del mouse non funziona sulle finestre di app amministratore** (UIPI), e non
  funziona affatto sul desktop sicuro — prompt UAC, schermata di blocco. È una protezione di
  Windows, non è aggirabile.
- **Il blocco dell'orientamento è ignorato sui tablet con Android 16**: oltre i 600 dp di
  larghezza il sistema toglie alle app il controllo dell'orientamento. Il video si adatta
  comunque, con le bande nere.
- **Il server non può essere un servizio Windows**: i servizi girano nella sessione 0, isolata
  dal desktop, e catturerebbero frame neri.
- **Il cavo richiede il debug USB**, quindi resta una funzione da sviluppatore: non è
  qualcosa che si possa chiedere a un utente qualsiasi.
- **`adb reverse` non sopravvive** allo scollegamento del cavo né al riavvio del PC. Il server
  lo ristabilisce da solo appena il dispositivo ricompare.

## Prestazioni

Misurate su HONOR Pad 10 (Android 16) e un mini PC con Intel Arc B390, schermo 1600×900.

```
cattura schermo (GDI)   71,7 ms      ← il collo di bottiglia
ridimensionamento        0,0 ms
codifica JPEG            2,4 ms
                        ─────────
                        74,1 ms/frame → tetto ~13 fps
```

Su una macchina con un monitor collegato la cattura costava **21 ms** (~45 fps). La differenza
non dipende dal codice: **questo PC non ha un display attaccato**, e senza un percorso video
hardware attivo `BitBlt` non è accelerato — e la Desktop Duplication API viene negata con
`E_ACCESSDENIED`. Due sintomi, una causa.

Se hai un monitor, un dock grafico o anche solo un tappo HDMI fittizio, collegalo e verifica:

```powershell
MonitorExtender.Server.exe --probe
MonitorExtender.Server.exe --compare
```

Il prototipo di cattura via GPU è già nel codice, dietro `--compare`: manca solo una macchina
su cui la duplicazione sia permessa.

## Preparare una distribuzione

```powershell
tools\publish-server.ps1                        # autonomo, ~68 MB, non richiede .NET
tools\publish-server.ps1 -FrameworkDependent    # ~1 MB, richiede .NET 8 installato
```

Produce un archivio in `dist/` con l'eseguibile, gli script e questo README, pronto da
allegare a una release GitHub.

Per l'app Android, la procedura completa di pubblicazione è in
[`docs/play-store.md`](docs/play-store.md).

## Compilare

```powershell
dotnet build src/MonitorExtender.Server          # server
cd android && .\gradlew.bat assembleDebug        # app
cd android && .\gradlew.bat testDebugUnitTest    # 16 test
```

Per una release firmata dell'app, copia `android/keystore.properties.example` in
`android/keystore.properties` e compilalo. Senza quel file la compilazione riesce comunque e
produce un pacchetto non firmato.

## Licenza

MIT — vedi [LICENSE](LICENSE).
