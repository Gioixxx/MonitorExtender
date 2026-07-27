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

    /** Endpoint dello stream MJPEG, per il viewer nativo. */
    fun streamUrl(hostAndPort: String) = "http://$hostAndPort/stream"

    /** Pagina HTML a schermo intero servita dal server, per la WebView. */
    fun pageUrl(hostAndPort: String) = "http://$hostAndPort/"
}
