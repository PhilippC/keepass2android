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

    private val prefs by lazy { getSharedPreferences("miuix_app", MODE_PRIVATE) }

    private fun loadRecents(): List<keepass2android.miuix.data.RecentDatabase> {
        val raw = prefs.getString("recent_databases", null) ?: return keepass2android.miuix.data.DemoData.recentDatabases
        val decoder = java.util.Base64.getDecoder()
        return raw.lineSequence().filter { it.isNotBlank() }.mapNotNull { line ->
            val parts = line.split("|")
            if (parts.size != 3) return@mapNotNull null
            runCatching {
                fun dec(s: String) = String(decoder.decode(s), Charsets.UTF_8)
                keepass2android.miuix.data.RecentDatabase(dec(parts[0]), dec(parts[1]), dec(parts[2]).ifEmpty { null })
            }.getOrNull()
        }.toList()
    }

    private fun saveRecents(recents: List<keepass2android.miuix.data.RecentDatabase>) {
        val encoder = java.util.Base64.getEncoder()
        val raw = recents.joinToString("\n") {
            "${encoder.encodeToString(it.name.toByteArray(Charsets.UTF_8))}|" +
                "${encoder.encodeToString(it.path.toByteArray(Charsets.UTF_8))}|" +
                encoder.encodeToString((it.realPath ?: "").toByteArray(Charsets.UTF_8))
        }
        prefs.edit().putString("recent_databases", raw).apply()
    }

    private var filePickCallback: ((name: String, path: String, size: Long) -> Unit)? = null
    private var filePickForKeyFile = false

    private val filePickerLauncher =
        registerForActivityResult(ActivityResultContracts.StartActivityForResult()) { result ->
            val callback = filePickCallback
            filePickCallback = null
            if (result.resultCode != RESULT_OK || callback == null) return@registerForActivityResult
            val data = result.data ?: return@registerForActivityResult
            val path = data.getStringExtra("path") ?: return@registerForActivityResult
            callback(
                data.getStringExtra("name") ?: "file",
                path,
                data.getLongExtra("size", -1L),
            )
        }

    // 从「所有文件访问」设置页返回后，若已授权则继续打开文件浏览器
    private val allFilesAccessLauncher =
        registerForActivityResult(ActivityResultContracts.StartActivityForResult()) {
            if (isFileAccessGranted()) {
                launchFilePicker()
            } else {
                filePickCallback = null
                android.widget.Toast.makeText(
                    this,
                    "未授予「所有文件访问」权限，无法浏览本地文件",
                    android.widget.Toast.LENGTH_LONG,
                ).show()
            }
        }

    private val legacyStoragePermissionLauncher =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
            if (granted && filePickCallback != null) {
                launchFilePicker()
            } else {
                filePickCallback = null
                android.widget.Toast.makeText(
                    this,
                    "未授予存储权限，无法浏览本地文件",
                    android.widget.Toast.LENGTH_LONG,
                ).show()
            }
        }

    private fun isFileAccessGranted(): Boolean =
        if (android.os.Build.VERSION.SDK_INT >= 30) {
            android.os.Environment.isExternalStorageManager()
        } else {
            ContextCompat.checkSelfPermission(
                this,
                android.Manifest.permission.READ_EXTERNAL_STORAGE,
            ) == android.content.pm.PackageManager.PERMISSION_GRANTED
        }

    private fun launchFilePicker() {
        val mode = if (filePickForKeyFile) FilePickerActivity.MODE_KEY_FILE else FilePickerActivity.MODE_DATABASE
        val intent = android.content.Intent(this, FilePickerActivity::class.java)
            .putExtra(FilePickerActivity.EXTRA_MODE, mode)
        runCatching { filePickerLauncher.launch(intent) }.onFailure { filePickCallback = null }
    }

    fun pickFile(forKeyFile: Boolean, onResult: (name: String, path: String, size: Long) -> Unit) {
        filePickCallback = onResult
        filePickForKeyFile = forKeyFile
        if (isFileAccessGranted()) {
            launchFilePicker()
        } else if (android.os.Build.VERSION.SDK_INT >= 30) {
            // Android 11+：跳转系统设置页申请「所有文件访问」
            val settingsIntent = android.content.Intent(
                android.provider.Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION,
                android.net.Uri.parse("package:$packageName"),
            )
            val launched = runCatching { allFilesAccessLauncher.launch(settingsIntent) }.isSuccess
            if (!launched) {
                runCatching {
                    allFilesAccessLauncher.launch(
                        android.content.Intent(android.provider.Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION),
                    )
                }.onFailure {
                    filePickCallback = null
                    android.widget.Toast.makeText(
                        this,
                        "无法打开权限设置页，请手动在系统设置中授予本应用「所有文件访问」",
                        android.widget.Toast.LENGTH_LONG,
                    ).show()
                }
            }
        } else {
            // Android 10 及以下：运行时申请存储权限
            legacyStoragePermissionLauncher.launch(android.Manifest.permission.READ_EXTERNAL_STORAGE)
        }
    }

    private var createFileCallback: ((name: String, path: String) -> Unit)? = null
    private var pendingExport: Pair<android.net.Uri, File>? = null

    private val createDocument = registerForActivityResult(ActivityResultContracts.CreateDocument("application/octet-stream")) { uri ->
        val callback = createFileCallback
        createFileCallback = null
        if (uri == null || callback == null) return@registerForActivityResult
        var name = uri.lastPathSegment?.substringAfterLast('/') ?: "keepass.kdbx"
        runCatching {
            contentResolver.query(uri, arrayOf(android.provider.OpenableColumns.DISPLAY_NAME), null, null, null)?.use { cursor ->
                if (cursor.moveToFirst()) cursor.getString(0)?.let { name = it }
            }
        }
        // 引擎先写临时文件，创建成功后再由 commitFile 回写到 SAF 目标
        val temp = File(cacheDir, "create-${System.currentTimeMillis()}-${name.replace('/', '_')}")
        pendingExport = uri to temp
        callback(name, temp.absolutePath)
    }

    fun createFile(suggestedName: String, onResult: (name: String, path: String) -> Unit) {
        createFileCallback = onResult
        runCatching { createDocument.launch(suggestedName) }.onFailure { createFileCallback = null }
    }

    fun commitFile(path: String, onDone: (Boolean) -> Unit) {
        val pending = pendingExport
        if (pending == null || pending.second.absolutePath != path) {
            onDone(true)
            return
        }
        pendingExport = null
        runCatching {
            contentResolver.openOutputStream(pending.first)?.use { output ->
                pending.second.inputStream().use { input -> input.copyTo(output) }
            } ?: error("无法写入目标位置")
            pending.second.delete()
        }.onSuccess { onDone(true) }.onFailure { onDone(false) }
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
                loadRecents = ::loadRecents,
                saveRecents = ::saveRecents,
                onCreateFile = ::createFile,
                commitFile = ::commitFile,
                defaultDatabasePath = File(
                    getExternalFilesDir(null) ?: filesDir,
                    "keepass.kdbx",
                ).absolutePath,
                hasQuickUnlock = ::hasQuickUnlock,
                quickUnlock = ::quickUnlock,
                saveQuickUnlock = { path, password, keyPath ->
                    QuickUnlockStore.save(this, path, password, keyPath)
                },
            )
        }
    }
}
