package keepass2android.miuix.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import keepass2android.miuix.App

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        val clientId = runCatching {
            packageManager.getApplicationInfo(
                packageName,
                android.content.pm.PackageManager.GET_META_DATA,
            ).metaData?.getString("onedrive_client_id").orEmpty()
        }.getOrDefault("")
        setContent {
            App(cacheDirPath = cacheDir.absolutePath, oneDriveClientId = clientId)
        }
    }
}
