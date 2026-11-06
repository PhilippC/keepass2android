package keepass2android.miuix.kdbx

import keepass2android.miuix.data.KpEntry
import keepass2android.miuix.data.KpGroup

data class KdbxContent(
    val name: String,
    val groups: List<KpGroup>,
    val entries: List<KpEntry>,
)

expect object KdbxEngine {
    /**
     * 解锁并解析 .kdbx 文件。
     * 失败时抛出 IOException，message 为可直接展示的中文提示。
     */
    suspend fun unlock(filePath: String, password: String, keyFilePath: String?): KdbxContent
}
