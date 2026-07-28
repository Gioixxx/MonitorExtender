package com.monitorextender.viewer

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ServerAddressTest {

    @Test
    fun `aggiunge la porta di default quando manca`() {
        assertEquals("192.168.1.62:8080", ServerAddress.normalize("192.168.1.62"))
    }

    @Test
    fun `conserva una porta esplicita`() {
        assertEquals("192.168.1.62:9000", ServerAddress.normalize("192.168.1.62:9000"))
    }

    @Test
    fun `tollera schema e percorso incollati dal browser`() {
        assertEquals("192.168.1.62:8080", ServerAddress.normalize("http://192.168.1.62:8080/stream"))
        assertEquals("192.168.1.62:8080", ServerAddress.normalize("  192.168.1.62:8080/  "))
    }

    @Test
    fun `rifiuta input vuoti o con porta assurda`() {
        assertNull(ServerAddress.normalize(""))
        assertNull(ServerAddress.normalize("   "))
        assertNull(ServerAddress.normalize("192.168.1.62:99999"))
        assertNull(ServerAddress.normalize("192.168.1.62:abc"))
    }

    @Test
    fun `costruisce gli url dei due viewer`() {
        assertEquals(
            "http://192.168.1.62:8080/stream?scale=720&fps=20&q=60",
            ServerAddress.streamUrl("192.168.1.62:8080"),
        )
        assertEquals("http://192.168.1.62:8080/", ServerAddress.pageUrl("192.168.1.62:8080"))
    }

    @Test
    fun `chiede piu' qualita' quando passa dal cavo`() {
        val overUsb = ServerAddress.streamUrl("127.0.0.1:8080")
        assertTrue("via cavo servono i parametri spinti: $overUsb", overUsb.contains("q=85"))
        assertTrue(overUsb.contains("fps=30"))

        val overLan = ServerAddress.streamUrl("192.168.1.62:8080")
        assertTrue("via rete servono i parametri prudenti: $overLan", overLan.contains("q=60"))
    }

    @Test
    fun `riconosce il collegamento via cavo`() {
        assertTrue(ServerAddress.isLoopback("127.0.0.1:8080"))
        assertTrue(ServerAddress.isLoopback("localhost:8080"))
        assertFalse(ServerAddress.isLoopback("192.168.1.62:8080"))
    }
}
