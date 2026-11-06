package keepass2android.miuix.kdbx

import java.io.File
import java.io.FileInputStream
import java.io.IOException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.linguafranca.pwdb.kdbx.KdbxCreds
import org.linguafranca.pwdb.kdbx.dom.DomDatabaseWrapper
import org.linguafranca.pwdb.kdbx.dom.DomEntryWrapper
import org.linguafranca.pwdb.kdbx.dom.DomGroupWrapper
import keepass2android.miuix.data.EntryField
import keepass2android.miuix.data.KpEntry
import keepass2android.miuix.data.KpGroup
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.BankCards
import top.yukonga.miuix.kmp.icon.extended.Contacts
import top.yukonga.miuix.kmp.icon.extended.Email
import top.yukonga.miuix.kmp.icon.extended.GridView
import top.yukonga.miuix.kmp.icon.extended.Link
import top.yukonga.miuix.kmp.icon.extended.Lock
import top.yukonga.miuix.kmp.icon.extended.Notes
import top.yukonga.miuix.kmp.icon.extended.WorldClock

actual object KdbxEngine {

    actual suspend fun unlock(
        filePath: String,
        password: String,
        keyFilePath: String?,
    ): KdbxContent = withContext(Dispatchers.IO) {
        val file = File(filePath)
        if (!file.exists()) throw IOException("数据库文件不存在")
        val creds = if (keyFilePath != null) {
            val keyFile = File(keyFilePath)
            if (!keyFile.exists()) throw IOException("密钥文件不存在")
            if (password.isEmpty()) {
                KdbxCreds(keyFile.inputStream())
            } else {
                KdbxCreds(password.toByteArray(Charsets.UTF_8), keyFile.inputStream())
            }
        } else {
            if (password.isEmpty()) throw IOException("请输入主密码")
            KdbxCreds(password.toByteArray(Charsets.UTF_8))
        }

        try {
            val db = FileInputStream(file).use { input -> DomDatabaseWrapper.load(creds, input) }
            var groupId = 0
            var entryId = 0
            val groups = mutableListOf<KpGroup>()
            val entries = mutableListOf<KpEntry>()
            val dbName = db.name ?: file.nameWithoutExtension

            fun walk(group: DomGroupWrapper, parentId: Int) {
                val myId = groupId++
                val title = group.name
                    ?: (if (parentId == -1) dbName else "未命名分组")
                groups.add(KpGroup(myId, parentId, title))
                group.entries.forEach { entry ->
                    val id = entryId++
                    entries.add(convert(id, myId, entry))
                }
                group.groups.forEach { walk(it, myId) }
            }
            walk(db.rootGroup, -1)
            KdbxContent(dbName, groups, entries)
        } catch (e: Exception) {
            throw when {
                e is IOException -> e
                e.message?.contains("version", ignoreCase = true) == true ->
                    IOException("暂不支持该数据库格式（KDBX 4），请用 KeePass / KeePassXC 将其另存为 KDBX 3 后重试")
                else ->
                    IOException("解锁失败：主密码或密钥文件不正确，或文件已损坏")
            }
        }
    }

    private fun convert(id: Int, groupId: Int, entry: DomEntryWrapper): KpEntry {
        val title = entry.getProperty("Title") ?: ""
        val userName = entry.getProperty("UserName") ?: ""
        val url = entry.getProperty("URL") ?: ""
        val notes = entry.getProperty("Notes") ?: ""
        val secret = entry.getProperty("Password") ?: ""
        val standard = setOf("Title", "UserName", "URL", "Notes", "Password")
        val fields = buildList {
            if (userName.isNotEmpty()) add(EntryField("用户名", userName))
            if (secret.isNotEmpty()) add(EntryField("密码", secret, protected = true))
            if (url.isNotEmpty()) add(EntryField("网址", url))
            runCatching { entry.propertyNames }.getOrNull()
                ?.filter { it !in standard }
                ?.forEach { fieldName ->
                    entry.getProperty(fieldName)?.let { value ->
                        if (value.isNotEmpty()) add(EntryField(fieldName, value))
                    }
                }
            if (notes.isNotEmpty()) add(EntryField("备注", notes))
        }
        return KpEntry(
            id = id,
            groupId = groupId,
            title = title,
            userName = userName,
            icon = iconFor(entry),
            notes = notes.ifEmpty { null },
            fields = fields,
        )
    }

    private fun iconFor(entry: DomEntryWrapper) = runCatching { entry.icon.index }.getOrNull()?.let { index ->
        val pool = listOf(
            MiuixIcons.Email,
            MiuixIcons.Lock,
            MiuixIcons.BankCards,
            MiuixIcons.Link,
            MiuixIcons.Contacts,
            MiuixIcons.Notes,
            MiuixIcons.WorldClock,
            MiuixIcons.GridView,
        )
        pool[index.mod(pool.size)]
    } ?: MiuixIcons.Lock
}
