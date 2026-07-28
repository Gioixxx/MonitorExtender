package com.monitorextender.viewer

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.channels.ClosedReceiveChannelException
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.Locale
import java.util.concurrent.TimeUnit

/**
 * Manda i comandi di mouse al PC.
 *
 * I movimenti arrivano a decine al secondo, ma non parte una richiesta per ciascuno: i comandi
 * passano da una coda e chi li spedisce, a ogni giro, svuota tutto quello che si e' accumulato
 * mentre la richiesta precedente era in volo. Il raggruppamento nasce da se' sotto carico —
 * piu' il collegamento e' lento, piu' comandi viaggiano insieme — e nessun click va perso,
 * cosa che invece succederebbe tenendo solo l'ultima posizione.
 */
class InputSender(baseUrl: String, scope: CoroutineScope) {

    private val url = "$baseUrl/input"
    private val commands = Channel<String>(Channel.UNLIMITED)

    private val client = OkHttpClient.Builder()
        .connectTimeout(2, TimeUnit.SECONDS)
        .writeTimeout(2, TimeUnit.SECONDS)
        .readTimeout(2, TimeUnit.SECONDS)
        .build()

    init {
        scope.launch(Dispatchers.IO) { pump() }
    }

    /** Posizione come frazione (0..1) della larghezza e altezza dello schermo del PC. */
    fun move(x: Float, y: Float) {
        // Locale.US e' obbligatorio: con la lingua italiana il separatore decimale
        // diventerebbe la virgola e il server non riuscirebbe a leggere il numero.
        send(String.format(Locale.US, "m %.4f %.4f", x.coerceIn(0f, 1f), y.coerceIn(0f, 1f)))
    }

    fun buttonDown(button: String) = send("d $button")

    fun buttonUp(button: String) = send("u $button")

    /** 120 e' uno scatto di rotella, il segno indica la direzione. */
    fun wheel(delta: Int) = send("w $delta")

    fun click(button: String) {
        buttonDown(button)
        buttonUp(button)
    }

    fun close() {
        commands.close()
    }

    private fun send(command: String) {
        commands.trySend(command)
    }

    private suspend fun pump() = coroutineScope {
        while (isActive) {
            val batch = try {
                StringBuilder(commands.receive())
            } catch (_: ClosedReceiveChannelException) {
                return@coroutineScope
            }

            // Tutto quello gia' in coda parte nella stessa richiesta.
            while (true) {
                val next = commands.tryReceive().getOrNull() ?: break
                batch.append('\n').append(next)
            }
            post(batch.toString())
        }
    }

    private fun post(body: String) {
        try {
            client.newCall(Request.Builder().url(url).post(body.toRequestBody()).build())
                .execute()
                .close()
        } catch (_: Exception) {
            // Il controllo e' accessorio: se un comando si perde, il video continua.
        }
    }
}
