package com.monitorextender.viewer

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.random.Random

class BackoffTest {

    /** Senza jitter la progressione e' deterministica e si puo' asserire esattamente. */
    private fun deterministic() = Backoff(firstDelayMs = 500, maxDelayMs = 10_000, jitter = 0.0)

    @Test
    fun `raddoppia a ogni tentativo fallito`() {
        val backoff = deterministic()
        assertEquals(500, backoff.nextDelayMs())
        assertEquals(1000, backoff.nextDelayMs())
        assertEquals(2000, backoff.nextDelayMs())
        assertEquals(4000, backoff.nextDelayMs())
        assertEquals(8000, backoff.nextDelayMs())
    }

    @Test
    fun `si ferma al tetto invece di crescere all'infinito`() {
        val backoff = deterministic()
        repeat(20) { backoff.nextDelayMs() }
        assertEquals(10_000, backoff.nextDelayMs())
    }

    @Test
    fun `riparte da capo dopo una connessione riuscita`() {
        val backoff = deterministic()
        repeat(5) { backoff.nextDelayMs() }
        backoff.reset()
        assertEquals(500, backoff.nextDelayMs())
    }

    @Test
    fun `il jitter resta dentro la percentuale dichiarata`() {
        // Con jitter 20% il primo ritardo deve cadere in [400, 600].
        val backoff = Backoff(firstDelayMs = 500, maxDelayMs = 10_000, jitter = 0.2, random = Random(1))
        repeat(200) {
            val delay = Backoff(500, 10_000, 0.2, Random(it)).nextDelayMs()
            assertTrue("ritardo fuori scala: $delay", delay in 400..600)
        }
        assertTrue(backoff.nextDelayMs() in 400..600)
    }
}
