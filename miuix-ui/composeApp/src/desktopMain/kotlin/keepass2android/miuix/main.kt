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
        App(
            cacheDirPath = System.getProperty("java.io.tmpdir") ?: ".",
            oneDriveClientId = System.getenv("ONEDRIVE_CLIENT_ID")
                ?: System.getProperty("onedrive.clientId") ?: "",
            onOpenUrl = { url ->
                runCatching { java.awt.Desktop.getDesktop().browse(java.net.URI(url)) }
            },
        )
    }
}
