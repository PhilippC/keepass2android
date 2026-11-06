package keepass2android.miuix.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import java.io.File
import keepass2android.miuix.App

class MainActivity : ComponentActivity() {

    private var filePickCallback: ((name: String, path: String, size: Long) -> Unit)? = null

    private val openDocument = registerForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        val callback = filePickCallback
        filePickCallback = null
        if (uri == null || callback == null) return@registerForActivityResult
        var name = uri.lastPathSegment?.substringAfterLast('/') ?: "database.kdbx"
        var reportedSize = -1L
        runCatching {
            contentResolver.query(
                uri,
                arrayOf(android.provider.OpenableColumns.DISPLAY_NAME, android.provider.OpenableColumns.SIZE),
                null,
                null,
                null,
            )?.use { cursor ->
                if (cursor.moveToFirst()) {
                    cursor.getString(0)?.let { name = it }
                    if (!cursor.isNull(1)) reportedSize = cursor.getLong(1)
                }
            }
        }
        runCatching {
            val target = File(cacheDir, name.replace('/', '_'))
            contentResolver.openInputStream(uri)?.use { input ->
                target.outputStream().use { output -> input.copyTo(output) }
            }
            callback(name, target.absolutePath, if (reportedSize >= 0) reportedSize else target.length())
        }
    }

    fun pickFile(onResult: (name: String, path: String, size: Long) -> Unit) {
        filePickCallback = onResult
        runCatching { openDocument.launch(arrayOf("*/*")) }.onFailure { filePickCallback = null }
    }

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
            App(
                cacheDirPath = cacheDir.absolutePath,
                oneDriveClientId = clientId,
                onOpenUrl = { url ->
                    runCatching {
                        startActivity(
                            android.content.Intent(
                                android.content.Intent.ACTION_VIEW,
                                android.net.Uri.parse(url),
                            )
                        )
                    }
                },
                onPickFile = ::pickFile,
            )
        }
    }
}
