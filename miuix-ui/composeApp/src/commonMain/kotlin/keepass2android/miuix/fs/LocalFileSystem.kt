package keepass2android.miuix.fs

data class LocalFileEntry(
    val name: String,
    val path: String,
    val isDirectory: Boolean,
    val size: Long,
)

expect object LocalFileSystem {
    fun list(dirPath: String): List<LocalFileEntry>
    fun externalStorageRoot(): String
}
