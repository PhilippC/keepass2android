package keepass2android.miuix.app

import android.os.Bundle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.core.content.ContextCompat
import androidx.fragment.app.FragmentActivity
import java.io.File
import keepass2android.miuix.App

class MainActivity : FragmentActivity() {

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

    private fun biometricAvailable(): Boolean =
        BiometricManager.from(this)
            .canAuthenticate(BiometricManager.Authenticators.BIOMETRIC_WEAK) == BiometricManager.BIOMETRIC_SUCCESS

    private fun hasQuickUnlock(dbPath: String): Boolean =
        QuickUnlockStore.has(this, dbPath) && biometricAvailable()

    private fun quickUnlock(
        dbPath: String,
        onSuccess: (password: String, keyFilePath: String?) -> Unit,
        onError: (String) -> Unit,
    ) {
        if (!QuickUnlockStore.has(this, dbPath)) {
            onError("未找到已保存的解锁凭据")
            return
        }
        val prompt = BiometricPrompt(
            this,
            ContextCompat.getMainExecutor(this),
            object : BiometricPrompt.AuthenticationCallback() {
                override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) {
                    val credentials = QuickUnlockStore.load(this@MainActivity, dbPath)
                    if (credentials != null) {
                        onSuccess(credentials.first, credentials.second)
                    } else {
                        onError("无法读取已保存的解锁凭据，请重新输入主密码")
                    }
                }

                override fun onAuthenticationError(errorCode: Int, errString: CharSequence) {
                    if (errorCode != BiometricPrompt.ERROR_NEGATIVE_BUTTON &&
                        errorCode != BiometricPrompt.ERROR_USER_CANCELED
                    ) {
                        onError(errString.toString())
                    }
                }
            },
        )
        val promptInfo = BiometricPrompt.PromptInfo.Builder()
            .setTitle("指纹解锁数据库")
            .setSubtitle("验证指纹以快速解锁")
            .setNegativeButtonText("取消")
            .build()
        prompt.authenticate(promptInfo)
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
                hasQuickUnlock = ::hasQuickUnlock,
                quickUnlock = ::quickUnlock,
                saveQuickUnlock = { path, password, keyPath ->
                    QuickUnlockStore.save(this, path, password, keyPath)
                },
            )
        }
    }
}
