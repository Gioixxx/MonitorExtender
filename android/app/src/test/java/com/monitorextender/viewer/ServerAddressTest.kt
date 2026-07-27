package com.monitorextender.viewer

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
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
        assertEquals("http://192.168.1.62:8080/stream", ServerAddress.streamUrl("192.168.1.62:8080"))
        assertEquals("http://192.168.1.62:8080/", ServerAddress.pageUrl("192.168.1.62:8080"))
    }
}
