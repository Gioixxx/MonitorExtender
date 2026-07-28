package com.monitorextender.viewer

/**
 * Normalizza quello che l'utente digita nel campo indirizzo.
 * Accetta "192.168.1.62", "192.168.1.62:8080", "http://192.168.1.62:8080/" e simili.
 */
object ServerAddress {

    const val DEFAULT_PORT = 8080

    /** Restituisce "host:porta", oppure null se l'input non contiene un host utilizzabile. */
    fun normalize(input: String): String? {
        var text = input.trim()
        if (text.isEmpty()) return null

        text = text.removePrefix("http://").removePrefix("https://")
        text = text.substringBefore('/')
        if (text.isEmpty()) return null

        return if (text.contains(':')) {
            val port = text.substringAfterLast(':').toIntOrNull()
            if (port == null || port !in 1..65535) null else text
        } else {
            "$text:$DEFAULT_PORT"
        }
    }

    /**
     * Qualita' massima che il collegamento regge, misurata sullo stesso stream:
     * il cavo consegna ~37 Mbit/s, la WiFi si ferma a ~14. Chiedere al cavo i parametri
     * prudenti della rete sprecherebbe tre quarti della banda; chiedere alla rete quelli del
     * cavo la soffocherebbe, facendo arrivare un frame su tre.
     *
     * `scale` e' l'altezza richiesta: il server la limita comunque a quella dello schermo,
     * quindi 1080 significa "risoluzione nativa, qualunque sia".
     */
    private const val OVER_USB = "scale=1080&fps=30&q=85"
    private const val OVER_LAN = "scale=720&fps=20&q=60"

    fun isLoopback(hostAndPort: String): Boolean {
        val host = hostAndPort.substringBeforeLast(':')
        return host == "127.0.0.1" || host == "localhost"
    }

    /** Endpoint dello stream MJPEG, con i parametri adatti al collegamento. */
    fun streamUrl(hostAndPort: String): String {
        val profile = if (isLoopback(hostAndPort)) OVER_USB else OVER_LAN
        return "http://$hostAndPort/stream?$profile"
    }

    /** Pagina HTML a schermo intero servita dal server, per la WebView. */
    fun pageUrl(hostAndPort: String) = "http://$hostAndPort/"
}
