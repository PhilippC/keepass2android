package keepass2android.miuix

import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Window
import androidx.compose.ui.window.application
import androidx.compose.ui.window.rememberWindowState

fun main() = application {
    Window(
        onCloseRequest = ::exitApplication,
        title = "KeePass2Android · miuix 原型",
        state = rememberWindowState(width = 420.dp, height = 780.dp),
    ) {
        App()
    }
}
