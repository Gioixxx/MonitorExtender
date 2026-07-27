package com.monitorextender.viewer

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.SocketTimeoutException

/**
 * Cerca il server sulla LAN mandando una sonda UDP in broadcast, cosi' non serve digitare l'IP.
 *
 * L'indirizzo del server si prende dal **mittente** della risposta, non dal JSON: un PC con piu'
 * schede di rete (WiFi, Ethernet, adattatori virtuali di WSL o Hyper-V) non sa quale dei suoi
 * indirizzi sia raggiungibile dal telefono, mentre il mittente del pacchetto e' per costruzione
 * quello giusto.
 */
object Discovery {

    private const val PORT = 8079
    private const val PROBE = "MONITOREXTENDER?"

    data class Server(val address: String, val name: String, val port: Int) {
        val hostAndPort get() = "$address:$port"
    }

    suspend fun findFirst(timeoutMs: Int = 2000): Server? = withContext(Dispatchers.IO) {
        DatagramSocket().use { socket ->
            socket.broadcast = true
            socket.soTimeout = timeoutMs

            val probe = PROBE.toByteArray()
            socket.send(DatagramPacket(probe, probe.size, InetAddress.getByName("255.255.255.255"), PORT))

            val buffer = ByteArray(512)
            val deadline = System.currentTimeMillis() + timeoutMs
            while (System.currentTimeMillis() < deadline) {
                val reply = DatagramPacket(buffer, buffer.size)
                try {
                    socket.receive(reply)
                } catch (_: SocketTimeoutException) {
                    return@withContext null
                }

                val body = String(reply.data, reply.offset, reply.length)
                if (!body.contains("monitorextender")) continue

                return@withContext Server(
                    address = reply.address.hostAddress ?: continue,
                    name = body.stringField("name") ?: "PC",
                    port = body.intField("port") ?: ServerAddress.DEFAULT_PORT,
                )
            }
            null
        }
    }

    // La risposta e' un JSON di tre campi: due espressioni regolari costano meno di una
    // dipendenza da un parser completo.
    private fun String.stringField(name: String): String? =
        Regex("\"$name\"\\s*:\\s*\"([^\"]*)\"").find(this)?.groupValues?.get(1)

    private fun String.intField(name: String): Int? =
        Regex("\"$name\"\\s*:\\s*(\\d+)").find(this)?.groupValues?.get(1)?.toIntOrNull()
}
