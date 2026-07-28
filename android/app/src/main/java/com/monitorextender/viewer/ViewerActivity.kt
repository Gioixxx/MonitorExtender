package com.monitorextender.viewer

import android.os.Bundle
import android.view.View
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import androidx.lifecycle.lifecycleScope
import com.monitorextender.viewer.databinding.ActivityViewerBinding
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.util.concurrent.TimeUnit

/** Viewer nativo: legge lo stream MJPEG e lo disegna su una SurfaceView. */
class ViewerActivity : ComponentActivity() {

    private lateinit var binding: ActivityViewerBinding
    private lateinit var url: String
    private var streaming: Job? = null
    private var overlayVisible = false
    private var autoRevealed = false
    private var controllingPc = false
    private var input: InputSender? = null

    private val client = OkHttpClient.Builder()
        .connectTimeout(5, TimeUnit.SECONDS)
        // Lo stream non finisce mai: un timeout complessivo lo troncherebbe a meta'.
        // Conta solo la pausa massima tra due frame.
        .callTimeout(0, TimeUnit.SECONDS)
        .readTimeout(10, TimeUnit.SECONDS)
        .retryOnConnectionFailure(false)
        .build()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityViewerBinding.inflate(layoutInflater)
        setContentView(binding.root)

        val extra = intent.getStringExtra(MainActivity.EXTRA_URL)
        if (extra == null) {
            finish()
            return
        }
        url = extra

        // Guardare un monitor senza toccare lo schermo lo farebbe spegnere dopo un minuto.
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        goImmersive()

        setUpTouch()
    }

    /**
     * Il controllo del PC si attiva solo via cavo: il server accetta i comandi di mouse
     * unicamente da loopback, quindi sulla WiFi non varrebbe nemmeno la pena mandarli. Li' il
     * tocco resta quello che era, cioe' mostra e nasconde le statistiche.
     */
    private fun setUpTouch() {
        val base = url.substringBefore("/stream")
        val host = base.toHttpUrlOrNull()?.host

        if (host != null && (host == "127.0.0.1" || host == "localhost")) {
            val sender = InputSender(base, lifecycleScope).also { input = it }
            binding.video.setOnTouchListener(TouchController(sender) { toggleOverlay() })
            controllingPc = true
        } else {
            binding.root.setOnClickListener { toggleOverlay() }
        }
    }

    /**
     * Con il controllo del PC attivo ogni tocco e' un click, quindi le statistiche si aprono
     * con tre dita — un gesto che nessuno indovina. Mostrarle qualche secondo all'apertura e'
     * l'unico modo di far sapere che esistono e come richiamarle.
     */
    private fun revealOverlayHint() {
        if (overlayVisible) return

        overlayVisible = true
        autoRevealed = true
        binding.stats.text = getString(
            if (controllingPc) R.string.overlay_hint_touch else R.string.overlay_hint_tap
        )
        binding.stats.visibility = View.VISIBLE

        binding.stats.postDelayed({
            // Se nel frattempo l'hai aperto tu, non te lo chiudo in faccia.
            if (autoRevealed) {
                autoRevealed = false
                overlayVisible = false
                binding.stats.visibility = View.GONE
            }
        }, HINT_MS)
    }

    override fun onStart() {
        super.onStart()
        streaming = lifecycleScope.launch(Dispatchers.IO) { streamWithRetry() }
    }

    override fun onStop() {
        super.onStop()
        // Fuori schermo non si consuma banda ne' batteria: il server smette di catturare
        // appena l'ultimo client si scollega.
        streaming?.cancel()
        streaming = null
    }

    override fun onDestroy() {
        input?.close()
        input = null
        super.onDestroy()
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        // Le barre di sistema tornano da sole dopo uno swipe: qui si rimandano via.
        if (hasFocus) goImmersive()
    }

    private fun goImmersive() {
        WindowCompat.setDecorFitsSystemWindows(window, false)
        WindowInsetsControllerCompat(window, binding.root).apply {
            hide(WindowInsetsCompat.Type.systemBars())
            systemBarsBehavior = WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        }
    }

    private fun toggleOverlay() {
        autoRevealed = false
        overlayVisible = !overlayVisible
        binding.stats.visibility = if (overlayVisible) View.VISIBLE else View.GONE
    }

    /**
     * Si riconnette da sola: il PC che va in standby, la WiFi che cade o il server riavviato
     * non devono costringere a rientrare nell'app.
     */
    private suspend fun streamWithRetry() = coroutineScope {
        val backoff = Backoff()
        while (isActive) {
            try {
                stream(backoff)
            } catch (e: CancellationException) {
                throw e // uscita normale da onStop
            } catch (e: Exception) {
                showStatus(getString(R.string.stream_error, e.message ?: e.javaClass.simpleName))
            }
            if (!isActive) break

            val wait = backoff.nextDelayMs()
            showStatus(getString(R.string.reconnecting, wait / 1000.0))
            delay(wait)
        }
    }

    private suspend fun stream(backoff: Backoff) = coroutineScope {
        client.newCall(Request.Builder().url(url).build()).execute().use { response ->
            if (!response.isSuccessful) throw java.io.IOException("HTTP ${response.code}")

            backoff.reset()
            showStatus(null)
            withContext(Dispatchers.Main) { revealOverlayHint() }

            val mjpeg = MjpegStream(checkNotNull(response.body).byteStream())
            val decoder = JpegDecoder()
            val stats = StreamStats()

            while (isActive) {
                val frame = mjpeg.nextFrame()
                val bitmap = decoder.decode(frame) ?: continue
                binding.video.drawFrame(bitmap)
                // Il suggerimento resta finche' non scade: sovrascriverlo con i numeri lo
                // renderebbe illeggibile prima ancora di essere stato letto.
                if (stats.onFrame(frame.length) && overlayVisible && !autoRevealed) {
                    val text = stats.format()
                    withContext(Dispatchers.Main) { binding.stats.text = text }
                }
            }
        }
    }

    private suspend fun showStatus(message: String?) = withContext(Dispatchers.Main) {
        binding.status.text = message ?: ""
        binding.status.visibility = if (message == null) View.GONE else View.VISIBLE
    }

    private companion object {
        const val HINT_MS = 5000L
    }
}
