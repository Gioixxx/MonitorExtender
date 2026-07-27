package com.monitorextender.viewer

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.core.content.edit
import androidx.lifecycle.lifecycleScope
import com.monitorextender.viewer.databinding.ActivityMainBinding
import kotlinx.coroutines.launch

/** Schermata di partenza: indirizzo del PC e scelta del viewer. */
class MainActivity : ComponentActivity() {

    private lateinit var binding: ActivityMainBinding

    private val prefs by lazy { getSharedPreferences(PREFS, Context.MODE_PRIVATE) }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        binding.address.setText(prefs.getString(KEY_ADDRESS, ""))

        binding.connectNative.setOnClickListener {
            open(ViewerActivity::class.java) { ServerAddress.streamUrl(it) }
        }
        binding.connectWebView.setOnClickListener {
            open(WebViewerActivity::class.java) { ServerAddress.pageUrl(it) }
        }
        binding.search.setOnClickListener { search() }
    }

    /** Chiede alla rete chi sta servendo lo stream, invece di farsi digitare l'IP. */
    private fun search() {
        binding.search.isEnabled = false
        binding.search.text = getString(R.string.searching)

        lifecycleScope.launch {
            val server = runCatching { Discovery.findFirst() }.getOrNull()

            binding.search.isEnabled = true
            binding.search.text = getString(R.string.search_pc)

            if (server == null) {
                Toast.makeText(this@MainActivity, R.string.not_found, Toast.LENGTH_LONG).show()
                return@launch
            }
            binding.address.setText(server.hostAndPort)
            val message = if (server.viaUsb) {
                getString(R.string.found_pc_usb, server.name)
            } else {
                getString(R.string.found_pc, server.name, server.hostAndPort)
            }
            Toast.makeText(this@MainActivity, message, Toast.LENGTH_SHORT).show()
        }
    }

    private fun open(target: Class<*>, url: (String) -> String) {
        val address = ServerAddress.normalize(binding.address.text.toString())
        if (address == null) {
            binding.address.error = getString(R.string.address_invalid)
            return
        }
        prefs.edit { putString(KEY_ADDRESS, address) }
        startActivity(Intent(this, target).putExtra(EXTRA_URL, url(address)))
    }

    companion object {
        const val EXTRA_URL = "url"
        private const val PREFS = "monitorextender"
        private const val KEY_ADDRESS = "address"
    }
}
