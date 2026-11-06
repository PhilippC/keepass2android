package keepass2android.miuix.fs

import java.io.File

actual object LocalFileSystem {

    actual fun list(dirPath: String): List<LocalFileEntry> =
        (File(dirPath).listFiles() ?: emptyArray())
            .filter { !it.name.startsWith(".") }
            .sortedWith(
                compareByDescending<File> { it.isDirectory }
                    .thenBy { it.name.lowercase() },
            )
            .map { LocalFileEntry(it.name, it.absolutePath, it.isDirectory, it.length()) }

    actual fun externalStorageRoot(): String {
        // Android 内部存储根目录
        val androidRoot = File("/storage/emulated/0")
        if (androidRoot.exists() && androidRoot.isDirectory) return androidRoot.absolutePath
        // 桌面端回退到用户主目录
        return File(System.getProperty("user.home") ?: ".").absolutePath
    }
}
