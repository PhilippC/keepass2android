package keepass2android.miuix.onedrive

import java.io.File
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLDecoder
import java.util.Base64

actual data class OneDriveFile actual constructor(
    actual val name: String,
    actual val path: String,
    actual val sizeBytes: Long,
)

actual fun fetchOneDriveSharedFile(
    shareLink: String,
    targetDir: String,
    fallbackName: String,
): OneDriveFile = OneDriveClient.fetchSharedFile(shareLink, targetDir, fallbackName)

object OneDriveClient {

    private const val API = "https://api.onedrive.com/v1.0/shares/"

    fun encodeShare(link: String): String =
        "u!" + Base64.getUrlEncoder().withoutPadding()
            .encodeToString(link.trim().toByteArray(Charsets.UTF_8))

    fun fetchSharedFile(shareLink: String, targetDir: String, fallbackName: String): OneDriveFile {
        val link = shareLink.trim()
        require(link.startsWith("http://") || link.startsWith("https://")) {
            "请粘贴有效的分享链接（以 https:// 开头）"
        }
        val url = URL(API + encodeShare(link) + "/root/content")
        val conn = (url.openConnection() as HttpURLConnection).apply {
            instanceFollowRedirects = true
            connectTimeout = 15000
            readTimeout = 60000
            setRequestProperty("User-Agent", "Mozilla/5.0 (KeePass2Android-miuix-prototype)")
        }
        try {
            when (val code = conn.responseCode) {
                400, 404 -> throw IOException("链接无效或文件已被删除")
                401, 403 -> throw IOException("没有访问权限：该链接可能需要登录，暂不支持受保护链接")
                in 300..399 -> throw IOException("重定向次数过多，请检查链接")
                !in 200..299 -> throw IOException("下载失败（HTTP $code）")
            }
            val name = resolveFileName(conn, link, fallbackName)
            val declaredSize = conn.contentLengthLong
            val target = File(targetDir).apply { mkdirs() }.resolve(name)
            conn.inputStream.use { input ->
                target.outputStream().use { output -> input.copyTo(output, 64 * 1024) }
            }
            return OneDriveFile(
                name = name,
                path = target.absolutePath,
                sizeBytes = if (declaredSize > 0) declaredSize else target.length(),
            )
        } finally {
            conn.disconnect()
        }
    }

    private fun resolveFileName(conn: HttpURLConnection, shareLink: String, fallback: String): String {
        header(conn, "Content-Disposition")?.let { cd ->
            Regex("filename\\*=UTF-8''([^;]+)", RegexOption.IGNORE_CASE).find(cd)?.groupValues?.get(1)?.let {
                return URLDecoder.decode(it, Charsets.UTF_8)
            }
            Regex("filename=\"?([^\";]+)\"?", RegexOption.IGNORE_CASE).find(cd)?.groupValues?.get(1)?.let {
                return it.trim()
            }
        }
        shareLink.substringBefore('?').trimEnd('/').substringAfterLast('/').takeIf { it.contains('.') }?.let {
            return URLDecoder.decode(it, Charsets.UTF_8)
        }
        return fallback
    }

    private fun header(conn: HttpURLConnection, name: String): String? =
        conn.headerFields.entries
            .firstOrNull { it.key?.equals(name, ignoreCase = true) == true }
            ?.value?.firstOrNull()
}
