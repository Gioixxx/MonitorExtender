package com.monitorextender.viewer

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.view.View
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
        binding.usbStatus.text = getString(R.string.checking)
        binding.wifiStatus.text = getString(R.string.checking)
        setCardEnabled(binding.usbCard, false)
        setCardEnabled(binding.wifiCard, false)
    }

    private fun showUsb(server: Discovery.Server?) {
        binding.recheck.isEnabled = true
        if (server != null) {
            binding.usbStatus.text = getString(R.string.usb_ready, server.name)
            setCardEnabled(binding.usbCard, true)
        } else {
            binding.usbStatus.text = getString(R.string.usb_missing)
            setCardEnabled(binding.usbCard, false)
        }
    }

    private fun showLan(server: Discovery.Server?) {
        if (server != null) {
            binding.wifiStatus.text = getString(R.string.wifi_ready, server.name, server.hostAndPort)
            setCardEnabled(binding.wifiCard, true)
        } else {
            binding.wifiStatus.text = getString(R.string.wifi_missing)
            setCardEnabled(binding.wifiCard, false)
        }
    }

    /** Una scheda inattiva resta leggibile ma smorzata: spiega ancora cosa fare per attivarla. */
    private fun setCardEnabled(card: View, enabled: Boolean) {
        card.isEnabled = enabled
        card.isClickable = enabled
        card.alpha = if (enabled) 1f else 0.55f
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
    }
}
