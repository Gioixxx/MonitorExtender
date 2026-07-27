package com.monitorextender.viewer

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.SocketTimeoutException
import java.util.concurrent.TimeUnit

/**
 * Trova il server senza far digitare l'indirizzo.
 *
 * Prova due strade, in ordine di velocita' del collegamento:
 *
 * 1. **Cavo USB.** Se sul PC e' attivo `adb reverse` (vedi `tools/usb-link.ps1`), il server
 *    risponde su `127.0.0.1` del dispositivo e i dati viaggiano sul cavo. Misurato sullo
 *    stesso stream: 36,8 Mbit/s contro i 13,8 della WiFi, cioe' tutti i frame consegnati
 *    invece di un terzo. Se il cavo c'e', tanto vale usarlo.
 * 2. **Broadcast UDP sulla LAN.** L'indirizzo si prende dal **mittente** della risposta, non
 *    dal JSON: un PC con piu' schede di rete (WiFi, Ethernet, adattatori virtuali di WSL o
 *    Hyper-V) non sa quale dei suoi indirizzi sia raggiungibile dal tablet, mentre il mittente
 *    del pacchetto e' per costruzione quello giusto.
 */
object Discovery {

    private const val DISCOVERY_PORT = 8079
    private const val PROBE = "MONITOREXTENDER?"
    private const val USB_HOST = "127.0.0.1"

    data class Server(
        val address: String,
        val name: String,
        val port: Int,
        val viaUsb: Boolean = false,
    ) {
        val hostAndPort get() = "$address:$port"
    }

    suspend fun findFirst(timeoutMs: Int = 2000): Server? = withContext(Dispatchers.IO) {
        probeUsb() ?: probeBroadcast(timeoutMs)
    }

    /**
     * Interroga `/info` su localhost: risponde solo se `adb reverse` sta inoltrando la porta
     * lungo il cavo. I timeout sono brevi perche' senza inoltro la connessione viene rifiutata
     * subito, e non deve rallentare la ricerca sulla rete.
     */
    private fun probeUsb(): Server? {
        val client = OkHttpClient.Builder()
            .connectTimeout(400, TimeUnit.MILLISECONDS)
            .readTimeout(400, TimeUnit.MILLISECONDS)
            .callTimeout(1, TimeUnit.SECONDS)
            .retryOnConnectionFailure(false)
            .build()

        val port = ServerAddress.DEFAULT_PORT
        return try {
            client.newCall(Request.Builder().url("http://$USB_HOST:$port/info").build()).execute()
                .use { response ->
                    if (!response.isSuccessful) return null
                    val body = response.body.string()
                    Server(
                        address = USB_HOST,
                        name = body.stringField("name") ?: "PC",
                        // La porta e' quella inoltrata in locale, non quella dichiarata dal
                        // server: sono la stessa solo perche' adb reverse le mappa 1:1.
                        port = port,
                        viaUsb = true,
                    )
                }
        } catch (_: Exception) {
            null // nessun inoltro attivo: si passa alla ricerca sulla rete
        }
    }

    private fun probeBroadcast(timeoutMs: Int): Server? =
        DatagramSocket().use { socket ->
            socket.broadcast = true
            socket.soTimeout = timeoutMs

            val probe = PROBE.toByteArray()
            socket.send(
                DatagramPacket(probe, probe.size, InetAddress.getByName("255.255.255.255"), DISCOVERY_PORT)
            )

            val buffer = ByteArray(512)
            val deadline = System.currentTimeMillis() + timeoutMs
            while (System.currentTimeMillis() < deadline) {
                val reply = DatagramPacket(buffer, buffer.size)
                try {
                    socket.receive(reply)
                } catch (_: SocketTimeoutException) {
                    return null
                }

                val body = String(reply.data, reply.offset, reply.length)
                if (!body.contains("monitorextender")) continue

                return Server(
                    address = reply.address.hostAddress ?: continue,
                    name = body.stringField("name") ?: "PC",
                    port = body.intField("port") ?: ServerAddress.DEFAULT_PORT,
                )
            }
            null
        }

    // La risposta e' un JSON di pochi campi: due espressioni regolari costano meno di una
    // dipendenza da un parser completo.
    private fun String.stringField(name: String): String? =
        Regex("\"$name\"\\s*:\\s*\"([^\"]*)\"").find(this)?.groupValues?.get(1)

    private fun String.intField(name: String): Int? =
        Regex("\"$name\"\\s*:\\s*(\\d+)").find(this)?.groupValues?.get(1)?.toIntOrNull()
}
