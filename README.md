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

## Installazione

Per chi vuole solo usarlo, senza compilare niente.

### Sul PC

1. Scarica l'ultimo installer dalle [Release](https://github.com/Gioixxx/MonitorExtender/releases/latest) — `MonitorExtenderSetup-<versione>.exe`.
2. Eseguilo. Il wizard chiede se configurare la rete per il Wi-Fi (compare un prompt di
   amministratore: serve solo per firewall e prenotazione URL, gestito da
   `tools/setup-network.ps1`) e se avviare MonitorExtender automaticamente al login.
3. Il server compare come icona accanto all'orologio. **Windows 11 la nasconde dietro il
   chevron `^`**: trascinala fuori una volta e ci resta, altrimenti sembrerà che non sia partito.

### Sul tablet

Installa l'app (dalle stesse Release finché non è sul Play Store), aprila e tocca una delle due
schede — cavo USB o Wi-Fi, vedi sopra. Se il cavo non risulta pronto, la scheda stessa dice
quale comando eseguire sul PC (`tools\usb-link.ps1`, richiede il **debug USB** attivo sul
tablet — conviene attivare anche *Rimani attivo* nelle opzioni sviluppatore, altrimenti lo
schermo si spegne e l'interfaccia ADB sparisce con lui).

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
| `tools/build-installer.ps1` | compila l'installer Windows (`dist\MonitorExtenderSetup-*.exe`) |

Il server ha due modalità di diagnosi:

```powershell
MonitorExtender.Server.exe --console    # in primo piano, con il registro a schermo
MonitorExtender.Server.exe --probe      # misura il costo di un singolo frame
MonitorExtender.Server.exe --compare    # confronta cattura GDI e Desktop Duplication
```

Il registro sta in `%LOCALAPPDATA%\MonitorExtender\server.log`.

## Requisiti

- **PC**: Windows 10 o 11. Nessun prerequisito con l'installer (build autonoma); `dotnet build`
  dai sorgenti richiede invece .NET 8
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

Il server cattura con **Desktop Duplication (DXGI)** quando può, e ripiega su GDI in automatico
solo se la duplicazione non è disponibile (nessun monitor collegato, sessione Remote Desktop,
un altro processo la occupa già). Misurato su HONOR Pad 10 (Android 16), schermo 1600×900
ridimensionato a 1280×720, qualità 60, confronto diretto sullo stesso ciclo (`--compare`, 40 giri):

```
Desktop Duplication (DXGI)   mediana  7,2 ms   → tetto teorico ~140 fps
GDI CopyFromScreen            mediana 28,7 ms   → tetto teorico  ~35 fps
```

**4,0× più veloce**, senza cambiare nulla nel codice a valle: il ridimensionamento e la codifica
JPEG restano gli stessi, cambia solo come il frame arriva dallo schermo.

Il ripiego su GDI resta utile su macchine senza un'uscita video reale attiva: lì la duplicazione
viene negata con `E_ACCESSDENIED` e `BitBlt`/`CopyFromScreen` non è accelerato, quindi la
cattura può costare 3-4× di più (misurato: 71,7 ms invece di 21 ms sulla stessa macchina, prima e
dopo aver collegato un monitor).

Per rifare la misura sulla tua macchina:

```powershell
MonitorExtender.Server.exe --probe
MonitorExtender.Server.exe --compare
```

Se un programma **WPF** compare invisibile o nero nello stream mirrorato — un limite noto di
Desktop Duplication con contenuti renderizzati in hardware — apri il menu dell'icona vicino
all'orologio e attiva **"Modalità compatibilità"**: forza la cattura su GDI senza toccare
impostazioni di sistema.

## Eseguire dai sorgenti (sviluppo)

```powershell
dotnet build src/MonitorExtender.Server
tools\setup-network.ps1        # solo per il Wi-Fi: urlacl, firewall, rete privata
tools\autostart.ps1            # opzionale: parte da solo al login, con l'eseguibile Debug
```

Equivalente manuale di quello che fa l'installer — utile per compilare ed eseguire senza
impacchettare nulla.

## Preparare una distribuzione

```powershell
tools\publish-server.ps1                        # autonomo, ~68 MB, non richiede .NET
tools\publish-server.ps1 -FrameworkDependent    # ~1 MB, richiede .NET 8 installato
tools\build-installer.ps1                       # installer completo: compila + Inno Setup
```

`publish-server.ps1` produce un archivio in `dist/` con l'eseguibile, gli script e questo
README, pronto da allegare a una release GitHub. `build-installer.ps1` fa un passo in più:
incatena la build autonoma con [Inno Setup](https://jrsoftware.org/isinfo.php) (`ISCC.exe`,
dipendenza solo di questa macchina di build, mai di chi installa) e produce
`dist\MonitorExtenderSetup-<versione>.exe` — lo stesso installer scaricabile dalla sezione
["Installazione"](#installazione) qui sopra. Lo script `.iss` è in `installer/MonitorExtender.iss`.

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
