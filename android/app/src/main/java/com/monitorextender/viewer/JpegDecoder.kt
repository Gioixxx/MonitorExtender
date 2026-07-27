package com.monitorextender.viewer

import android.graphics.Bitmap
import android.graphics.BitmapFactory

/**
 * Decodifica i frame JPEG riciclando sempre lo stesso Bitmap.
 *
 * Tutti i frame dello stream hanno la stessa dimensione, quindi `inBitmap` permette a
 * BitmapFactory di riscrivere la memoria gia' allocata invece di chiederne di nuova. Senza,
 * a 20 fps si allocano e si buttano ~25 MB/s di bitmap: il garbage collector interviene di
 * continuo e il video singhiozza.
 */
class JpegDecoder {

    private val options = BitmapFactory.Options().apply { inMutable = true }
    private var recycled: Bitmap? = null

    fun decode(frame: MjpegStream.Frame): Bitmap? {
        options.inBitmap = recycled

        val bitmap = try {
            BitmapFactory.decodeByteArray(frame.data, 0, frame.length, options)
        } catch (_: IllegalArgumentException) {
            // Il bitmap candidato non era riutilizzabile (cambio di risoluzione dello stream):
            // si riparte da zero invece di rinunciare al frame.
            options.inBitmap = null
            recycled = null
            BitmapFactory.decodeByteArray(frame.data, 0, frame.length, options)
        }

        if (bitmap != null) recycled = bitmap
        return bitmap
    }
}
