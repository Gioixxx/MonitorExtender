package com.monitorextender.viewer

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.view.View
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.core.content.edit
import androidx.lifecycle.lifecycleScope
import com.monitorextender.viewer.databinding.ActivityMainBinding
import kotlinx.coroutines.async
import kotlinx.coroutines.launch

/**
 * Schermata di partenza: le due strade per collegarsi, ciascuna con il proprio stato.
 *
 * Le vie sono mostrate separate e non nascoste dietro un solo pulsante "cerca" che prova il
 * cavo e ripiega sulla rete in silenzio: chi usa l'app non avrebbe modo di sapere che il cavo
 * esiste, ne' perche' non ha funzionato. Quando il cavo non e' disponibile, la scheda dice
 * esattamente quale comando eseguire sul PC.
 */
class MainActivity : ComponentActivity() {

    private lateinit var binding: ActivityMainBinding

    private val prefs by lazy { getSharedPreferences(PREFS, Context.MODE_PRIVATE) }

    private var usbServer: Discovery.Server? = null
    private var lanServer: Discovery.Server? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        limitContentWidth()
        binding.address.setText(prefs.getString(KEY_ADDRESS, ""))

        binding.usbCard.setOnClickListener {
            usbServer?.let { connect(it.hostAndPort, ViewerActivity::class.java) }
        }
        binding.wifiCard.setOnClickListener {
            lanServer?.let { connect(it.hostAndPort, ViewerActivity::class.java) }
        }
        binding.recheck.setOnClickListener { probe() }

        binding.connectNative.setOnClickListener { connectManual(ViewerActivity::class.java) }
        binding.connectWebView.setOnClickListener { connectManual(WebViewerActivity::class.java) }
    }

    override fun onResume() {
        super.onResume()
        // Tornando indietro dal viewer lo stato puo' essere cambiato: il cavo puo' essere
        // stato collegato nel frattempo, o il server riavviato.
        probe()
    }

    /**
     * Su un tablet in orizzontale lo schermo e' largo oltre mille dp: senza un limite le righe
     * degli ingressi si stirerebbero da un bordo all'altro e diventerebbero illeggibili.
     */
    private fun limitContentWidth() {
        val maxWidth = (MAX_CONTENT_DP * resources.displayMetrics.density).toInt()
        binding.content.post {
            val available = (binding.content.parent as View).width
            if (available > maxWidth) {
                binding.content.layoutParams = binding.content.layoutParams.apply {
                    width = maxWidth
                }
            }
        }
    }

    private fun probe() {
        setChecking()
        lifecycleScope.launch {
            // Le due ricerche partono insieme: quella sulla rete impiega fino a due secondi
            // e non deve far aspettare la risposta del cavo, che e' immediata.
            val usb = async { Discovery.findUsb() }
            val lan = async { Discovery.findOnLan() }

            usbServer = usb.await()
            lanServer = lan.await()

            showUsb(usbServer)
            showLan(lanServer)
        }
    }

    private fun setChecking() {
        binding.recheck.isEnabled = false
        binding.usbState.text = getString(R.string.checking)
        binding.wifiState.text = getString(R.string.checking)
        binding.usbStatus.text = ""
        binding.wifiStatus.text = ""
        binding.usbCommand.visibility = View.GONE
        setCardState(binding.usbCard, binding.usbState, false)
        setCardState(binding.wifiCard, binding.wifiState, false)
    }

    private fun showUsb(server: Discovery.Server?) {
        binding.recheck.isEnabled = true
        if (server != null) {
            binding.usbState.text = getString(R.string.usb_ready)
            binding.usbStatus.text = getString(R.string.usb_ready_detail, server.name)
            binding.usbCommand.visibility = View.GONE
        } else {
            binding.usbState.text = getString(R.string.usb_missing)
            binding.usbStatus.text = getString(R.string.usb_missing_detail)
            // Il comando compare solo quando serve davvero: e' l'unica cosa da fare per attivare
            // il cavo, e tenerlo sempre visibile lo trasformerebbe in rumore.
            binding.usbCommand.visibility = View.VISIBLE
        }
        setCardState(binding.usbCard, binding.usbState, server != null)
    }

    private fun showLan(server: Discovery.Server?) {
        if (server != null) {
            binding.wifiState.text = getString(R.string.wifi_ready)
            binding.wifiStatus.text =
                getString(R.string.wifi_ready_detail, server.name, server.hostAndPort)
        } else {
            binding.wifiState.text = getString(R.string.wifi_missing)
            binding.wifiStatus.text = getString(R.string.wifi_missing_detail)
        }
        setCardState(binding.wifiCard, binding.wifiState, server != null)
    }

    /**
     * Un ingresso disponibile accende la barra ambra ed e' l'unico punto colorato della
     * schermata; uno non disponibile resta grigio ma perfettamente leggibile, perche' e'
     * proprio li' che si trova la spiegazione di cosa fare.
     */
    private fun setCardState(card: View, state: TextView, available: Boolean) {
        card.isEnabled = available
        card.isClickable = available
        card.setBackgroundResource(
            if (available) R.drawable.input_available else R.drawable.input_unavailable
        )
        state.setTextColor(getColor(if (available) R.color.signal else R.color.ink_faint))
    }

    private fun connectManual(target: Class<*>) {
        val address = ServerAddress.normalize(binding.address.text.toString())
        if (address == null) {
            binding.address.error = getString(R.string.address_invalid)
            return
        }
        connect(address, target)
    }

    private fun connect(hostAndPort: String, target: Class<*>) {
        prefs.edit { putString(KEY_ADDRESS, hostAndPort) }
        val url = if (target == WebViewerActivity::class.java) {
            ServerAddress.pageUrl(hostAndPort)
        } else {
            ServerAddress.streamUrl(hostAndPort)
        }
        startActivity(Intent(this, target).putExtra(EXTRA_URL, url))
    }

    companion object {
        const val EXTRA_URL = "url"
        private const val PREFS = "monitorextender"
        private const val KEY_ADDRESS = "address"
        private const val MAX_CONTENT_DP = 560
    }
}
