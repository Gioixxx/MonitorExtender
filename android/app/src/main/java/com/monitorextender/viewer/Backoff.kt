package com.monitorextender.viewer

import kotlin.math.min
import kotlin.random.Random

/**
 * Attesa crescente tra un tentativo di riconnessione e il successivo.
 *
 * Ritentare subito e per sempre e' il modo piu' veloce per svuotare la batteria quando il PC
 * e' spento: l'attesa raddoppia a ogni fallimento fino a un tetto. Il jitter serve quando piu'
 * dispositivi guardano lo stesso server e la rete torna su: senza, ripartirebbero tutti nello
 * stesso istante.
 */
class Backoff(
    private val firstDelayMs: Long = 500,
    private val maxDelayMs: Long = 10_000,
    private val jitter: Double = 0.2,
    private val random: Random = Random.Default,
) {
    private var attempt = 0

    /** Ritardo prima del prossimo tentativo, in millisecondi. */
    fun nextDelayMs(): Long {
        val exponential = min(firstDelayMs shl attempt, maxDelayMs)
        if (firstDelayMs shl attempt < maxDelayMs) attempt++
        val spread = (exponential * jitter).toLong()
        return if (spread <= 0) exponential
        else exponential - spread + random.nextLong(2 * spread + 1)
    }

    /** Da chiamare quando la connessione riesce: il prossimo problema riparte da capo. */
    fun reset() {
        attempt = 0
    }
}
