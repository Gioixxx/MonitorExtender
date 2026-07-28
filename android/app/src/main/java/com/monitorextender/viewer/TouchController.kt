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

                // Appoggiare piu' dita insieme sposta inevitabilmente il primo: se si
                // continuasse a misurare dalla posizione iniziale, ogni gesto a piu' dita
                // risulterebbe "mosso" e verrebbe scartato. Si riparte da qui.
                startX = event.x
                startY = event.y
                movedFar = false
                gestureStart = SystemClock.elapsedRealtime()

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
        // Piu' dita sono, meno sincronizzate arrivano e piu' scivolano: le soglie di tempo e
        // di movimento vanno allentate, altrimenti il gesto a tre dita non scatta mai.
        val limit = if (maxPointers >= 2) MULTI_TAP_MS else TAP_MS
        val quick = SystemClock.elapsedRealtime() - gestureStart < limit

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

    private fun farFrom(event: MotionEvent): Boolean {
        val slop = if (maxPointers >= 2) MULTI_TOUCH_SLOP else TOUCH_SLOP
        return abs(event.x - startX) > slop || abs(event.y - startY) > slop
    }

    private fun midY(event: MotionEvent) = (event.getY(0) + event.getY(1)) / 2f

    private companion object {
        const val LEFT = "left"
        const val RIGHT = "right"
        const val WHEEL_NOTCH = 120
        const val PIXELS_PER_NOTCH = 40f
        const val TOUCH_SLOP = 24f
        const val MULTI_TOUCH_SLOP = 90f
        const val TAP_MS = 300L
        const val MULTI_TAP_MS = 700L
    }
}
