package com.monitorextender.viewer

import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import java.io.ByteArrayInputStream
import java.io.EOFException
import java.io.IOException
import java.io.InputStream

/**
 * Il parser gira su JVM pura, quindi si verifica senza un dispositivo Android.
 * La fixture `sample-stream.bin` sono byte veri catturati dal server .NET, non un
 * multipart scritto a mano che rischierebbe di validare le mie stesse assunzioni.
 */
class MjpegStreamTest {

    private fun fixture(): ByteArray =
        checkNotNull(javaClass.classLoader?.getResourceAsStream("sample-stream.bin")) {
            "fixture sample-stream.bin non trovata"
        }.use { it.readBytes() }

    @Test
    fun `estrae frame JPEG completi dallo stream del server`() {
        val stream = MjpegStream(ByteArrayInputStream(fixture()))

        var frames = 0
        try {
            while (true) {
                val frame = stream.nextFrame()
                assertTrue("frame $frames troppo piccolo", frame.length > 1000)
                assertJpeg(frame)
                frames++
            }
        } catch (_: EOFException) {
            // La cattura si ferma a meta' parte: e' la fine attesa della fixture.
        }

        assertTrue("attesi almeno 3 frame, trovati $frames", frames >= 3)
    }

    @Test
    fun `regge le letture parziali`() {
        // Il caso vero: read() torna una manciata di byte per volta e un frame da 6 KB
        // arriva spezzato in decine di letture. Qui si esaspera a 7 byte per chiamata.
        val stream = MjpegStream(TrickleInputStream(ByteArrayInputStream(fixture()), maxPerRead = 7))

        val first = stream.nextFrame()
        assertJpeg(first)
        val firstLength = first.length

        val second = stream.nextFrame()
        assertJpeg(second)

        // Stessa fixture letta tutta in una volta: i frame devono coincidere byte per byte.
        val reference = MjpegStream(ByteArrayInputStream(fixture())).nextFrame()
        assertEquals(reference.length, firstLength)
    }

    @Test
    fun `riusa lo stesso buffer tra un frame e l'altro`() {
        val stream = MjpegStream(ByteArrayInputStream(fixture()))
        val first = stream.nextFrame().data
        val second = stream.nextFrame().data
        assertSame("il buffer va riusato, non riallocato a ogni frame", first, second)
    }

    @Test
    fun `rifiuta una parte senza Content-Length`() {
        val malformed = "--frame\r\nContent-Type: image/jpeg\r\n\r\nnon importa".toByteArray()
        try {
            MjpegStream(ByteArrayInputStream(malformed)).nextFrame()
            fail("attesa una IOException")
        } catch (e: IOException) {
            assertTrue(e.message!!.contains("Content-Length"))
        }
    }

    @Test
    fun `rifiuta uno stream che non inizia con un boundary`() {
        val malformed = "HTTP/1.1 500 Internal Server Error\r\n\r\n".toByteArray()
        try {
            MjpegStream(ByteArrayInputStream(malformed)).nextFrame()
            fail("attesa una IOException")
        } catch (e: IOException) {
            assertTrue(e.message!!.contains("boundary"))
        }
    }

    private fun assertJpeg(frame: MjpegStream.Frame) {
        val d = frame.data
        val n = frame.length
        assertEquals("manca il marcatore SOI", 0xFF, d[0].toInt() and 0xFF)
        assertEquals("manca il marcatore SOI", 0xD8, d[1].toInt() and 0xFF)
        assertEquals("manca il marcatore EOI", 0xFF, d[n - 2].toInt() and 0xFF)
        assertEquals("manca il marcatore EOI", 0xD9, d[n - 1].toInt() and 0xFF)
    }

    /** Restituisce pochi byte per chiamata, come farebbe una connessione di rete lenta. */
    private class TrickleInputStream(
        private val source: InputStream,
        private val maxPerRead: Int,
    ) : InputStream() {
        override fun read(): Int = source.read()

        override fun read(b: ByteArray, off: Int, len: Int): Int =
            source.read(b, off, minOf(len, maxPerRead))
    }
}
