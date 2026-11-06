package keepass2android.miuix.onedrive

import java.io.File
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder
import androidx.compose.runtime.mutableStateOf
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.longOrNull

actual data class OneDriveItem actual constructor(
    actual val id: String,
    actual val name: String,
    actual val isFolder: Boolean,
    actual val sizeBytes: Long,
)

actual object OneDriveService {

    private const val AUTHORITY = "https://login.microsoftonline.com/common/oauth2/v2.0"
    private const val GRAPH = "https://graph.microsoft.com/v1.0"
    private const val SCOPE = "Files.Read User.Read offline_access"
    private const val SESSION_FILE = "onedrive_session.json"

    private var clientId = ""
    private var sessionDir = "."
    private var accessToken: String? = null
    private var refreshToken: String? = null

    actual val isConfigured: Boolean get() = clientId.isNotBlank()
    actual val isSignedIn: Boolean get() = refreshToken != null

    actual fun init(cacheDirPath: String, clientId: String) {
        this.clientId = clientId.trim()
        this.sessionDir = cacheDirPath.ifBlank { "." }
        accessToken = null
        refreshToken = null
        runCatching {
            val file = File(sessionDir, SESSION_FILE)
            if (file.exists()) {
                val obj = Json.parseToJsonElement(file.readText()).jsonObject
                refreshToken = obj["refresh_token"]?.jsonPrimitive?.contentOrNull
            }
        }
    }

    private val signInScope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    private var signInJob: Job? = null
    private val signInStateInternal = mutableStateOf<OneDriveSignInState>(OneDriveSignInState.Idle)

    actual val signInState: OneDriveSignInState get() = signInStateInternal.value

    actual fun startSignIn() {
        if (signInStateInternal.value is OneDriveSignInState.Pending) return
        signInJob?.cancel()
        signInJob = signInScope.launch {
            try {
                doSignIn()
            } catch (e: Exception) {
                if (signInStateInternal.value !is OneDriveSignInState.Success) {
                    signInStateInternal.value = OneDriveSignInState.Failed(e.message ?: "登录失败")
                }
            }
        }
    }

    actual fun cancelSignIn() {
        signInJob?.cancel()
        signInJob = null
        if (signInStateInternal.value !is OneDriveSignInState.Success) {
            signInStateInternal.value = OneDriveSignInState.Idle
        }
    }

    actual fun ackSignIn() {
        if (signInStateInternal.value is OneDriveSignInState.Success) {
            signInStateInternal.value = OneDriveSignInState.Idle
        }
    }

    private suspend fun doSignIn() {
        check(clientId.isNotBlank()) { "未配置 Client ID" }
        val (startCode, startBody) = postForm(
            "$AUTHORITY/devicecode",
            mapOf("client_id" to clientId, "scope" to SCOPE),
        )
        if (startCode !in 200..299) throw IOException("发起登录失败（HTTP $startCode）")
        val start = Json.parseToJsonElement(startBody).jsonObject
        val deviceCode = start.str("device_code") ?: throw IOException("授权响应无效")
        val userCode = start.str("user_code") ?: throw IOException("授权响应无效")
        val verifyUrl = start.str("verification_uri") ?: "https://microsoft.com/devicelogin"
        var interval = start["interval"]?.jsonPrimitive?.longOrNull ?: 5L
        val deadline = System.currentTimeMillis() + (start["expires_in"]?.jsonPrimitive?.longOrNull ?: 900L) * 1000
        signInStateInternal.value = OneDriveSignInState.Pending(verifyUrl, userCode)

        while (System.currentTimeMillis() < deadline) {
            currentCoroutineContext().ensureActive()
            delay(interval * 1000)
            val (code, body) = postForm(
                "$AUTHORITY/token",
                mapOf(
                    "client_id" to clientId,
                    "device_code" to deviceCode,
                    "grant_type" to "urn:ietf:params:oauth:grant-type:device_code",
                ),
            )
            val obj = runCatching { Json.parseToJsonElement(body).jsonObject }.getOrNull()
            if (code in 200..299 && obj != null) {
                accessToken = obj.str("access_token") ?: throw IOException("授权响应无效")
                refreshToken = obj.str("refresh_token") ?: throw IOException("授权响应无效")
                persistSession()
                signInStateInternal.value = OneDriveSignInState.Success
                return
            }
            when (obj?.str("error")) {
                "authorization_pending" -> Unit
                "slow_down" -> interval += 5
                "authorization_declined" -> throw IOException("你拒绝了授权")
                "expired_token" -> throw IOException("授权已超时，请重试")
                else -> throw IOException("登录失败：${obj?.str("error_description") ?: "HTTP $code"}")
            }
        }
        throw IOException("授权已超时，请重试")
    }

    actual fun signOut() {
        accessToken = null
        refreshToken = null
        runCatching { File(sessionDir, SESSION_FILE).delete() }
    }

    actual suspend fun accountName(): String = withContext(Dispatchers.Default) {
        val body = graphGet("$GRAPH/me?\$select=displayName,userPrincipalName")
        val obj = runCatching { Json.parseToJsonElement(body).jsonObject }.getOrNull()
        obj?.str("displayName") ?: obj?.str("userPrincipalName") ?: "OneDrive 账号"
    }

    actual suspend fun listChildren(folderId: String?): List<OneDriveItem> = withContext(Dispatchers.Default) {
        val base = if (folderId == null) "$GRAPH/me/drive/root/children" else "$GRAPH/me/drive/items/$folderId/children"
        val url = "$base?\$select=id,name,folder,file,size"
        val arr = runCatching {
            Json.parseToJsonElement(graphGet(url)).jsonObject["value"]?.jsonArray
        }.getOrNull() ?: return@withContext emptyList()
        arr.mapNotNull { el ->
            val obj = runCatching { el.jsonObject }.getOrNull() ?: return@mapNotNull null
            OneDriveItem(
                id = obj.str("id") ?: return@mapNotNull null,
                name = obj.str("name") ?: "",
                isFolder = obj.containsKey("folder"),
                sizeBytes = obj["size"]?.jsonPrimitive?.longOrNull ?: 0L,
            )
        }.sortedWith(compareByDescending<OneDriveItem> { it.isFolder }.thenBy { it.name.lowercase() })
    }

    actual suspend fun downloadItem(
        item: OneDriveItem,
        targetDir: String,
        fallbackName: String,
    ): OneDriveFile = withContext(Dispatchers.Default) {
        val name = (item.name.ifBlank { fallbackName }).replace('/', '_')
        val target = File(targetDir).apply { mkdirs() }.resolve(name)
        val conn = openConnection("$GRAPH/me/drive/items/${item.id}/content", "GET")
        try {
            conn.setRequestProperty("Authorization", "Bearer ${ensureToken()}")
            var code = conn.responseCode
            if (code == 401) {
                conn.disconnect()
                refreshTokens()
                return@withContext downloadOnce(item, target)
            }
            if (code !in 200..299) throw IOException("下载失败（HTTP $code）")
            conn.inputStream.use { input ->
                target.outputStream().use { output -> input.copyTo(output, 64 * 1024) }
            }
            OneDriveFile(name, target.absolutePath, target.length())
        } finally {
            conn.disconnect()
        }
    }

    private fun downloadOnce(item: OneDriveItem, target: File): OneDriveFile {
        val conn = openConnection("$GRAPH/me/drive/items/${item.id}/content", "GET")
        try {
            conn.setRequestProperty("Authorization", "Bearer ${ensureToken()}")
            val code = conn.responseCode
            if (code !in 200..299) throw IOException("下载失败（HTTP $code）")
            conn.inputStream.use { input ->
                target.outputStream().use { output -> input.copyTo(output, 64 * 1024) }
            }
            return OneDriveFile(item.name, target.absolutePath, target.length())
        } finally {
            conn.disconnect()
        }
    }

    private fun graphGet(url: String, retry: Boolean = true): String {
        val conn = openConnection(url, "GET")
        try {
            conn.setRequestProperty("Authorization", "Bearer ${ensureToken()}")
            val code = conn.responseCode
            if (code == 401 && retry) {
                refreshTokens()
                return graphGet(url, retry = false)
            }
            if (code !in 200..299) {
                throw IOException(graphErrorMessage(conn, code))
            }
            return conn.inputStream.readBytes().toString(Charsets.UTF_8)
        } finally {
            conn.disconnect()
        }
    }

    private fun ensureToken(): String {
        accessToken?.let { return it }
        refreshTokens()
        return accessToken ?: throw IOException("未登录 OneDrive")
    }

    private fun refreshTokens() {
        val token = refreshToken ?: throw IOException("未登录 OneDrive")
        val (code, body) = postForm(
            "$AUTHORITY/token",
            mapOf(
                "client_id" to clientId,
                "refresh_token" to token,
                "grant_type" to "refresh_token",
                "scope" to SCOPE,
            ),
        )
        val obj = runCatching { Json.parseToJsonElement(body).jsonObject }.getOrNull()
        if (code !in 200..299 || obj == null) throw IOException("会话已过期，请重新登录")
        accessToken = obj.str("access_token") ?: throw IOException("会话已过期，请重新登录")
        obj.str("refresh_token")?.let { refreshToken = it }
        persistSession()
    }

    private fun persistSession() {
        runCatching {
            File(sessionDir).mkdirs()
            File(sessionDir, SESSION_FILE).writeText("""{"refresh_token":"${refreshToken ?: ""}"}""")
        }
    }

    private fun postForm(url: String, params: Map<String, String>): Pair<Int, String> {
        val conn = openConnection(url, "POST")
        try {
            conn.doOutput = true
            conn.setRequestProperty("Content-Type", "application/x-www-form-urlencoded")
            val body = params.entries.joinToString("&") {
                "${URLEncoder.encode(it.key, Charsets.UTF_8)}=${URLEncoder.encode(it.value, Charsets.UTF_8)}"
            }
            conn.outputStream.use { it.write(body.toByteArray(Charsets.UTF_8)) }
            val code = conn.responseCode
            val stream = if (code in 200..299) conn.inputStream else conn.errorStream
            return code to (stream?.readBytes()?.toString(Charsets.UTF_8) ?: "")
        } finally {
            conn.disconnect()
        }
    }

    private fun openConnection(url: String, method: String): HttpURLConnection =
        (URL(url).openConnection() as HttpURLConnection).apply {
            requestMethod = method
            instanceFollowRedirects = true
            connectTimeout = 15000
            readTimeout = 60000
            setRequestProperty("User-Agent", "Mozilla/5.0 (KeePass2Android-miuix-prototype)")
        }

    private fun graphErrorMessage(conn: HttpURLConnection, code: Int): String {
        val body = conn.errorStream?.readBytes()?.toString(Charsets.UTF_8)
        val message = body?.let {
            runCatching {
                Json.parseToJsonElement(it).jsonObject["error"]?.jsonObject?.str("message")
            }.getOrNull()
        }
        return when (code) {
            401 -> "会话已过期，请重新登录"
            403 -> "没有访问权限"
            404 -> "文件不存在或已被删除"
            else -> message ?: "请求失败（HTTP $code）"
        }
    }

    private fun kotlinx.serialization.json.JsonObject.str(key: String): String? =
        this[key]?.jsonPrimitive?.contentOrNull
}
