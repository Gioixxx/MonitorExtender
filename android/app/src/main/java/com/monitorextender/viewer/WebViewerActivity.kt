package com.monitorextender.viewer

import android.annotation.SuppressLint
import android.graphics.Color
import android.os.Bundle
import android.view.WindowManager
import android.webkit.WebView
import androidx.activity.ComponentActivity
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat

/**
 * Viewer "pigro": una WebView sulla pagina servita dal server.
 * Non decodifica niente a mano — e' il browser di sistema a occuparsi del multipart — quindi
 * serve da riferimento: se questa mostra l'immagine e il viewer nativo no, il problema e' nel
 * parser, non nella rete.
 */
class WebViewerActivity : ComponentActivity() {

    private lateinit var webView: WebView

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val url = intent.getStringExtra(MainActivity.EXTRA_URL)
        if (url == null) {
            finish()
            return
        }

        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        WindowCompat.setDecorFitsSystemWindows(window, false)

        webView = WebView(this).apply {
            setBackgroundColor(Color.BLACK)
            settings.javaScriptEnabled = true
            settings.loadWithOverviewMode = true
            settings.useWideViewPort = true
            settings.cacheMode = android.webkit.WebSettings.LOAD_NO_CACHE
        }
        setContentView(webView)
        WindowInsetsControllerCompat(window, webView).apply {
            hide(WindowInsetsCompat.Type.systemBars())
            systemBarsBehavior = WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        }
        webView.loadUrl(url)
    }

    override fun onDestroy() {
        // Senza questo la WebView tiene aperta la connessione e il server continua a catturare.
        webView.loadUrl("about:blank")
        webView.destroy()
        super.onDestroy()
    }
}
