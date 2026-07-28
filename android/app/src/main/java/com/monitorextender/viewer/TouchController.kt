package com.monitorextender.viewer

import android.os.SystemClock
import android.view.MotionEvent
import android.view.View
import kotlin.math.abs

/**
 * Traduce i gesti sul tablet in comandi di mouse per il PC.
 *
 * La mappatura e' quella di un monitor touch, non di un trackpad: si tocca il punto dove si
 * vuole cliccare, e il cursore ci va. Per questo il tasto sinistro viene premuto subito al
 * tocco, senza attendere per capire se sara' un click o un trascinamento — l'attesa si
 * sentirebbe su ogni singolo tocco.
 *
 * Di conseguenza il click destro **non** puo' essere la pressione lunga (il sinistro e' gia'
 * premuto): e' il tocco con due dita. Quando arriva il secondo dito, il sinistro premuto per
 * sbaglio viene rilasciato subito.
 *
 *   un dito              -> premi, muovi, rilascia (click e trascinamento)
 *   due dita, scorrimento -> rotella
 *   due dita, tocco       -> click destro
 *   tre dita, tocco       -> mostra o nasconde le statistiche
 *
 * Le coordinate sono immediate perche' la view ha esattamente le proporzioni del frame
 * (vedi MjpegSurfaceView.onMeasure): la posizione nella view **e'** la posizione sullo schermo
 * del PC, normalizzata.
 */
class TouchController(
    private val sender: InputSender,
    private val onToggleOverlay: () -> Unit,
) : View.OnTouchListener {

    private var leftDown = false
    private var maxPointers = 0
    private var gestureStart = 0L
    private var movedFar = false
    private var startX = 0f
    private var startY = 0f
    private var lastScrollY = 0f
    private var scrollRemainder = 0f

    override fun onTouch(view: View, event: MotionEvent): Boolean {
        when (event.actionMasked) {
            MotionEvent.ACTION_DOWN -> {
                maxPointers = 1
                gestureStart = SystemClock.elapsedRealtime()
                movedFar = false
                startX = event.x
                startY = event.y

                sender.move(event.x / view.width, event.y / view.height)
                sender.buttonDown(LEFT)
                leftDown = true
            }

            MotionEvent.ACTION_POINTER_DOWN -> {
                maxPointers = maxOf(maxPointers, event.pointerCount)
                // Il primo dito aveva gia' premuto il sinistro: va annullato prima che
                // diventi un trascinamento indesiderato.
                releaseLeft()
                if (event.pointerCount == 2) {
                    lastScrollY = midY(event)
                    scrollRemainder = 0f
                }
            }

            MotionEvent.ACTION_MOVE -> {
                if (!movedFar && farFrom(event)) movedFar = true

                when {
                    event.pointerCount == 1 && leftDown ->
                        sender.move(event.x / view.width, event.y / view.height)

                    event.pointerCount == 2 -> scroll(event)
                }
            }

            MotionEvent.ACTION_UP -> {
                releaseLeft()
                finishGesture()
            }

            MotionEvent.ACTION_CANCEL -> {
                releaseLeft()
                maxPointers = 0
            }
        }
        return true
    }

    /**
     * Scorrimento naturale: il dito trascina il contenuto, quindi scendere manda la rotella
     * verso l'alto. E' l'aspettativa di chi tocca l'immagine, non quella di chi usa il mouse.
     */
    private fun scroll(event: MotionEvent) {
        val y = midY(event)
        scrollRemainder += y - lastScrollY
        lastScrollY = y

        while (abs(scrollRemainder) >= PIXELS_PER_NOTCH) {
            val up = scrollRemainder > 0
            sender.wheel(if (up) WHEEL_NOTCH else -WHEEL_NOTCH)
            scrollRemainder += if (up) -PIXELS_PER_NOTCH else PIXELS_PER_NOTCH
        }
    }

    private fun finishGesture() {
        val quick = SystemClock.elapsedRealtime() - gestureStart < TAP_MS
        if (quick && !movedFar) {
            when (maxPointers) {
                2 -> sender.click(RIGHT)
                3 -> onToggleOverlay()
            }
        }
        maxPointers = 0
    }

    private fun releaseLeft() {
        if (leftDown) {
            sender.buttonUp(LEFT)
            leftDown = false
        }
    }

    private fun farFrom(event: MotionEvent) =
        abs(event.x - startX) > TOUCH_SLOP || abs(event.y - startY) > TOUCH_SLOP

    private fun midY(event: MotionEvent) = (event.getY(0) + event.getY(1)) / 2f

    private companion object {
        const val LEFT = "left"
        const val RIGHT = "right"
        const val WHEEL_NOTCH = 120
        const val PIXELS_PER_NOTCH = 40f
        const val TOUCH_SLOP = 24f
        const val TAP_MS = 300L
    }
}
