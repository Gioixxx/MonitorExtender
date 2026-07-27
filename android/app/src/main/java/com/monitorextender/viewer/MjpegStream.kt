package com.monitorextender.viewer

import java.io.BufferedInputStream
import java.io.EOFException
import java.io.IOException
import java.io.InputStream

/**
 * Parser dello stream `multipart/x-mixed-replace` prodotto dal server .NET.
 *
 * Ogni parte ha questa forma esatta:
 * ```
 * --frame CRLF
 * Content-Type: image/jpeg CRLF
 * Content-Length: N CRLF
 * CRLF
 * <N byte JPEG>
 * CRLF
 * ```
 *
 * Due trappole, entrambe gestite qui:
 * - `InputStream.read` restituisce quasi sempre **meno** byte di quelli richiesti: un frame da
 *   60 KB arriva spezzato in una decina di letture. Chi assume che arrivi tutto insieme ottiene
 *   immagini troncate a intermittenza.
 * - Il buffer di destinazione viene riusato tra un frame e l'altro. A 20 fps, allocarne uno
 *   nuovo ogni volta significa ~1,2 MB/s di spazzatura e pause del garbage collector visibili
 *   come scatti nel video.
 */
class MjpegStream(source: InputStream) {

    /** La lettura degli header procede byte per byte: senza buffer sarebbe una syscall a carattere. */
    private val input = BufferedInputStream(source, 64 * 1024)

    private var buffer = ByteArray(256 * 1024)

    /** Il frame punta al buffer interno, valido solo fino alla chiamata successiva. */
    class Frame(val data: ByteArray, val length: Int)

    fun nextFrame(): Frame {
        // Il CRLF che chiude la parte precedente si presenta qui come riga vuota.
        var line = readLine()
        while (line.isEmpty()) line = readLine()
        if (!line.startsWith("--")) throw IOException("atteso un boundary, ricevuto: \"$line\"")

        var contentLength = -1
        while (true) {
            val header = readLine()
            if (header.isEmpty()) break // riga vuota = fine degli header di parte
            val colon = header.indexOf(':')
            if (colon > 0 && header.substring(0, colon).trim().equals("Content-Length", ignoreCase = true)) {
                contentLength = header.substring(colon + 1).trim().toIntOrNull() ?: -1
            }
        }
        if (contentLength <= 0) throw IOException("parte senza Content-Length valido")
        if (contentLength > MAX_FRAME_BYTES) throw IOException("frame da $contentLength byte: fuori scala")

        if (buffer.size < contentLength) buffer = ByteArray(contentLength)
        readFully(buffer, contentLength)
        return Frame(buffer, contentLength)
    }

    /** Legge una riga ASCII terminata da CRLF, senza sconfinare nel corpo della parte. */
    private fun readLine(): String {
        val line = StringBuilder(64)
        while (true) {
            val b = input.read()
            if (b < 0) throw EOFException("stream chiuso durante gli header")
            if (b == '\n'.code) return line.toString().trimEnd('\r')
            line.append(b.toChar())
            if (line.length > MAX_HEADER_CHARS) throw IOException("header di parte troppo lungo")
        }
    }

    /** Insiste finche' non sono arrivati tutti i byte richiesti: le letture parziali sono la norma. */
    private fun readFully(dest: ByteArray, length: Int) {
        var read = 0
        while (read < length) {
            val n = input.read(dest, read, length - read)
            if (n < 0) throw EOFException("stream chiuso dopo $read byte su $length")
            read += n
        }
    }

    private companion object {
        const val MAX_FRAME_BYTES = 16 * 1024 * 1024
        const val MAX_HEADER_CHARS = 1024
    }
}
