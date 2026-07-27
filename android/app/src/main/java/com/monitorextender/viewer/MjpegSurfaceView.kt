package com.monitorextender.viewer

import android.content.Context
import android.graphics.Bitmap
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Rect
import android.util.AttributeSet
import android.view.SurfaceHolder
import android.view.SurfaceView
import kotlin.math.roundToInt

/**
 * Superficie di disegno per lo stream.
 *
 * SurfaceView invece di ImageView perche' si disegna direttamente sul suo buffer, senza passare
 * dal ciclo di layout e invalidazione della gerarchia di view. `lockCanvas` e' chiamabile da
 * qualsiasi thread, quindi il frame viene disegnato dallo stesso thread che lo ha decodificato:
 * nessun rimbalzo sul main thread per ogni immagine.
 *
 * Il punto delicato e' la scalatura. Il canvas di `lockCanvas` e' **software**: ridimensionare
 * un 1280x720 fino a riempire uno schermo da 1600x2560 costa piu' della decodifica JPEG, e su
 * un tablet fa scendere i fps da 20 a 13. La soluzione e' `setFixedSize`: il buffer della
 * superficie viene fissato alla dimensione esatta del frame, il disegno diventa una copia 1:1
 * e l'ingrandimento lo fa il compositore hardware, gratis.
 */
class MjpegSurfaceView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
) : SurfaceView(context, attrs) {

    private val paint = Paint(Paint.FILTER_BITMAP_FLAG)
    private val fallbackDestination = Rect()

    @Volatile
    private var ready = false

    @Volatile
    private var contentWidth = 0

    @Volatile
    private var contentHeight = 0

    init {
        holder.addCallback(object : SurfaceHolder.Callback {
            override fun surfaceCreated(holder: SurfaceHolder) {
                ready = true
            }

            override fun surfaceChanged(holder: SurfaceHolder, format: Int, width: Int, height: Int) = Unit

            override fun surfaceDestroyed(holder: SurfaceHolder) {
                ready = false
            }
        })
    }

    /** Disegna il frame. Chiamabile da un thread di background. */
    fun drawFrame(bitmap: Bitmap) {
        if (!ready) return

        if (bitmap.width != contentWidth || bitmap.height != contentHeight) {
            contentWidth = bitmap.width
            contentHeight = bitmap.height
            // setFixedSize e requestLayout vogliono il main thread.
            post {
                holder.setFixedSize(contentWidth, contentHeight)
                requestLayout()
            }
        }

        val canvas = holder.lockCanvas() ?: return
        try {
            if (canvas.width == bitmap.width && canvas.height == bitmap.height) {
                // Caso normale: copia 1:1, nessuna scalatura software.
                canvas.drawBitmap(bitmap, 0f, 0f, null)
            } else {
                // Solo per i primi frame, finche' setFixedSize non ha fatto effetto.
                canvas.drawColor(Color.BLACK)
                fitCentered(bitmap.width, bitmap.height, canvas.width, canvas.height, fallbackDestination)
                canvas.drawBitmap(bitmap, null, fallbackDestination, paint)
            }
        } finally {
            holder.unlockCanvasAndPost(canvas)
        }
    }

    /**
     * La view si dimensiona con le proporzioni del frame dentro lo spazio disponibile: cosi' il
     * letterbox lo fa il layout una volta sola, invece del disegno a ogni frame.
     */
    override fun onMeasure(widthMeasureSpec: Int, heightMeasureSpec: Int) {
        val availableWidth = MeasureSpec.getSize(widthMeasureSpec)
        val availableHeight = MeasureSpec.getSize(heightMeasureSpec)

        if (contentWidth <= 0 || contentHeight <= 0 || availableWidth <= 0 || availableHeight <= 0) {
            setMeasuredDimension(availableWidth, availableHeight)
            return
        }

        val scale = minOf(
            availableWidth.toFloat() / contentWidth,
            availableHeight.toFloat() / contentHeight,
        )
        setMeasuredDimension((contentWidth * scale).roundToInt(), (contentHeight * scale).roundToInt())
    }

    /** Riquadro piu' grande con le proporzioni del frame che entra nella superficie, centrato. */
    private fun fitCentered(srcW: Int, srcH: Int, dstW: Int, dstH: Int, out: Rect) {
        if (srcW <= 0 || srcH <= 0) return
        val scale = minOf(dstW.toFloat() / srcW, dstH.toFloat() / srcH)
        val w = (srcW * scale).toInt()
        val h = (srcH * scale).toInt()
        val left = (dstW - w) / 2
        val top = (dstH - h) / 2
        out.set(left, top, left + w, top + h)
    }
}
