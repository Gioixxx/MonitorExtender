# Pubblicazione sul Google Play Store

Tutto il materiale della scheda e la procedura, in ordine. Le parti che richiedono una tua
decisione o le tue credenziali sono marcate **[tu]**.

---

## 1. Prima di caricare

### Chiave di firma **[tu]**

Va creata una volta sola e conservata fuori dal repository. **Se la perdi non puoi più
aggiornare l'app pubblicata**, salvo chiedere a Google il ripristino della chiave di
caricamento.

```powershell
& "$env:ProgramFiles\Android\Android Studio\jbr\bin\keytool.exe" -genkeypair -v `
    -keystore monitorextender-upload.jks -alias upload `
    -keyalg RSA -keysize 4096 -validity 10000
```

Poi copia `android/keystore.properties.example` in `android/keystore.properties` e compilalo.
Entrambi i file (`.jks` e `keystore.properties`) sono già esclusi da git.

### Costruire il pacchetto

Google Play accetta **App Bundle**, non APK:

```powershell
cd android
.\gradlew.bat bundleRelease
# → app/build/outputs/bundle/release/app-release.aab
```

Senza `keystore.properties` la compilazione riesce lo stesso ma produce un pacchetto non
firmato, buono solo per verificare che R8 non rompa nulla.

### Informativa sulla privacy **[tu]**

Play la richiede a **ogni** app, anche a quelle che non raccolgono nulla, e vuole un URL
pubblico. Il testo è pronto in [`docs/privacy.md`](privacy.md). Per avere un indirizzo:

- attiva **GitHub Pages** sul repository (Settings → Pages → branch `master`, cartella `/docs`)
- l'URL diventa `https://gioixxx.github.io/MonitorExtender/privacy`

---

## 2. Scheda dello store

### Dati di base

| Campo | Valore |
|---|---|
| Nome applicazione | `MonitorExtender` |
| Nome del pacchetto | `io.github.gioixxx.monitorextender` |
| Categoria | Strumenti |
| Tipo | App gratuita, senza acquisti in-app |
| Contiene annunci | No |

### Descrizione breve (max 80 caratteri)

```
Il tuo tablet diventa lo schermo del PC, via cavo USB o Wi-Fi.
```

### Descrizione completa

> Il primo paragrafo chiarisce subito che duplica lo schermo invece di aggiungerne uno: è la
> cosa che genera più recensioni negative se la si scopre dopo l'installazione.

```
MonitorExtender mostra sul tuo tablet lo schermo di un PC Windows, e ti permette di
comandarne il mouse dal touchscreen.

IMPORTANTE: l'app DUPLICA lo schermo del PC, non ne aggiunge uno secondo. Vedrai lo
stesso desktop che c'è sul computer, non una scrivania indipendente. È pensata per usare
un tablet come schermo di un mini PC, oppure per tenere il computer sott'occhio da
un'altra postazione.

Serve un programma gratuito e open source da installare sul PC Windows, scaricabile da
github.com/Gioixxx/MonitorExtender

DUE MODI DI COLLEGARSI

• Via cavo USB — molto più veloce e nitido, e l'unico che permette di comandare il mouse
  del PC dal touchscreen. Richiede il debug USB attivo sul tablet.
• Via Wi-Fi — nessun cavo, PC e tablet sulla stessa rete. Solo visione.

L'app trova il PC da sola: nella schermata iniziale ti dice quali collegamenti sono
pronti e, se il cavo non lo è, quale comando eseguire sul computer.

COMANDARE IL PC DAL TOUCHSCREEN (solo via cavo)

• un dito: click e trascinamento
• due dita in scorrimento: rotella
• due dita, tocco: tasto destro
• tre dita, tocco: statistiche di fps e latenza

FUNZIONA ANCHE SENZA INTERNET

Il collegamento via cavo non usa la rete: né Wi-Fi né dati. Utile dove una rete non c'è.

PRIVACY

Nessun dato raccolto, nessuna pubblicità, nessun servizio di analisi. Le immagini dello
schermo viaggiano solo fra il tuo computer e il tuo dispositivo.

COSA SERVE

• un PC con Windows 10 o 11
• Android 8.0 o successivo
• per il cavo: debug USB attivo sul tablet
• per il Wi-Fi: PC e tablet sulla stessa rete

LIMITI DA CONOSCERE

• Il comando del mouse non agisce sulle finestre delle applicazioni avviate come
  amministratore, né sulle schermate di sistema di Windows: è una protezione del sistema
  operativo, non un difetto dell'app.
• Il collegamento via cavo richiede di attivare le opzioni sviluppatore sul tablet.

Codice sorgente, documentazione e programma per il PC:
github.com/Gioixxx/MonitorExtender
```

