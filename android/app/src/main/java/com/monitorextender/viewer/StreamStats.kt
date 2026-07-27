package com.monitorextender.viewer

import android.os.SystemClock

/**
 * Conta fps e latenza reali lato client.
 *
 * "Latenza" qui e' il tempo tra due frame consumati, non il ritardo rispetto a quello che
 * succede sullo schermo del PC — quello non e' misurabile senza un orologio comune. Serve
 * comunque a capire se il collo di bottiglia e' la rete (fps bassi, intervalli irregolari)
 * o la decodifica (intervalli regolari ma lunghi).
 */
class StreamStats(private val windowMs: Long = 1000) {

    private var windowStart = 0L
    private var framesInWindow = 0
    private var bytesInWindow = 0L
    private var lastFrameAt = 0L
    private var worstGapMs = 0L

    var fps = 0.0
        private set
    var averageGapMs = 0L
        private set
    var peakGapMs = 0L
        private set
    var kilobytesPerFrame = 0L
        private set

    /** Registra un frame. Restituisce true quando i valori pubblici sono stati aggiornati. */
    fun onFrame(sizeBytes: Int): Boolean {
        val now = SystemClock.elapsedRealtime()
        if (windowStart == 0L) {
            windowStart = now
            lastFrameAt = now
            return false
        }

        val gap = now - lastFrameAt
        lastFrameAt = now
        if (gap > worstGapMs) worstGapMs = gap

        framesInWindow++
        bytesInWindow += sizeBytes

        val elapsed = now - windowStart
        if (elapsed < windowMs) return false

        fps = framesInWindow * 1000.0 / elapsed
        averageGapMs = if (framesInWindow > 0) elapsed / framesInWindow else 0
        peakGapMs = worstGapMs
        kilobytesPerFrame = if (framesInWindow > 0) bytesInWindow / framesInWindow / 1024 else 0

        windowStart = now
        framesInWindow = 0
        bytesInWindow = 0
        worstGapMs = 0
        return true
    }

    fun format(): String =
        "%.1f fps · %d ms medi · picco %d ms · %d KB/frame".format(fps, averageGapMs, peakGapMs, kilobytesPerFrame)
}