### English listing

Short description:

```
Turn your tablet into your PC's screen, over USB cable or Wi-Fi.
```

Full description:

```
MonitorExtender shows a Windows PC's screen on your tablet, and lets you control the
PC's mouse from the touchscreen.

IMPORTANT: this app MIRRORS the PC screen, it does not add a second one. You will see
the same desktop that is on the computer, not an independent workspace. It is meant for
using a tablet as the screen of a mini PC, or for keeping an eye on a computer from
another desk.

It requires a free, open source companion program on the Windows PC, available at
github.com/Gioixxx/MonitorExtender

TWO WAYS TO CONNECT

• Over USB cable — much faster and sharper, and the only way to control the PC's mouse
  from the touchscreen. Requires USB debugging enabled on the tablet.
• Over Wi-Fi — no cable, PC and tablet on the same network. Viewing only.

The app finds the PC by itself: the first screen tells you which connections are ready
and, when the cable is not, which command to run on the computer.

CONTROLLING THE PC (cable only)

• one finger: click and drag
• two fingers sliding: scroll wheel
• two fingers, tap: right click
• three fingers, tap: frame rate and latency readout

WORKS WITHOUT INTERNET

The cable connection uses no network at all: no Wi-Fi, no mobile data.

PRIVACY

No data collected, no ads, no analytics. Screen images travel only between your computer
and your device.

REQUIREMENTS

• a PC running Windows 10 or 11
• Android 8.0 or later
• for the cable: USB debugging enabled on the tablet
• for Wi-Fi: PC and tablet on the same network

KNOWN LIMITS

• Mouse control does not act on windows of applications running as administrator, nor on
  Windows system screens: this is an operating system protection, not a bug.
• The cable connection requires enabling developer options on the tablet.

Source code, documentation and the PC program:
github.com/Gioixxx/MonitorExtender
```

---

## 3. Modulo Sicurezza dei dati

Play chiede di dichiarare cosa raccogli. Le risposte per questa app:

| Domanda | Risposta |
|---|---|
| L'app raccoglie o condivide dati utente richiesti? | **No** |
| I dati sono criptati in transito? | Non applicabile — nessun dato raccolto |
| Gli utenti possono chiedere la cancellazione dei dati? | Non applicabile |

L'ultimo indirizzo digitato resta nelle preferenze locali e **non** conta come raccolta: non
lascia il dispositivo.

## 4. Classificazione dei contenuti

Questionario IARC, categoria **Utilità / Produttività**. Nessun contenuto violento, sessuale,
di gioco d'azzardo o generato dagli utenti. Nessuna condivisione di posizione o dati personali.
Esito atteso: **PEGI 3 / Everyone**.

## 5. Materiali grafici

Generati da `tools/store-assets.ps1` in `docs/store/`.

| Materiale | Formato richiesto | File |
|---|---|---|
| Icona | 512×512 PNG, senza trasparenza | `icon-512.png` |
| Immagine in evidenza | 1024×500 PNG | `feature-1024x500.png` |
| Screenshot tablet 10" | almeno 2, PNG o JPEG | `screenshot-*.png` |

Gli screenshot vanno catturati dal dispositivo con l'app in funzione:

```powershell
tools\store-assets.ps1 -Screenshots
```

---

## 6. Cose da sapere prima di premere Pubblica

**Il nome del pacchetto è definitivo.** Dopo la prima pubblicazione
`io.github.gioixxx.monitorextender` non è più modificabile: per cambiarlo servirebbe una
scheda nuova, perdendo recensioni e installazioni.

**L'app dipende da un programma esterno.** Senza il server sul PC non fa nulla. Chi la scarica
senza leggere lascerà una recensione a una stella. La descrizione lo dice tre volte, e la
schermata iniziale lo ripete: è deliberato.

**La funzione migliore è da sviluppatori.** Il cavo USB richiede il debug attivo, che la
maggior parte delle persone non ha e non attiverà. In pratica lo store distribuirà soprattutto
la modalità Wi-Fi. Valuta se valga la pena pubblicarla o se il repository GitHub basti al tuo
scopo.

**Verifica preliminare.** Play esegue una scansione automatica su alcuni dispositivi reali e
segnala il traffico in chiaro come avviso: non blocca la pubblicazione, ed è atteso. La
motivazione è in `docs/privacy.md`.

**Account sviluppatore.** Serve un account Google Play Console, con quota di iscrizione una
tantum, e — per gli account personali aperti di recente — una fase di verifica con test chiusi
prima di poter pubblicare in produzione.
